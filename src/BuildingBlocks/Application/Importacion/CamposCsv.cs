using System.Globalization;

namespace Bastion.BuildingBlocks.Application.Importacion;

/// <summary>
/// Cómo se leen, en el dialecto de una hoja de cálculo en español, los campos que no son texto: los
/// importes y los sí o no (ADR-0034 §5).
/// </summary>
/// <remarks>
/// <b>Nada se recorta ni se interpreta por aproximación.</b> Una hoja de cálculo no exporta espacios
/// alrededor de un número ni el símbolo de la divisa en un importe con formato general; si llegan, es
/// que el campo no es lo que la columna espera, y se dice. Leer «1.5» como uno y medio sería leerlo
/// en la cultura de otro, y leerlo como quince, adivinar.
/// </remarks>
public static class CamposCsv
{
    /// <summary>Cuántas cifras enteras admite un importe: las de un <c>numeric(18,4)</c>.</summary>
    public const int CifrasEnterasMaximas = 14;

    /// <summary>Cuántos decimales admite un importe: los de la escala de R6.</summary>
    public const int DecimalesMaximos = 4;

    private static readonly string[] s_si = ["sí", "si", "VERDADERO"];
    private static readonly string[] s_no = ["no", "FALSO"];

    /// <summary>
    /// Lee un sí o un no: <c>sí</c>, <c>si</c>, <c>no</c>, <c>VERDADERO</c> o <c>FALSO</c>, sin distinguir
    /// mayúsculas. Vacío es no.
    /// </summary>
    /// <param name="campo">El campo tal como llegó.</param>
    /// <param name="valor">Lo que dice, si se ha podido leer.</param>
    /// <returns>Si el campo es uno de los admitidos.</returns>
    public static bool IntentarLeerSiNo(string campo, out bool valor)
    {
        ArgumentNullException.ThrowIfNull(campo);

        valor = Array.Exists(s_si, si => string.Equals(si, campo, StringComparison.OrdinalIgnoreCase));

        return valor
            || campo.Length == 0
            || Array.Exists(s_no, no => string.Equals(no, campo, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Lee un importe escrito a la española: coma decimal con hasta cuatro decimales, y punto de miles
    /// solo en grupos de tres.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Admite el signo menos delante: un límite negativo se lee, y lo rechaza la regla con su motivo,
    /// que es más útil que decir que no es un número.
    /// </para>
    /// <para>
    /// <b>Un grupo de miles no empieza por cero.</b> «0.500» no lo escribe una hoja en español, y quien
    /// lo escribe a mano casi siempre quiere decir medio: leerlo como quinientos sería el mismo error
    /// que el dialecto existe para no cometer.
    /// </para>
    /// </remarks>
    /// <param name="campo">El campo tal como llegó.</param>
    /// <param name="valor">El importe, si se ha podido leer.</param>
    /// <returns>Si el campo es un importe del dialecto.</returns>
    public static bool IntentarLeerImporte(string campo, out decimal valor)
    {
        ArgumentNullException.ThrowIfNull(campo);

        valor = 0m;
        ReadOnlySpan<char> resto = campo;
        bool negativo = resto.Length > 0 && resto[0] == '-';

        if (negativo)
        {
            resto = resto[1..];
        }

        int coma = resto.IndexOf(',');
        ReadOnlySpan<char> entera = coma < 0 ? resto : resto[..coma];
        ReadOnlySpan<char> decimales = coma < 0 ? [] : resto[(coma + 1)..];

        if (coma >= 0 && (decimales.Length is 0 or > DecimalesMaximos || !SoloCifras(decimales)))
        {
            return false;
        }

        string? cifras = CifrasDeLaParteEntera(entera);

        if (cifras is null || cifras.Length > CifrasEnterasMaximas)
        {
            return false;
        }

        string invariante = coma < 0 ? cifras : $"{cifras}.{decimales}";
        valor = decimal.Parse(invariante, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

        if (negativo)
        {
            valor = -valor;
        }

        return true;
    }

    // Las cifras sin los puntos de miles, o null si la parte entera no es del dialecto.
    private static string? CifrasDeLaParteEntera(ReadOnlySpan<char> entera)
    {
        if (entera.Length == 0)
        {
            return null;
        }

        if (!entera.Contains('.'))
        {
            return SoloCifras(entera) ? entera.ToString() : null;
        }

        string[] grupos = entera.ToString().Split('.');
        string primero = grupos[0];

        bool primeroValido = primero.Length is >= 1 and <= 3 && SoloCifras(primero) && primero[0] != '0';
        bool restoValido = grupos.Skip(1).All(grupo => grupo.Length == 3 && SoloCifras(grupo));

        return primeroValido && restoValido ? string.Concat(grupos) : null;
    }

    // `char.IsAsciiDigit` y no `char.IsDigit`: el segundo admite las cifras de otras escrituras, y
    // «١٢» no es un importe del dialecto.
    private static bool SoloCifras(ReadOnlySpan<char> texto)
    {
        foreach (char caracter in texto)
        {
            if (!char.IsAsciiDigit(caracter))
            {
                return false;
            }
        }

        return true;
    }
}
