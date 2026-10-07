using Bastion.Inventario.Application;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bastion.Inventario.Infrastructure.Persistencia;

/// <summary>
/// La unidad de trabajo del módulo, sobre su propio <see cref="InventarioDbContext"/>.
/// </summary>
/// <remarks>
/// Una POR MÓDULO, como en los otros cuatro. Aquí además carga con la transacción que dobla la
/// R12 a sabiendas: la confirmación de un ajuste escribe la cabecera del documento y sus filas del
/// libro en la misma llamada, y que sea una sola es lo que impide un ajuste confirmado con el
/// stock sin mover (rompería la R3).
/// </remarks>
/// <param name="contexto">El contexto del módulo.</param>
internal sealed class UnidadDeTrabajoDeInventario(InventarioDbContext contexto)
    : IUnidadTrabajoDeInventario
{
    public Task<int> ConfirmarAsync(CancellationToken cancelacion) =>
        contexto.SaveChangesAsync(cancelacion);

    /// <inheritdoc />
    public async Task<T> EnTransaccionAsync<T>(
        Func<CancellationToken, Task<T>> trabajo, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(trabajo);

        // YA HAY UNA: no se abre otra. Anidar transacciones en EF Core lanza, y aunque no lanzara,
        // confirmar aquí dentro soltaría el cerrojo del recuento que el de fuera todavía necesita.
        // Es el caso de añadir una línea con `Idempotency-Key`: la abrió el filtro.
        if (contexto.Database.CurrentTransaction is not null)
        {
            return await trabajo(cancelacion).ConfigureAwait(false);
        }

        await using IDbContextTransaction transaccion =
            await contexto.Database.BeginTransactionAsync(cancelacion).ConfigureAwait(false);

        T resultado = await trabajo(cancelacion).ConfigureAwait(false);

        // SE CONFIRMA AUNQUE EL TRABAJO HAYA DICHO QUE NO, como en Catálogo: lo que se escribe lo
        // decide el caso de uso llamando a `ConfirmarAsync`, y un camino de fallo no llega a
        // llamarlo. Confirmar sin haber escrito suelta el cerrojo y no graba nada. Lo que SÍ
        // deshace todo es una excepción, y el `await using` la revierte al salir.
        await transaccion.CommitAsync(cancelacion).ConfigureAwait(false);

        return resultado;
    }
}
