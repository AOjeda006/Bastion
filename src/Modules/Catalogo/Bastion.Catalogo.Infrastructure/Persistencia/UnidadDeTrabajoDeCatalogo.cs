using Bastion.Catalogo.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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

    /// <inheritdoc />
    public async Task<T> EnTransaccionAsync<T>(
        Func<CancellationToken, Task<T>> trabajo, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(trabajo);

        // YA HAY UNA: no se abre otra. Anidar transacciones en EF Core lanza, y aunque no lanzara,
        // confirmar aqui dentro soltaria el cerrojo del articulo que el de fuera todavia necesita.
        if (contexto.Database.CurrentTransaction is not null)
        {
            return await trabajo(cancelacion).ConfigureAwait(false);
        }

        await using IDbContextTransaction transaccion =
            await contexto.Database.BeginTransactionAsync(cancelacion).ConfigureAwait(false);

        T resultado = await trabajo(cancelacion).ConfigureAwait(false);

        // SE CONFIRMA AUNQUE EL TRABAJO HAYA DICHO QUE NO, como en Organizacion: lo que se escribe
        // lo decide el caso de uso llamando a `ConfirmarAsync`, y un camino de fallo no llega a
        // llamarlo. Confirmar sin haber escrito suelta el cerrojo y no graba nada. Lo que SI
        // deshace todo es una excepcion, y el `await using` la revierte al salir.
        await transaccion.CommitAsync(cancelacion).ConfigureAwait(false);

        return resultado;
    }
}
