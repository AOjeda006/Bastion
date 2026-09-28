using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Movimientos;

namespace Bastion.Inventario.Application.Ajustes;

/// <summary>Los ajustes y las filas del libro que escriben, que son dos cosas distintas.</summary>
/// <remarks>
/// <b>Un solo repositorio para dos agregados, y a propósito.</b> Confirmar escribe la cabecera del
/// documento y sus filas del libro en la <b>misma transacción</b> —es la mitad de la R12 que este
/// ítem dobla a sabiendas, con su motivo en <c>docs/PLAN.md</c>—, así que separarlos en dos
/// repositorios con dos unidades de trabajo sugeriría que se pueden guardar por separado, que es
/// exactamente lo que no puede pasar.
/// </remarks>
public interface IRepositorioDeAjustes
{
    /// <summary>Trae un ajuste con sus líneas.</summary>
    /// <param name="id">Identificador del ajuste.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>El ajuste, o <see langword="null"/> si no hay ninguno con ese identificador.</returns>
    Task<Ajuste?> ObtenerAsync(Guid id, CancellationToken cancelacion);

    /// <summary>Apunta un ajuste nuevo.</summary>
    /// <param name="ajuste">El documento.</param>
    void Agregar(Ajuste ajuste);

    /// <summary>
    /// Anota en el libro las filas que un documento acaba de generar, y mueve con ellas sus
    /// existencias (R3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Las dos cosas en una sola llamada, y a propósito.</b> La existencia es la suma del libro:
    /// una firma que dejara añadir filas sin mover la suma dejaría escribir la discrepancia que la
    /// R3 prohíbe, y una que dejara mover la suma sin filas, la contraria. Por eso no hay ninguna
    /// de las dos por separado.
    /// </para>
    /// <para>
    /// <b>Y es asíncrona aunque las filas se guarden al confirmar la unidad de trabajo</b>: la
    /// existencia se mueve aquí, con una sentencia contra el motor, dentro de la transacción que ya
    /// está abierta. Si esa transacción no llega a confirmarse, no queda ni lo uno ni lo otro.
    /// </para>
    /// <para>
    /// <b>Antes de mover la existencia guarda lo que el documento tenga pendiente</b> (ítem 2.8):
    /// quien confirma o anula un documento que otro acaba de cambiar tiene que chocar con la guarda
    /// del documento, no con el stock que el otro ya movió.
    /// </para>
    /// <para>
    /// Solo se añaden. No hay ningún método para modificarlas ni para quitarlas, y no es un olvido:
    /// la tabla las rechazaría igualmente en el motor, y una firma que no existe no hace falta
    /// explicarla dos veces.
    /// </para>
    /// </remarks>
    /// <param name="movimientos">Las filas.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>Una tarea que acaba cuando la existencia se ha movido.</returns>
    Task AnotarEnElLibroAsync(
        IReadOnlyCollection<MovimientoStock> movimientos, CancellationToken cancelacion);

    /// <summary>Las filas del libro que escribió un documento (R13, la ida).</summary>
    /// <param name="tipo">Qué clase de documento.</param>
    /// <param name="documentoId">Cuál.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>Sus movimientos, en el orden en que se escribieron.</returns>
    Task<IReadOnlyList<MovimientoStock>> MovimientosDeAsync(
        TipoDeDocumentoOrigen tipo,
        Guid documentoId,
        CancellationToken cancelacion);
}
