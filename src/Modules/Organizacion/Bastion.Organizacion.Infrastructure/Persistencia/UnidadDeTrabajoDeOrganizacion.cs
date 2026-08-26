using Bastion.BuildingBlocks.Application;

namespace Bastion.Organizacion.Infrastructure.Persistencia;

/// <summary>
/// La unidad de trabajo del módulo, sobre su propio <see cref="OrganizacionDbContext"/>.
/// </summary>
/// <remarks>
/// Una POR MÓDULO, y no una compartida: cada módulo tiene su contexto y su esquema, y una unidad
/// de trabajo común acabaría confirmando en la misma llamada cambios de dos módulos, que es
/// exactamente la frontera que el §4 no quiere que se cruce sin darse cuenta.
/// </remarks>
internal sealed class UnidadDeTrabajoDeOrganizacion(OrganizacionDbContext contexto) : IUnidadTrabajo
{
    public Task<int> ConfirmarAsync(CancellationToken cancelacion) =>
        contexto.SaveChangesAsync(cancelacion);
}
