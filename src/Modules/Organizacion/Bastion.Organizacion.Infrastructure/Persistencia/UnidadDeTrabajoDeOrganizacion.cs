using Bastion.Organizacion.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bastion.Organizacion.Infrastructure.Persistencia;

/// <summary>
/// La unidad de trabajo del módulo, sobre su propio <see cref="OrganizacionDbContext"/>.
/// </summary>
/// <remarks>
/// Una POR MÓDULO, y no una compartida: cada módulo tiene su contexto y su esquema, y una unidad
/// de trabajo común acabaría confirmando en la misma llamada cambios de dos módulos, que es
/// exactamente la frontera que el §4 no quiere que se cruce sin darse cuenta.
/// </remarks>
internal sealed class UnidadDeTrabajoDeOrganizacion(OrganizacionDbContext contexto) : IUnidadTrabajoDeOrganizacion
{
    public Task<int> ConfirmarAsync(CancellationToken cancelacion) =>
        contexto.SaveChangesAsync(cancelacion);

    /// <inheritdoc />
    public async Task<T> EnTransaccionAsync<T>(
        Func<CancellationToken, Task<T>> trabajo, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(trabajo);

        // YA HAY UNA: no se abre otra. Anidar transacciones en EF Core lanza, y aunque no lanzara,
        // confirmar aqui dentro soltaria los cerrojos que el de fuera todavia necesita. Quien la
        // abrio es quien la cierra.
        if (contexto.Database.CurrentTransaction is not null)
        {
            return await trabajo(cancelacion).ConfigureAwait(false);
        }

        await using IDbContextTransaction transaccion =
            await contexto.Database.BeginTransactionAsync(cancelacion).ConfigureAwait(false);

        T resultado = await trabajo(cancelacion).ConfigureAwait(false);

        // SE CONFIRMA AUNQUE EL TRABAJO HAYA DICHO QUE NO, y no es un descuido: lo que se escribe
        // lo decide el caso de uso llamando a `ConfirmarAsync`, y un camino de fallo no llega a
        // llamarlo. Confirmar una transaccion que no ha escrito nada no graba nada y suelta los
        // cerrojos, que es justo lo que hay que hacer con una negativa. Lo que SI deshace todo es
        // una excepcion: el `await using` la revierte al salir.
        await transaccion.CommitAsync(cancelacion).ConfigureAwait(false);

        return resultado;
    }
}
