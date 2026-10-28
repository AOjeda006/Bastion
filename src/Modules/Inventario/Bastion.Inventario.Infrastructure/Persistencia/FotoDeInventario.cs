using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Inventario.Application;

namespace Bastion.Inventario.Infrastructure.Persistencia;

/// <summary>
/// La foto del módulo, sobre su propio <see cref="InventarioDbContext"/> (ADR-0073).
/// </summary>
internal sealed class FotoDeInventario(InventarioDbContext contexto) : IFotoDeInventario
{
    /// <inheritdoc />
    public Task<IAsyncDisposable> AbrirAsync(CancellationToken cancelacion) =>
        FotoDeLectura.AbrirAsync(contexto, cancelacion);
}
