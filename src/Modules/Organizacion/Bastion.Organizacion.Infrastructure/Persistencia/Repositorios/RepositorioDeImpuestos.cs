using System.Linq.Expressions;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Organizacion.Application.Impuestos;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Domain.Impuestos;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeImpuestos"/>
internal sealed class RepositorioDeImpuestos(OrganizacionDbContext contexto) : IRepositorioDeImpuestos
{
    public Task<Impuesto?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Impuestos.FirstOrDefaultAsync(impuesto => impuesto.Id == id, cancelacion);

    public Task<bool> HaySolapeAsync(
        string codigo,
        DateOnly desde,
        DateOnly? hasta,
        Guid? excepto,
        CancellationToken cancelacion) =>
        contexto.Impuestos.AnyAsync(
            impuesto => impuesto.Codigo == codigo
                && (excepto == null || impuesto.Id != excepto)

                // Dos intervalos cerrados se pisan cuando cada uno empieza antes de que acabe el
                // otro. El extremo NULO se lee como «hasta siempre», que es lo mismo que hace el
                // `daterange` de la restricción de la base: un rango sin límite superior. Las dos
                // comparaciones son <= porque los dos extremos están INCLUIDOS, igual que en
                // `Impuesto.RigeEl` — con < , dos tramos que compartieran un solo día pasarían por
                // aquí y los pararía la base con un 500 en vez de esta respuesta.
                && impuesto.VigenteDesde <= (hasta ?? DateOnly.MaxValue)
                && desde <= (impuesto.VigenteHasta ?? DateOnly.MaxValue),
            cancelacion);

    // El desempate lleva la vigencia porque un impuesto es varios tramos con el mismo código:
    // sin ella, los tramos de un mismo código salen en el orden que quiera el plan de ejecución,
    // y el primero de la lista —el que se lee como «el vigente»— cambia entre dos consultas.
    private static readonly CriteriosDe<Impuesto> s_criterios = new()
    {
        Ordenables = new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["codigo"] = (Expression<Func<Impuesto, string>>)(impuesto => impuesto.Codigo),
            ["nombre"] = (Expression<Func<Impuesto, string>>)(impuesto => impuesto.Nombre),
            ["porcentaje"] = (Expression<Func<Impuesto, decimal>>)(impuesto => impuesto.Porcentaje),
        },
        PorOmision = "codigo",
        Desempate = ordenada => ordenada
            .ThenByDescending(impuesto => impuesto.VigenteDesde)
            .ThenBy(impuesto => impuesto.Id),
        Filtro = texto =>
        {
            string patron = Filtros.Contiene(texto);

            return impuesto => EF.Functions.ILike(impuesto.Codigo, patron, Filtros.Escape)
                || EF.Functions.ILike(impuesto.Nombre, patron, Filtros.Escape);
        },
    };

    public IReadOnlySet<string> CamposOrdenables => s_criterios.CamposOrdenables;

    public Task<PaginaDe<Impuesto>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion) =>
        contexto.Impuestos.PaginarAsync(paginacion, s_criterios, cancelacion);

    public void Agregar(Impuesto impuesto) => contexto.Impuestos.Add(impuesto);
}
