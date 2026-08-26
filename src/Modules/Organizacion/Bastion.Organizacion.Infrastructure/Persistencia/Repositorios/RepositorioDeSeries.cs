using Bastion.Organizacion.Application.Series;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Domain.Series;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeSeries"/>
internal sealed class RepositorioDeSeries(OrganizacionDbContext contexto) : IRepositorioDeSeries
{
    public Task<Serie?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Series.FirstOrDefaultAsync(serie => serie.Id == id, cancelacion);

    public Task<bool> ExisteElCodigoAsync(
        Guid empresaId,
        Guid ejercicioId,
        string codigo,
        CancellationToken cancelacion) =>
        contexto.Series.AnyAsync(
            serie => serie.EmpresaId == empresaId
                && serie.EjercicioId == ejercicioId
                && serie.Codigo == codigo,
            cancelacion);

    public Task<PaginaDe<Serie>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion) =>
        contexto.Series
            .OrderBy(serie => serie.Codigo)
            .ThenBy(serie => serie.Id)
            .PaginarAsync(paginacion, cancelacion);

    public void Agregar(Serie serie) => contexto.Series.Add(serie);

    public void Eliminar(Serie serie) => contexto.Series.Remove(serie);
}
