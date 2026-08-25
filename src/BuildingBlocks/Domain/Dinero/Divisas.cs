namespace Bastion.BuildingBlocks.Domain.Dinero;

// La divisa se guarda como texto ISO 4217 y no como enumerado: el catálogo lo mantiene la ISO,
// no nosotros, y un enumerado obligaría a recompilar para admitir una divisa más.
/// <summary>
/// Catálogo de divisas: qué es un código válido y con cuántos decimales se redondea cada una.
/// </summary>
/// <remarks>
/// Pasó de <c>internal</c> a <c>public</c> en el ítem 0.4: la divisa base de una empresa se
/// valida contra este mismo catálogo. Duplicar la comprobación en el módulo habría sido tener
/// dos listas de divisas que se separan en cuanto entre la segunda.
/// </remarks>
public static class Divisas
{
    // Unidad mínima (decimales de redondeo fiscal) POR divisa. Hoy solo el euro, que es lo
    // único que Bastion factura. Deliberadamente NO hay valor por omisión: suponer dos
    // decimales acertaría con el dólar y fallaría en silencio con el yen (cero decimales) o
    // el dinar (tres). Cuando entre una divisa más, entra aquí con su caso dorado.
    private static readonly Dictionary<string, int> s_unidadMinima =
        new(StringComparer.Ordinal) { ["EUR"] = 2 };

    /// <summary>Normaliza el código y comprueba que tiene forma ISO 4217.</summary>
    public static string Normalizar(string divisa)
    {
        ArgumentNullException.ThrowIfNull(divisa);

        string normalizada = divisa.Trim().ToUpperInvariant();

        return normalizada.Length == 3 && normalizada.All(char.IsAsciiLetterUpper)
            ? normalizada
            : throw new ArgumentException(
                $"La divisa {divisa} no es un código ISO 4217 (tres letras, como EUR).", nameof(divisa));
    }

    /// <summary>Decimales de redondeo fiscal de la divisa; lanza si no se conoce.</summary>
    public static int UnidadMinima(string divisa) =>
        s_unidadMinima.TryGetValue(divisa, out int decimales)
            ? decimales
            : throw new NotSupportedException(
                $"No se conoce la unidad mínima de {divisa}. Añádela en Divisas con su caso dorado " +
                "antes de redondear importes en esa divisa.");
}
