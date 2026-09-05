using Bastion.Terceros.Application;

namespace Bastion.Terceros.Infrastructure.Persistencia;

/// <summary>
/// La unidad de trabajo del módulo, sobre su propio <see cref="TercerosDbContext"/>.
/// </summary>
/// <remarks>
/// Una POR MÓDULO, y no una compartida: cada módulo tiene su contexto y su esquema, y una unidad
/// de trabajo común acabaría confirmando en la misma llamada cambios de dos módulos.
/// </remarks>
internal sealed class UnidadDeTrabajoDeTerceros(TercerosDbContext contexto) : IUnidadTrabajoDeTerceros
{
    public Task<int> ConfirmarAsync(CancellationToken cancelacion) =>
        contexto.SaveChangesAsync(cancelacion);
}
