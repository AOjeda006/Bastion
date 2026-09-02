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

    public Task<PaginaDe<Impuesto>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion) =>
        contexto.Impuestos
            .OrderBy(impuesto => impuesto.Codigo)
            .ThenByDescending(impuesto => impuesto.VigenteDesde)
            .ThenBy(impuesto => impuesto.Id)
            .PaginarAsync(paginacion, cancelacion);

    public void Agregar(Impuesto impuesto) => contexto.Impuestos.Add(impuesto);
}
