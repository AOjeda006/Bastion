using System.Linq.Expressions;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Catalogo.Application.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Catalogo.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeTarifas"/>
internal sealed class RepositorioDeTarifas(CatalogoDbContext contexto) : IRepositorioDeTarifas
{
    // El desempate lleva la vigencia por lo mismo que el de los tramos de impuesto: una tarifa son
    // varios tramos con el mismo código, y sin ella el primero de la lista —el que se lee como «el
    // vigente»— cambiaría entre dos consultas según el plan de ejecución.
    private static readonly CriteriosDe<Tarifa> s_criterios = new()
    {
        Ordenables = new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["codigo"] = (Expression<Func<Tarifa, string>>)(tarifa => tarifa.Codigo),
            ["nombre"] = (Expression<Func<Tarifa, string>>)(tarifa => tarifa.Nombre),
            ["vigenteDesde"] = (Expression<Func<Tarifa, DateOnly>>)(tarifa => tarifa.VigenteDesde),
        },
        PorOmision = "codigo",
        Desempate = ordenada => ordenada
            .ThenByDescending(tarifa => tarifa.VigenteDesde)
            .ThenBy(tarifa => tarifa.Id),
        Filtro = texto =>
        {
            string patron = Filtros.Contiene(texto);

            return tarifa => EF.Functions.ILike(tarifa.Codigo, patron, Filtros.Escape)
                || EF.Functions.ILike(tarifa.Nombre, patron, Filtros.Escape);
        },
    };

    public IReadOnlySet<string> CamposOrdenables => s_criterios.CamposOrdenables;

    public Task<Tarifa?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Tarifas.FirstOrDefaultAsync(tarifa => tarifa.Id == id, cancelacion);

    /// <inheritdoc/>
    /// <remarks>
    /// Los dos extremos con <c>&lt;=</c>, que es lo mismo que dice <c>Tarifa.RigeEl</c> y lo mismo
    /// que construye el <c>daterange(…, '[]')</c> de la restricción de exclusión. Las tres
    /// convenciones son la misma a propósito: si ésta usara <c>&lt;</c>, el día en que un tramo
    /// acaba valdría una cosa al resolver un precio y otra al comprobar el solape.
    /// </remarks>
    public Task<Tarifa?> VigenteAsync(
        Guid empresaId,
        string codigo,
        DateOnly dia,
        CancellationToken cancelacion) =>
        contexto.Tarifas.FirstOrDefaultAsync(
            tarifa => tarifa.EmpresaId == empresaId
                && tarifa.Codigo == codigo
                && tarifa.VigenteDesde <= dia
                && (tarifa.VigenteHasta == null || dia <= tarifa.VigenteHasta),
            cancelacion);

    public Task<bool> ExisteElCodigoAsync(
        Guid empresaId,
        string codigo,
        CancellationToken cancelacion) =>
        contexto.Tarifas.AnyAsync(
            tarifa => tarifa.EmpresaId == empresaId && tarifa.Codigo == codigo,
            cancelacion);

    public Task<bool> HaySolapeAsync(
        Guid empresaId,
        string codigo,
        DateOnly desde,
        DateOnly? hasta,
        Guid? excepto,
        CancellationToken cancelacion) =>
        contexto.Tarifas.AnyAsync(
            tarifa => tarifa.EmpresaId == empresaId
                && tarifa.Codigo == codigo
                && (excepto == null || tarifa.Id != excepto)

                // Dos intervalos cerrados se pisan cuando cada uno empieza antes de que acabe el
                // otro. El extremo NULO se lee como «hasta siempre», que es lo mismo que hace el
                // `daterange` de la restricción: un rango sin límite superior. Las dos
                // comparaciones son <= porque los dos extremos están INCLUIDOS — con <, dos tramos
                // que compartieran un solo día pasarían por aquí y los pararía la base con un 500
                // en vez de esta respuesta.
                && tarifa.VigenteDesde <= (hasta ?? DateOnly.MaxValue)
                && desde <= (tarifa.VigenteHasta ?? DateOnly.MaxValue),
            cancelacion);

    public Task<PaginaDe<Tarifa>> ListarAsync(
        Paginacion paginacion,
        string? codigo,
        CancellationToken cancelacion)
    {
        IQueryable<Tarifa> consulta = contexto.Tarifas;

        if (codigo is not null)
        {
            consulta = consulta.Where(tarifa => tarifa.Codigo == codigo);
        }

        return consulta.PaginarAsync(paginacion, s_criterios, cancelacion);
    }

    public void Agregar(Tarifa tarifa) => contexto.Tarifas.Add(tarifa);
}
