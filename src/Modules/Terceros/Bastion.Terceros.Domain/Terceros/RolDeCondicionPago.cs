namespace Bastion.Terceros.Domain.Terceros;

/// <summary>
/// De qué cara de la relación es una condición de pago.
/// </summary>
/// <remarks>
/// Dos valores y no un booleano: <c>EsDeCliente = false</c> obliga a saber qué es lo contrario, y
/// lo contrario de cliente en esta ficha no es «no cliente», es «proveedor». Además el enumerado se
/// guarda como texto, así que la columna se lee sin traducirla.
/// </remarks>
public enum RolDeCondicionPago
{
    /// <summary>Lo que se le concede cobrando: cuándo paga él.</summary>
    Cliente,

    /// <summary>Lo que se acepta pagando: cuándo se le paga a él.</summary>
    Proveedor,
}
