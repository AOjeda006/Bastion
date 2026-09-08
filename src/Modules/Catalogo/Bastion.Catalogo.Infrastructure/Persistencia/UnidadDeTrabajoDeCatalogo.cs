using Bastion.Catalogo.Application;

namespace Bastion.Catalogo.Infrastructure.Persistencia;

/// <summary>
/// La unidad de trabajo del módulo, sobre su propio <see cref="CatalogoDbContext"/>.
/// </summary>
/// <remarks>
/// Una POR MÓDULO, y no una compartida: cada módulo tiene su contexto y su esquema, y una unidad
/// de trabajo común acabaría confirmando en la misma llamada cambios de dos módulos.
/// </remarks>
internal sealed class UnidadDeTrabajoDeCatalogo(CatalogoDbContext contexto) : IUnidadTrabajoDeCatalogo
{
    public Task<int> ConfirmarAsync(CancellationToken cancelacion) =>
        contexto.SaveChangesAsync(cancelacion);
}
