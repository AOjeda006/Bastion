namespace Bastion.BuildingBlocks.Domain.Dinero;

/// <summary>
/// Cantidad de dinero con divisa (R6). Inmutable y con igualdad por valor.
/// </summary>
/// <remarks>
/// <para>
/// La cantidad es siempre <see cref="decimal"/>, nunca coma flotante: <c>double</c> no puede
/// representar 0,1 y un céntimo perdido en una suma es un asiento que no cuadra.
/// </para>
/// <para>
/// Vive en la escala de importe de R6 (<c>numeric(18,4)</c>), que el tipo impone al construirse
/// para que la reducción ocurra aquí y no en el motor de la base de datos, donde el modo de
/// redondeo ya no sería el nuestro. Para precios unitarios, que van en <c>numeric(18,6)</c>,
/// está <see cref="PrecioUnitario"/>.
/// </para>
/// <para>
/// El modo de redondeo es <see cref="MidpointRounding.AwayFromZero"/> y está escrito en todas
/// las llamadas. NO es el de .NET, que por omisión es <see cref="MidpointRounding.ToEven"/>
/// (el llamado redondeo del banquero): 0,125 daría 0,12 en vez de 0,13.
/// </para>
/// </remarks>
public sealed record Importe
{
    /// <summary>Decimales de la escala de importe de R6.</summary>
    public const int Decimales = 4;

    private Importe(decimal cantidad, string divisa) => (Cantidad, Divisa) = (cantidad, divisa);

    /// <summary>Cantidad, con <see cref="Decimales"/> decimales como mucho.</summary>
    public decimal Cantidad { get; }

    /// <summary>Divisa, como código ISO 4217 en mayúsculas.</summary>
    public string Divisa { get; }

    /// <summary>Crea un importe reduciéndolo a la escala de importe.</summary>
    /// <exception cref="ArgumentException">La divisa no es un código ISO 4217.</exception>
    public static Importe De(decimal cantidad, string divisa) =>
        new(Math.Round(cantidad, Decimales, MidpointRounding.AwayFromZero), CatalogoDeDivisas.Normalizar(divisa));

    /// <summary>Importe nulo en la divisa indicada.</summary>
    public static Importe Cero(string divisa) => De(0m, divisa);

    /// <summary>Suma dos importes de la misma divisa, sin redondear.</summary>
    /// <exception cref="InvalidOperationException">Los importes tienen divisas distintas.</exception>
    public static Importe operator +(Importe izquierdo, Importe derecho)
    {
        ArgumentNullException.ThrowIfNull(izquierdo);
        ArgumentNullException.ThrowIfNull(derecho);

        // Operar entre divisas distintas LANZA: no se convierte ni se deja pasar. Convertir
        // exige un tipo de cambio con su fecha, que este objeto de valor no tiene ni debe
        // adivinar; dejarlo pasar sumaría euros con dólares como si fueran lo mismo.
        if (!string.Equals(izquierdo.Divisa, derecho.Divisa, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"No se pueden operar importes en {izquierdo.Divisa} y en {derecho.Divisa}: " +
                "haría falta un tipo de cambio con fecha.");
        }

        // Sin redondear a propósito: los dos sumandos ya están en escala 4, luego su suma
        // también. Redondear en cada acumulación repartiría por todas partes el error que R6
        // quiere concentrar en un solo punto.
        return new Importe(izquierdo.Cantidad + derecho.Cantidad, izquierdo.Divisa);
    }

    /// <summary>Suma dos importes de la misma divisa, sin redondear.</summary>
    /// <exception cref="InvalidOperationException">Los importes tienen divisas distintas.</exception>
    public static Importe Sumar(Importe izquierdo, Importe derecho) => izquierdo + derecho;

    /// <summary>
    /// Cuota de aplicar un tipo impositivo a este importe tomado como base imponible,
    /// redondeada UNA sola vez a la unidad mínima de la divisa.
    /// </summary>
    /// <remarks>
    /// Esta es la primitiva de la regla de R6: el redondeo se aplica por base imponible y tipo
    /// impositivo, no línea a línea ni al total. Redondea el producto EXACTO, sin pasar antes
    /// por la escala de importe, porque redondear dos veces no da lo mismo que redondear una.
    /// Quien aplique la regla debe llamar a esto una vez por par (base, tipo), nunca por línea.
    /// </remarks>
    /// <exception cref="NotSupportedException">No se conoce la unidad mínima de la divisa.</exception>
    public Importe Cuota(decimal tipo) =>
        new(Math.Round(Cantidad * tipo, CatalogoDeDivisas.UnidadMinima(Divisa), MidpointRounding.AwayFromZero), Divisa);
}
