namespace Bastion.Catalogo.Domain.Catalogo;

/// <summary>Para qué se fija una tarifa: para vender o para comprar.</summary>
/// <remarks>
/// <para>
/// <b>Va con el código, no con el tramo.</b> Los tramos de un código son todos del mismo tipo, y
/// el de un tramo no cambia, como su divisa: una tarifa de venta que pasara a ser de compra
/// convertiría en precios de proveedor los que ya se aplicaron a clientes. Lo sostiene una
/// restricción de exclusión de la base, como el solape, y no solo el caso de uso.
/// </para>
/// <para>
/// Se guarda como <b>texto</b> y no como entero, igual que los demás enumerados del sistema: un
/// enumerado guardado por su valor deja de significar nada en cuanto alguien reordena las
/// constantes.
/// </para>
/// </remarks>
public enum TipoDeTarifa
{
    /// <summary>Lo que se cobra a un cliente. Toda tarifa anterior al 3.3 es de este tipo.</summary>
    Venta = 0,

    /// <summary>Lo que cobra un proveedor: la que propone el precio de una línea de compra.</summary>
    Compra = 1,
}
