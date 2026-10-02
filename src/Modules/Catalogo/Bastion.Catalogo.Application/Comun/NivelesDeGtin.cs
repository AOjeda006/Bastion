using Bastion.Catalogo.Domain.Catalogo;

namespace Bastion.Catalogo.Application.Comun;

/// <summary>
/// Lee el nivel de un código de barras que llega por la API y lo convierte en el del dominio.
/// </summary>
/// <remarks>
/// Vive aquí por lo mismo que <see cref="TiposDeArticulo"/>: el enumerado no se ve desde el
/// contrato, así que la traducción es de la primera capa que ve las dos orillas.
/// </remarks>
internal static class NivelesDeGtin
{
    /// <summary>Los nombres admitidos, en el orden en que se cuentan al rechazar.</summary>
    internal static string Admitidos => string.Join(", ", Enum.GetNames<NivelDeGtin>());

    /// <summary>
    /// Convierte el texto en un <see cref="NivelDeGtin"/>, o devuelve nulo si no es ninguno.
    /// </summary>
    /// <remarks>
    /// Contra los NOMBRES y no con <c>Enum.TryParse</c>, como el tipo del artículo: <c>TryParse</c>
    /// acepta el ordinal en texto, y un <c>"2"</c> entraría como <c>Palet</c>.
    /// </remarks>
    /// <param name="texto">El nivel tal como llegó en el cuerpo.</param>
    internal static NivelDeGtin? Leer(string? texto) =>
        Array.Exists(
            Enum.GetNames<NivelDeGtin>(),
            nombre => string.Equals(nombre, texto, StringComparison.Ordinal))
            ? Enum.Parse<NivelDeGtin>(texto!)
            : null;
}
