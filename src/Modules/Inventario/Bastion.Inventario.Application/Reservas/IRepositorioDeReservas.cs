using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Reservas;
using Bastion.Inventario.Domain.Valoraciones;

namespace Bastion.Inventario.Application.Reservas;

/// <summary>
/// Lo que los casos de uso de la reserva necesitan de la persistencia: el cerrojo de la clave, la
/// reserva de un origen, lo reservado de una clave, el físico de sus huecos y escribir la salida.
/// </summary>
/// <remarks>
/// <b>Toda escritura sobre las reservas de una clave tiene antes la valoración de esa clave</b>
/// (ADR-0059 §2). Por eso las lecturas que deciden —la reserva del origen, las caducadas, lo
/// reservado y el físico— van sin cerrojo propio y después del de la valoración: con él tomado, no
/// pueden cambiar mientras se leen.
/// </remarks>
public interface IRepositorioDeReservas
{
    /// <summary>
    /// Bloquea la valoración de la clave con <c>FOR NO KEY UPDATE</c>, sin crearla, y devuelve su
    /// cantidad: el físico de la clave en el almacén.
    /// </summary>
    /// <remarks>
    /// <b>No la crea</b>: una clave sin fila tiene el físico a cero, y reservar ahí es un <c>422</c>
    /// que no escribe nada (ADR-0059 §2).
    /// </remarks>
    /// <param name="clave">El artículo y el almacén.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>La cantidad de la valoración, o <see langword="null"/> si la clave no tiene fila.</returns>
    Task<decimal?> BloquearLaValoracionAsync(ClaveDeValoracion clave, CancellationToken cancelacion);

    /// <summary>
    /// Bloquea la valoración de cada clave, creándola vacía si no existe, y la lee bloqueada: el
    /// cerrojo de una salida.
    /// </summary>
    /// <param name="claves">Las claves.</param>
    /// <param name="divisa">La de la empresa, para las claves que nazcan.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>El saldo de cada clave, ya bloqueado hasta el <c>COMMIT</c>.</returns>
    Task<IReadOnlyDictionary<ClaveDeValoracion, SaldoValorado>> BloquearLasValoracionesAsync(
        IReadOnlyCollection<ClaveDeValoracion> claves,
        string divisa,
        CancellationToken cancelacion);

    /// <summary>La clave de la reserva de un origen, sin cerrojo, para saber qué bloquear.</summary>
    /// <param name="tipo">La clase del documento.</param>
    /// <param name="id">El documento.</param>
    /// <param name="linea">Su línea.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>La clave, o <see langword="null"/> si el origen no tiene reserva en la empresa.</returns>
    Task<ClaveDeValoracion?> ClaveDelOrigenAsync(
        TipoDeOrigenDeReserva tipo, Guid id, int linea, CancellationToken cancelacion);

    /// <summary>La reserva de un origen, con sus consumos y rastreada, para cambiarla.</summary>
    /// <param name="tipo">La clase del documento.</param>
    /// <param name="id">El documento.</param>
    /// <param name="linea">Su línea.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>La reserva, o <see langword="null"/>.</returns>
    Task<Reserva?> ObtenerPorOrigenAsync(
        TipoDeOrigenDeReserva tipo, Guid id, int linea, CancellationToken cancelacion);

    /// <summary>
    /// Las reservas de la clave guardadas activas cuya caducidad no es posterior a ahora, rastreadas,
    /// para escribirlas liberadas (ADR-0059 §4).
    /// </summary>
    /// <param name="clave">La clave, ya bloqueada.</param>
    /// <param name="ahora">El instante, el del <c>TimeProvider</c>.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>Las reservas.</returns>
    Task<IReadOnlyList<Reserva>> CaducadasDeLaClaveAsync(
        ClaveDeValoracion clave, DateTimeOffset ahora, CancellationToken cancelacion);

    /// <summary>
    /// Lo reservado de cada clave en un instante: la suma de lo pendiente de sus reservas activas y
    /// vigentes, leída en la base (ADR-0059 §3).
    /// </summary>
    /// <remarks>
    /// <b>Lee lo confirmado, no el rastreador</b>: una caducada que este caso de uso acaba de liberar
    /// sin guardar no cuenta igual, porque su caducidad no es posterior a <paramref name="ahora"/>.
    /// </remarks>
    /// <param name="claves">Las claves, ya bloqueadas.</param>
    /// <param name="ahora">El instante, el del <c>TimeProvider</c>.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>Lo reservado de cada clave pedida; cero si no tiene reservas.</returns>
    Task<IReadOnlyDictionary<ClaveDeValoracion, decimal>> ReservadoDeAsync(
        IReadOnlyCollection<ClaveDeValoracion> claves, DateTimeOffset ahora, CancellationToken cancelacion);

    /// <summary>Añade una reserva nueva, que se escribe al confirmar la unidad de trabajo.</summary>
    /// <param name="reserva">La reserva.</param>
    void Agregar(Reserva reserva);

    /// <summary>
    /// Busca la fila de cada lote y de cada serie, <b>sin crear</b> los que falten (ADR-0059 §6).
    /// </summary>
    /// <remarks>Un lote que no existe no tiene nada que sacar: no vuelve, y su físico es cero.</remarks>
    /// <param name="lotes">Los lotes que nombra el consumo.</param>
    /// <param name="series">Los números de serie que nombra el consumo.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>Los que existen, con su fila.</returns>
    Task<LotesYSeriesResueltos> BuscarLotesYSeriesAsync(
        IReadOnlyList<CodigoDeUnArticulo> lotes,
        IReadOnlyList<CodigoDeUnArticulo> series,
        CancellationToken cancelacion);

    /// <summary>El físico de cada hueco de una clave, con su lote o su serie.</summary>
    /// <param name="clave">La clave, ya bloqueada.</param>
    /// <param name="huecos">Los huecos por los que se pregunta.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>El físico de cada hueco pedido que tiene fila; los demás no vuelven.</returns>
    Task<IReadOnlyDictionary<HuecoDeExistencias, decimal>> FisicoDeLosHuecosAsync(
        ClaveDeValoracion clave, IReadOnlyCollection<HuecoDeExistencias> huecos, CancellationToken cancelacion);

    /// <summary>
    /// Escribe la salida de un consumo: guarda la reserva y su consumo, y mueve las existencias y la
    /// valoración, como el ajuste, dejando las filas del libro para la unidad de trabajo.
    /// </summary>
    /// <param name="movimientos">Las filas del libro del consumo.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>Una tarea que acaba cuando las sentencias han corrido.</returns>
    Task AnotarEnElLibroAsync(IReadOnlyCollection<MovimientoStock> movimientos, CancellationToken cancelacion);
}

/// <summary>Un hueco de una clave: la ubicación, con su lote o su número de serie.</summary>
/// <param name="UbicacionId">La ubicación del almacén de la clave.</param>
/// <param name="LoteId">La fila del lote, o <see langword="null"/>.</param>
/// <param name="NumeroDeSerieId">La fila del número de serie, o <see langword="null"/>.</param>
public readonly record struct HuecoDeExistencias(Guid UbicacionId, Guid? LoteId, Guid? NumeroDeSerieId);
