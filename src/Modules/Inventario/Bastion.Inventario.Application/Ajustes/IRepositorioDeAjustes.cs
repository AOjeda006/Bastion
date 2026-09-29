using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Valoraciones;

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
    /// Bloquea la valoración de cada clave, en orden de clave y creándola si no existe, y la lee ya
    /// bloqueada (ADR-0046 §2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El cerrojo dura hasta el <c>COMMIT</c></b>, así que lo que se lee no se queda viejo: otra
    /// confirmación de la misma clave espera aquí a que esta acabe, y después lee lo que esta dejó.
    /// </para>
    /// <para>
    /// <b>Una clave nueva nace vacía, en la divisa del documento</b>. Si el documento no llega a
    /// confirmarse, la transacción se deshace y la fila no queda.
    /// </para>
    /// </remarks>
    /// <param name="claves">Las claves que el documento va a valorar.</param>
    /// <param name="divisa">La del documento, para las claves que nazcan.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>El saldo bloqueado de cada clave.</returns>
    Task<IReadOnlyDictionary<ClaveDeValoracion, SaldoValorado>> BloquearLasValoracionesAsync(
        IReadOnlyCollection<ClaveDeValoracion> claves,
        string divisa,
        CancellationToken cancelacion);

    /// <summary>
    /// La fila de cada lote y de cada serie, creada si es la primera vez que se nombra
    /// (ADR-0048 §2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Crea con <c>INSERT … ON CONFLICT DO NOTHING</c> y lee después</b>, dentro de la transacción
    /// que confirma. Dos primeras entradas del mismo lote a la vez se ordenan solas en el índice
    /// único: la segunda espera y, cuando la primera confirma, su lectura ve la fila. Si el documento
    /// no llega a confirmarse, el <c>ROLLBACK</c> se lleva los lotes que creó.
    /// </para>
    /// <para>
    /// <b>Va después de la valoración y antes del documento</b>, en el orden de los cerrojos del
    /// ADR-0048 §4, y los crea en el orden en que llegan, que es el de artículo y código.
    /// </para>
    /// </remarks>
    /// <param name="lotes">Los lotes del documento, sin repetir y en orden.</param>
    /// <param name="series">Las series del documento, sin repetir y en orden.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>La fila de cada uno.</returns>
    Task<LotesYSeriesResueltos> ResolverLotesYSeriesAsync(
        IReadOnlyList<CodigoDeUnArticulo> lotes,
        IReadOnlyList<CodigoDeUnArticulo> series,
        CancellationToken cancelacion);

    /// <summary>
    /// Anota en el libro las filas que un documento acaba de generar, y mueve con ellas sus
    /// existencias y su valoración (R3).
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
    /// <b>La valoración se suma aquí también, después de la existencia</b> (ADR-0046 §2), sobre las
    /// filas que <see cref="BloquearLasValoracionesAsync"/> dejó bloqueadas. Si alguna clave no se
    /// bloqueó antes, revienta: sería un defecto de quien llama, no un saldo nuevo.
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
