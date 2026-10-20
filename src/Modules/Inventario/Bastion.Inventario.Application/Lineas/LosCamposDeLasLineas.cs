using System.Text.Json;

namespace Bastion.Inventario.Application.Lineas;

/// <summary>La ruta de un campo de una línea, tal como viaja en el cuerpo (ADR-0065 §3).</summary>
/// <remarks>
/// <para>
/// <b>Con el índice desde cero</b>, que es el de la ruta de <c>zod</c> sobre el formulario: el
/// error del servidor y el de la pantalla caen en el mismo campo. Los nombres salen de las
/// propiedades del contrato con la política del serializador, así que no hay una cadena
/// tecleada que se pueda quedar atrás.
/// </para>
/// <para>
/// <b>Es de los dos documentos con líneas</b>, el ajuste y la transferencia (ADR-0068 §6). Cada uno
/// le pasa el <c>nameof</c> de su colección y de su propiedad.
/// </para>
/// </remarks>
internal static class LosCamposDeLasLineas
{
    /// <summary>La ruta del campo.</summary>
    /// <param name="coleccion">La propiedad de la petición que lleva las líneas, por su <c>nameof</c>.</param>
    /// <param name="linea">La línea, desde uno, como la dicen los mensajes.</param>
    /// <param name="propiedad">La propiedad de la línea, por su <c>nameof</c>.</param>
    /// <returns>La ruta, como <c>lineas[0].codigoDeLote</c>.</returns>
    internal static string Ruta(string coleccion, int linea, string propiedad) =>
        $"{JsonNamingPolicy.CamelCase.ConvertName(coleccion)}[{linea - 1}]." +
        JsonNamingPolicy.CamelCase.ConvertName(propiedad);

    /// <summary>El campo de una línea que falla, para la sobrecarga de la validación.</summary>
    /// <remarks>
    /// <b>No fabrica el error, y es a propósito.</b> El catálogo de <c>type</c> lo saca un barrido
    /// que lee el primer argumento de cada llamada a <c>ErrorDeOperacion</c> (ADR-0030): un ayudante
    /// que recibiera el código como parámetro dejaría todos los suyos sin resolver. Así el código va,
    /// literal, en la llamada de cada error.
    /// </remarks>
    /// <param name="coleccion">La propiedad de la petición que lleva las líneas, por su <c>nameof</c>.</param>
    /// <param name="linea">La línea, desde uno.</param>
    /// <param name="propiedad">La propiedad que falla, por su <c>nameof</c>.</param>
    /// <param name="motivo">Lo que se dice del campo.</param>
    /// <returns>Un solo campo, con su motivo.</returns>
    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> ElCampo(
        string coleccion, int linea, string propiedad, string motivo) =>
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [Ruta(coleccion, linea, propiedad)] = [motivo],
        };
}
