namespace Bastion.BuildingBlocks.Domain.Dinero;

// La divisa se guarda como texto ISO 4217 y no como enumerado: el catálogo lo mantiene la ISO,
// no nosotros, y un enumerado obligaría a recompilar para admitir una divisa más.
internal static class Divisas
{
    // Unidad mínima (decimales de redondeo fiscal) POR divisa. Hoy solo el euro, que es lo
    // único que Bastion factura. Deliberadamente NO hay valor por omisión: suponer dos
    // decimales acertaría con el dólar y fallaría en silencio con el yen (cero decimales) o
    // el dinar (tres). Cuando entre una divisa más, entra aquí con su caso dorado.
    private static readonly Dictionary<string, int> s_unidadMinima =
        new(StringComparer.Ordinal) { ["EUR"] = 2 };

    internal static string Normalizar(string divisa)
    {
        ArgumentNullException.ThrowIfNull(divisa);

        string normalizada = divisa.Trim().ToUpperInvariant();

        return normalizada.Length == 3 && normalizada.All(char.IsAsciiLetterUpper)
            ? normalizada
            : throw new ArgumentException(
                $"La divisa {divisa} no es un código ISO 4217 (tres letras, como EUR).", nameof(divisa));
    }

    internal static int UnidadMinima(string divisa) =>
        s_unidadMinima.TryGetValue(divisa, out int decimales)
            ? decimales
            : throw new NotSupportedException(
                $"No se conoce la unidad mínima de {divisa}. Añádela en Divisas con su caso dorado " +
                "antes de redondear importes en esa divisa.");
}
