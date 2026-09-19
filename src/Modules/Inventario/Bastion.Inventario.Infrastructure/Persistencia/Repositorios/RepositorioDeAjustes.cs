using Bastion.Inventario.Application.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Movimientos;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Inventario.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeAjustes"/>
internal sealed class RepositorioDeAjustes(InventarioDbContext contexto) : IRepositorioDeAjustes
{
    /// <inheritdoc/>
    /// <remarks>
    /// Las líneas vienen con el documento sin pedirlas: la navegación está marcada
    /// <c>AutoInclude</c> en su configuración, porque son su agregado y confirmarlo sin ellas
    /// escribiría un libro vacío.
    /// </remarks>
    public Task<Ajuste?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Ajustes.FirstOrDefaultAsync(ajuste => ajuste.Id == id, cancelacion);

    /// <inheritdoc/>
    public void Agregar(Ajuste ajuste) => contexto.Ajustes.Add(ajuste);

    /// <inheritdoc/>
    public void AgregarMovimientos(IReadOnlyCollection<MovimientoStock> movimientos)
    {
        ArgumentNullException.ThrowIfNull(movimientos);

        contexto.Movimientos.AddRange(movimientos);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// <b>Sin rastrear</b>: esto es la mitad de lectura de la R13 y nadie modifica lo que trae —no
    /// podría: la tabla no admite <c>UPDATE</c>—, así que meter el libro entero de un documento en
    /// el rastreador solo costaría memoria.
    /// </para>
    /// <para>
    /// <b>El orden es el del identificador</b>, y no es arbitrario: es un UUID v7, cuyo prefijo es
    /// el instante de creación, así que ordenar por él devuelve las filas en el orden en que se
    /// escribieron. No se ordena por <c>fecha_de_operacion</c> porque todas las de un mismo
    /// documento la comparten —es la del documento— y dejaría el desempate al azar del plan.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<MovimientoStock>> MovimientosDeAsync(
        TipoDeDocumentoOrigen tipo,
        Guid documentoId,
        CancellationToken cancelacion) =>
        await contexto.Movimientos
            .Where(movimiento => movimiento.DocumentoOrigenTipo == tipo
                && movimiento.DocumentoOrigenId == documentoId)
            .OrderBy(movimiento => movimiento.Id)
            .AsNoTracking()
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
}
