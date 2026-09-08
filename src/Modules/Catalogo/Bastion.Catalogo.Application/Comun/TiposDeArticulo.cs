using Bastion.Catalogo.Domain.Catalogo;

namespace Bastion.Catalogo.Application.Comun;

/// <summary>
/// Lee el tipo de artículo que llega por la API y lo convierte en el del dominio.
/// </summary>
/// <remarks>
/// Vive aquí por lo mismo que <c>RegimenesFiscales</c> en Terceros: el enumerado no se ve desde el
/// contrato —<c>Contracts</c> no referencia el dominio—, así que la traducción es de la primera
/// capa que ve las dos orillas.
/// </remarks>
internal static class TiposDeArticulo
{
    /// <summary>Los nombres admitidos, en el orden en que se cuentan al rechazar.</summary>
    internal static string Admitidos => string.Join(", ", Enum.GetNames<TipoDeArticulo>());

    /// <summary>
    /// Convierte el texto en un <see cref="TipoDeArticulo"/>, o devuelve nulo si no es ninguno.
    /// </summary>
    /// <remarks>
    /// Se compara contra los NOMBRES y no con <c>Enum.TryParse</c>, y no es estilo: <c>TryParse</c>
    /// acepta el ordinal en texto —un <c>"1"</c> entraría como <c>Servicio</c>, justo el
    /// acoplamiento al orden del enumerado que el contrato dice no tener— y acepta listas separadas
    /// por comas. Comparando con <c>GetNames</c> en ordinal, lo único que entra es uno de los dos,
    /// escrito tal cual.
    /// </remarks>
    /// <param name="texto">El tipo tal como llegó en el cuerpo.</param>
    internal static TipoDeArticulo? Leer(string? texto) =>
        Array.Exists(
            Enum.GetNames<TipoDeArticulo>(),
            nombre => string.Equals(nombre, texto, StringComparison.Ordinal))
            ? Enum.Parse<TipoDeArticulo>(texto!)
            : null;
}
