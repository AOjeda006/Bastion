using Bastion.BuildingBlocks.Domain.Entidades;
using Bastion.Inventario.Domain.Movimientos;

namespace Bastion.Inventario.Domain.Reservas;

/// <summary>
/// Lo que un documento de salida sacó de una reserva: cuánto, con qué documento y en qué fecha
/// (ADR-0059 §6).
/// </summary>
/// <remarks>
/// <para>
/// <b>Es hijo de solo inserción, sin testigo</b>, como la línea del ajuste (ADR-0059 §10): nace al
/// consumir, en la misma transacción que las filas del libro que lo acompañan, y no cambia nunca.
/// </para>
/// <para>
/// <b>Es la otra punta de la doble flecha de un albarán</b> (ADR-0059 §8). El albarán vive en Ventas
/// y Inventario no puede leer su tabla, así que la flecha de una fila <c>Albaran</c> se cierra contra
/// este consumo: el mismo documento, de una reserva de su mismo artículo y almacén.
/// </para>
/// </remarks>
public sealed class ConsumoDeReserva : EntidadBase
{
    private ConsumoDeReserva(
        Guid id,
        Guid reservaId,
        TipoDeDocumentoOrigen documentoTipo,
        Guid documentoId,
        DateOnly fechaDeOperacion,
        decimal cantidad,
        DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        ReservaId = reservaId;
        DocumentoTipo = documentoTipo;
        DocumentoId = documentoId;
        FechaDeOperacion = fechaDeOperacion;
        Cantidad = cantidad;
    }

    private ConsumoDeReserva()
    {
    }

    /// <summary>Identificador del consumo.</summary>
    public Guid Id { get; private set; }

    /// <summary>La reserva que consume.</summary>
    public Guid ReservaId { get; private set; }

    /// <summary>La clase del documento que sale: en el 2.13, el albarán.</summary>
    public TipoDeDocumentoOrigen DocumentoTipo { get; private set; }

    /// <summary>
    /// El documento que sale, el mismo que llevan sus filas en <c>documento_origen_id</c>. Una
    /// reserva lo tiene una sola vez.
    /// </summary>
    public Guid DocumentoId { get; private set; }

    /// <summary>El día de la salida, el de sus filas del libro.</summary>
    public DateOnly FechaDeOperacion { get; private set; }

    /// <summary>Lo que sacó, en la unidad base de la reserva: la suma de sus líneas.</summary>
    public decimal Cantidad { get; private set; }

    /// <summary>Anota un consumo. Solo lo llama la reserva, al consumir.</summary>
    /// <param name="reservaId">La reserva.</param>
    /// <param name="documentoTipo">La clase del documento que sale.</param>
    /// <param name="documentoId">El documento que sale.</param>
    /// <param name="fechaDeOperacion">El día de la salida.</param>
    /// <param name="cantidad">Lo que saca, positivo.</param>
    /// <param name="momento">Ahora.</param>
    /// <returns>El consumo.</returns>
    internal static ConsumoDeReserva Anotar(
        Guid reservaId,
        TipoDeDocumentoOrigen documentoTipo,
        Guid documentoId,
        DateOnly fechaDeOperacion,
        decimal cantidad,
        DateTimeOffset momento) =>
        new(Guid.CreateVersion7(), reservaId, documentoTipo, documentoId, fechaDeOperacion, cantidad, momento);
}
