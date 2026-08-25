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
}
