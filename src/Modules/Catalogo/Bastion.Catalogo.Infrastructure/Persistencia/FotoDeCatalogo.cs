using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Catalogo.Application;

namespace Bastion.Catalogo.Infrastructure.Persistencia;

/// <summary>
/// La foto del módulo, sobre su propio <see cref="CatalogoDbContext"/> (ADR-0073).
/// </summary>
internal sealed class FotoDeCatalogo(CatalogoDbContext contexto) : IFotoDeCatalogo
{
    /// <inheritdoc />
    public Task<IAsyncDisposable> AbrirAsync(CancellationToken cancelacion) =>
        FotoDeLectura.AbrirAsync(contexto, cancelacion);
}
