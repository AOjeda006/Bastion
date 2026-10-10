using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Reservas;

namespace Bastion.Inventario.Application.Reservas;

/// <summary>La línea del documento de otro módulo que pide la reserva, y por la que se la reconoce.</summary>
/// <remarks>
/// <b>Es la clave de la reserva para quien la pide</b>: el origen es único en todos los estados
/// (ADR-0059 §1, precisión 6), así que consumir y liberar la buscan por él, y un reintento de
/// reservar la encuentra.
/// </remarks>
/// <param name="Tipo">La clase del documento: en el 2.13, el pedido de venta.</param>
/// <param name="Id">El documento, en su módulo.</param>
/// <param name="Linea">Su línea, desde uno.</param>
public sealed record OrigenDeLaReserva(TipoDeOrigenDeReserva Tipo, Guid Id, int Linea);

/// <summary>Lo que hace falta para apartar una cantidad de un artículo en un almacén.</summary>
/// <remarks>
/// <b>No lleva empresa ni unidad</b>: la empresa sale del <i>claim</i> (R8), y la unidad es la base
/// del artículo, que se pregunta a Catálogo y se copia en la reserva (ADR-0059 §12).
/// </remarks>
/// <param name="Origen">La línea que la pide.</param>
/// <param name="ArticuloId">El artículo.</param>
/// <param name="AlmacenId">El almacén.</param>
/// <param name="Cantidad">Cuánto, en la unidad base del artículo.</param>
/// <param name="CaducaEl">Desde cuándo deja de apartar, o <c>null</c> si no caduca.</param>
public sealed record ReservarDto(
    OrigenDeLaReserva Origen,
    Guid ArticuloId,
    Guid AlmacenId,
    decimal Cantidad,
    DateTimeOffset? CaducaEl = null);

/// <summary>Lo que hace falta para sacar por el libro una parte o todo lo que una reserva aparta.</summary>
/// <remarks>
/// <b>Recibe el origen y no el identificador de la reserva</b>: el albarán sabe de qué línea de
/// pedido sale (ADR-0059 §6). El artículo y el almacén son los de la reserva.
/// </remarks>
/// <param name="Origen">La línea del pedido cuya reserva se consume.</param>
/// <param name="DocumentoTipo">La clase del documento que sale: en el 2.13, el albarán.</param>
/// <param name="DocumentoId">El documento que sale, en su módulo.</param>
/// <param name="FechaDeOperacion">El día de la salida. No futuro, y en un ejercicio abierto.</param>
/// <param name="Lineas">Lo que sale, por hueco.</param>
public sealed record ConsumirReservaDto(
    OrigenDeLaReserva Origen,
    TipoDeDocumentoOrigen DocumentoTipo,
    Guid DocumentoId,
    DateOnly FechaDeOperacion,
    IReadOnlyList<LineaDeConsumoDto> Lineas);

/// <summary>Lo que sale de un hueco al consumir una reserva.</summary>
/// <param name="UbicacionId">El hueco del almacén de la reserva.</param>
/// <param name="Cantidad">Cuánto, en la unidad base de la reserva. Positiva.</param>
/// <param name="CodigoDeLote">El lote, si el artículo va por lote.</param>
/// <param name="NumeroDeSerie">El número de serie, si va por serie: una unidad base.</param>
public sealed record LineaDeConsumoDto(
    Guid UbicacionId,
    decimal Cantidad,
    string? CodigoDeLote = null,
    string? NumeroDeSerie = null);

/// <summary>Lo que hace falta para soltar a mano lo que le queda a una reserva.</summary>
/// <param name="Origen">La línea del pedido cuya reserva se libera.</param>
/// <param name="Motivo">Por qué, escrito por quien la libera.</param>
public sealed record LiberarReservaDto(OrigenDeLaReserva Origen, string Motivo);

/// <summary>Una reserva, como se lee en un instante.</summary>
/// <remarks>
/// <b>El estado es el de ese instante, no el guardado</b> (ADR-0059 §4): una reserva guardada activa
/// que ya caducó se enseña liberada, con la causa y la fecha de su caducidad, aunque nadie la haya
/// escrito todavía.
/// </remarks>
/// <param name="Id">Identificador.</param>
/// <param name="Origen">La línea que la pidió.</param>
/// <param name="ArticuloId">El artículo que aparta.</param>
/// <param name="AlmacenId">El almacén del que lo aparta.</param>
/// <param name="Cantidad">Lo que se pidió apartar.</param>
/// <param name="Consumida">Lo que ya ha salido por el libro.</param>
/// <param name="Pendiente">Lo que falta por salir de lo que se pidió.</param>
/// <param name="UnidadBaseId">La unidad base del artículo al reservar.</param>
/// <param name="CaducaEl">Desde cuándo deja de apartar, o <c>null</c>.</param>
/// <param name="Estado">El estado en el instante de la lectura.</param>
/// <param name="Causa">Por qué se liberó, si se lee liberada.</param>
/// <param name="Motivo">El motivo escrito de una liberación a mano.</param>
/// <param name="LiberadaEl">Cuándo se liberó, si se lee liberada.</param>
public sealed record ReservaDto(
    Guid Id,
    OrigenDeLaReserva Origen,
    Guid ArticuloId,
    Guid AlmacenId,
    decimal Cantidad,
    decimal Consumida,
    decimal Pendiente,
    Guid UnidadBaseId,
    DateTimeOffset? CaducaEl,
    EstadoDeReserva Estado,
    CausaDeLiberacion? Causa,
    string? Motivo,
    DateTimeOffset? LiberadaEl);
