namespace Bastion.Organizacion.Domain.Series;

/// <summary>Clase de documento que numera una serie.</summary>
/// <remarks>Se persiste como texto, por la misma razón que el régimen de IVA.</remarks>
public enum TipoDeDocumento
{
    /// <summary>Factura emitida.</summary>
    FacturaEmitida,

    /// <summary>Factura rectificativa (art. 15 del RD 1619/2012: serie específica).</summary>
    FacturaRectificativa,

    /// <summary>Pedido de venta.</summary>
    PedidoDeVenta,

    /// <summary>Albarán de venta.</summary>
    AlbaranDeVenta,

    /// <summary>Pedido de compra.</summary>
    PedidoDeCompra,

    /// <summary>Albarán de compra.</summary>
    AlbaranDeCompra,

    // LOS TRES DE INVENTARIO, del ítem 2.4, y entran los tres a la vez aunque hoy solo numere uno.
    // No es adelantarse: son los tres documentos de la fase 2 -el ajuste (2.3), la transferencia
    // (2.11) y el recuento (2.12)-, y lo que decide una serie es QUÉ numera. Dejar los otros dos
    // fuera obligaría a migrar el enumerado dos veces más para no ganar nada, porque aquí un valor
    // sin usar no es un camino muerto: es una serie que nadie ha creado todavía.

    /// <summary>Ajuste de inventario: la corrección de existencias del ítem 2.3.</summary>
    AjusteDeInventario,

    /// <summary>Transferencia entre almacenes (ítem 2.11).</summary>
    TransferenciaDeInventario,

    /// <summary>Recuento de existencias (ítem 2.12).</summary>
    RecuentoDeInventario,
}
