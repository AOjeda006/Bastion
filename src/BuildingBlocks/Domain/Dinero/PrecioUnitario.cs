namespace Bastion.BuildingBlocks.Domain.Dinero;

/// <summary>
/// Precio por unidad, en la escala de precio unitario de R6 (<c>numeric(18,6)</c>).
/// </summary>
/// <remarks>
/// Es un tipo distinto de <see cref="Importe"/> y no un importe con más decimales, porque las
/// dos escalas de R6 no son intercambiables: un precio unitario no es dinero que se pueda sumar
/// a una factura, y multiplicarlo por una cantidad no devuelve otro precio unitario. Que sean
/// tipos distintos hace que el compilador impida confundirlos.
/// </remarks>
public sealed record PrecioUnitario
{
    /// <summary>Decimales de la escala de precio unitario de R6.</summary>
    public const int Decimales = 6;

    private PrecioUnitario(decimal cantidad, string divisa) => (Cantidad, Divisa) = (cantidad, divisa);

    /// <summary>Cantidad, con <see cref="Decimales"/> decimales como mucho.</summary>
    public decimal Cantidad { get; }

    /// <summary>Divisa, como código ISO 4217 en mayúsculas.</summary>
    public string Divisa { get; }

    /// <summary>Crea un precio unitario reduciéndolo a la escala de precio unitario.</summary>
    /// <exception cref="ArgumentException">La divisa no es un código ISO 4217.</exception>
    public static PrecioUnitario De(decimal cantidad, string divisa) =>
        new(Math.Round(cantidad, Decimales, MidpointRounding.AwayFromZero), CatalogoDeDivisas.Normalizar(divisa));

    /// <summary>Importe de multiplicar este precio por una cantidad.</summary>
    /// <remarks>
    /// ESTE es el único punto donde se baja de la escala 6 a la escala 4, y es un solo redondeo
    /// sobre el producto exacto. Devolver <see cref="Importe"/> y no <c>decimal</c> es lo que
    /// impide que el resultado se siga tratando como si tuviera seis decimales.
    /// </remarks>
    public Importe Por(decimal cantidad) => Importe.De(Cantidad * cantidad, Divisa);
}
