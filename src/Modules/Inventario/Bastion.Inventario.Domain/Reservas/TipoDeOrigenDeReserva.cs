namespace Bastion.Inventario.Domain.Reservas;

/// <summary>Qué clase de documento pide la reserva, desde otro módulo (ADR-0059 §12).</summary>
/// <remarks>
/// <b>Solo conoce el pedido de venta</b>, el valor que necesitan los tests del 2.13. La orden de
/// fabricación entra con su fase, y no antes: un valor que nadie pide es una casilla sin productor.
/// </remarks>
public enum TipoDeOrigenDeReserva
{
    /// <summary>Una línea de un pedido de venta, de la fase 4.</summary>
    PedidoDeVenta = 1,
}
