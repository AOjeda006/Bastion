using System.Text;

namespace Bastion.BuildingBlocks.Domain.Direcciones;

/// <summary>
/// Dirección postal en campos estructurados (R17). Nunca dos líneas de texto libre.
/// </summary>
/// <remarks>
/// <para>
/// No es una preferencia estética. El <em>SEPA Credit Transfer Rulebook</em> retira el formato
/// de dirección NO estructurada el <b>15 de noviembre de 2026</b>: a partir de ahí una
/// transferencia con la dirección en texto libre no se cursa. Y partir texto libre en campos
/// a posteriori es trabajo manual sobre datos sucios que no se automatiza con fiabilidad.
/// </para>
/// <para>
/// Vive en el bloque común y no en un módulo porque la necesitan Organización (empresa y
/// almacén), Terceros y Facturación. Una dirección duplicada por módulo sería tres reglas de
/// validación que se separan en cuanto una se toque.
/// </para>
/// <para>
/// La representación en una línea es <see cref="EnUnaLinea"/>, una función que COMPONE. No hay
/// columna que la almacene: un campo derivado guardado es un campo que se queda obsoleto.
/// </para>
/// </remarks>
public sealed record Direccion
{
    /// <summary>Tope de la calle: <c>StreetName</c> del rulebook de SEPA.</summary>
    public const int LongitudMaximaDeCalle = 70;

    /// <summary>Tope del número: <c>BuildingNumber</c> del rulebook de SEPA.</summary>
    public const int LongitudMaximaDeNumero = 16;

    /// <summary>Tope del código postal: <c>PostCode</c> del rulebook de SEPA.</summary>
    public const int LongitudMaximaDeCodigoPostal = 16;

    /// <summary>Tope de la población: <c>TownName</c> del rulebook de SEPA.</summary>
    public const int LongitudMaximaDePoblacion = 35;

    /// <summary>Tope de la subdivisión: <c>CountrySubDivision</c> del rulebook de SEPA.</summary>
    public const int LongitudMaximaDeSubdivision = 35;

    /// <summary>Longitud del país: ISO 3166-1 alfa-2, ni una letra más ni una menos.</summary>
    public const int LongitudDelPais = 2;

    private Direccion(
        string calle,
        string? numero,
        string codigoPostal,
        string poblacion,
        string? subdivision,
        string pais)
    {
        Calle = calle;
        Numero = numero;
        CodigoPostal = codigoPostal;
        Poblacion = poblacion;
        Subdivision = subdivision;
        Pais = pais;
    }

    /// <summary>Nombre de la vía, sin el número.</summary>
    public string Calle { get; }

    /// <summary>Número de portal, opcional: hay direcciones rurales que no lo tienen.</summary>
    public string? Numero { get; }

    /// <summary>Código postal.</summary>
    public string CodigoPostal { get; }

    /// <summary>Población.</summary>
    public string Poblacion { get; }

    /// <summary>Subdivisión del país (en España, la provincia). Opcional en el rulebook.</summary>
    public string? Subdivision { get; }

    /// <summary>País en ISO 3166-1 alfa-2, en mayúsculas.</summary>
    public string Pais { get; }

    /// <summary>Construye una dirección validando la forma de cada campo.</summary>
    /// <remarks>
    /// Lanza en vez de devolver un <c>Resultado</c> a propósito (ADR-0004): la forma la valida
    /// el borde de la API con anotaciones sobre el modelo de petición, así que llegar aquí con
    /// una calle vacía es un error de programación, no un desenlace de negocio.
    /// </remarks>
    public static Direccion De(
        string calle,
        string? numero,
        string codigoPostal,
        string poblacion,
        string? subdivision,
        string pais)
    {
        return new Direccion(
            Obligatorio(calle, nameof(calle), LongitudMaximaDeCalle),
            Opcional(numero, nameof(numero), LongitudMaximaDeNumero),
            Obligatorio(codigoPostal, nameof(codigoPostal), LongitudMaximaDeCodigoPostal),
            Obligatorio(poblacion, nameof(poblacion), LongitudMaximaDePoblacion),
            Opcional(subdivision, nameof(subdivision), LongitudMaximaDeSubdivision),
            NormalizarPais(pais));
    }

    /// <summary>Compone la dirección en una línea, para mostrarla o imprimirla.</summary>
    public string EnUnaLinea()
    {
        StringBuilder linea = new();

        linea.Append(Calle);
        if (Numero is not null)
        {
            linea.Append(' ').Append(Numero);
        }

        linea.Append(", ").Append(CodigoPostal).Append(' ').Append(Poblacion);

        if (Subdivision is not null)
        {
            linea.Append(", ").Append(Subdivision);
        }

        return linea.Append(", ").Append(Pais).ToString();
    }

    private static string Obligatorio(string valor, string campo, int longitudMaxima)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valor, campo);

        string recortado = valor.Trim();
        if (recortado.Length > longitudMaxima)
        {
            throw new ArgumentException(
                $"«{campo}» admite {longitudMaxima} caracteres como máximo (rulebook de SEPA) y trae {recortado.Length}.",
                campo);
        }

        return recortado;
    }

    private static string? Opcional(string? valor, string campo, int longitudMaxima) =>
        string.IsNullOrWhiteSpace(valor) ? null : Obligatorio(valor, campo, longitudMaxima);

    private static string NormalizarPais(string pais)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pais);

        // `ToUpperInvariant` y no `ToUpper()`: en una máquina con cultura turca, `ToUpper()`
        // convierte la «i» en «İ» y «is» dejaría de pasar por código de país.
        string codigo = pais.Trim().ToUpperInvariant();

        if (codigo.Length != LongitudDelPais || !codigo.All(char.IsAsciiLetterUpper))
        {
            throw new ArgumentException(
                $"El país se guarda en ISO 3166-1 alfa-2 (dos letras): «{pais}» no lo es.",
                nameof(pais));
        }

        return codigo;
    }
}
