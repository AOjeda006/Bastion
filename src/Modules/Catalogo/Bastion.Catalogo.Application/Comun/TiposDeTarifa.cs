using Bastion.Catalogo.Domain.Catalogo;

namespace Bastion.Catalogo.Application.Comun;

/// <summary>
/// Lee el tipo de tarifa que llega por la API y lo convierte en el del dominio.
/// </summary>
/// <remarks>
/// El gemelo de <see cref="TiposDeArticulo"/>, y por el mismo motivo: el enumerado no se ve desde
/// el contrato, así que la traducción es de la primera capa que ve las dos orillas.
/// </remarks>
internal static class TiposDeTarifa
{
    /// <summary>Los nombres admitidos, en el orden en que se cuentan al rechazar.</summary>
    internal static string Admitidos => string.Join(", ", Enum.GetNames<TipoDeTarifa>());

    /// <summary>
    /// Convierte el texto en un <see cref="TipoDeTarifa"/>, o devuelve nulo si no es ninguno.
    /// </summary>
    /// <remarks>
    /// Contra los NOMBRES y no con <c>Enum.TryParse</c>, como el del artículo: <c>TryParse</c>
    /// dejaría entrar un <c>"1"</c> como <c>Compra</c>, y una lista separada por comas.
    /// </remarks>
    /// <param name="texto">El tipo tal como llegó en el cuerpo.</param>
    internal static TipoDeTarifa? Leer(string? texto) =>
        Array.Exists(
            Enum.GetNames<TipoDeTarifa>(),
            nombre => string.Equals(nombre, texto, StringComparison.Ordinal))
            ? Enum.Parse<TipoDeTarifa>(texto!)
            : null;
}
