using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Terceros.Application;

namespace Bastion.Terceros.Infrastructure.Persistencia;

/// <summary>
/// La foto del módulo, sobre su propio <see cref="TercerosDbContext"/> (ADR-0073).
/// </summary>
internal sealed class FotoDeTerceros(TercerosDbContext contexto) : IFotoDeTerceros
{
    /// <inheritdoc />
    public Task<IAsyncDisposable> AbrirAsync(CancellationToken cancelacion) =>
        FotoDeLectura.AbrirAsync(contexto, cancelacion);
}
