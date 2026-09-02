using Bastion.Organizacion.Application.Unidades;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Domain.Unidades;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeUnidadesDeMedida"/>
internal sealed class RepositorioDeUnidadesDeMedida(OrganizacionDbContext contexto)
    : IRepositorioDeUnidadesDeMedida
{
    public Task<UnidadMedida?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        contexto.UnidadesDeMedida.FirstOrDefaultAsync(unidad => unidad.Id == id, cancelacion);

    public Task<bool> ExisteElCodigoAsync(string codigo, CancellationToken cancelacion) =>
        contexto.UnidadesDeMedida.AnyAsync(unidad => unidad.Codigo == codigo, cancelacion);

    public async Task<bool> ExistenAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(ids);

        int encontradas = await contexto.UnidadesDeMedida
            .CountAsync(unidad => ids.Contains(unidad.Id), cancelacion)
            .ConfigureAwait(false);

        return encontradas == ids.Distinct().Count();
    }

    public Task<PaginaDe<UnidadMedida>> ListarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion) =>
        contexto.UnidadesDeMedida
            .OrderBy(unidad => unidad.Codigo)
            .ThenBy(unidad => unidad.Id)
            .PaginarAsync(paginacion, cancelacion);

    public void Agregar(UnidadMedida unidad) => contexto.UnidadesDeMedida.Add(unidad);
}

/// <inheritdoc cref="IRepositorioDeConversiones"/>
internal sealed class RepositorioDeConversiones(OrganizacionDbContext contexto)
    : IRepositorioDeConversiones
{
    public Task<ConversionUM?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        contexto.ConversionesDeUnidades.FirstOrDefaultAsync(
            conversion => conversion.Id == id, cancelacion);

    public Task<bool> ExisteAsync(
        Guid unidadOrigenId,
        Guid unidadDestinoId,
        CancellationToken cancelacion) =>
        contexto.ConversionesDeUnidades.AnyAsync(
            conversion => conversion.UnidadOrigenId == unidadOrigenId
                && conversion.UnidadDestinoId == unidadDestinoId,
            cancelacion);

    public Task<PaginaDe<ConversionUM>> ListarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion) =>
        contexto.ConversionesDeUnidades
            .OrderBy(conversion => conversion.UnidadOrigenId)
            .ThenBy(conversion => conversion.UnidadDestinoId)
            .ThenBy(conversion => conversion.Id)
            .PaginarAsync(paginacion, cancelacion);

    public void Agregar(ConversionUM conversion) => contexto.ConversionesDeUnidades.Add(conversion);
}
