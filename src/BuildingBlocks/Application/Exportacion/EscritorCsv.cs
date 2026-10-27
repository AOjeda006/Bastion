using System.Text;
using Bastion.BuildingBlocks.Application.Importacion;

namespace Bastion.BuildingBlocks.Application.Exportacion;

/// <summary>
/// Escribe un listado en CSV, en el dialecto que lee la importación (ADR-0034 §5), página a página
/// y sin tener nunca más de una en memoria (ADR-0073).
/// </summary>
/// <remarks>
/// <para>
/// <b>El dialecto es el de la importación, con dos cosas que allí se admiten y aquí se fijan</b>:
/// la codificación es UTF-8 con BOM, que es lo que necesita un Excel en español para no leerlo como
/// Windows-1252, y el fin de línea es CRLF, el de la RFC 4180. El separador es el punto y coma, y
/// un campo va entre comillas si lleva un punto y coma, una comilla o un salto de línea, con cada
/// comilla doblada.
/// </para>
/// <para>
/// <b>El apóstrofo va antes que las comillas</b>, y solo en los campos de texto
/// (<see cref="CampoCsv.EsTexto"/>): un texto que empieza por <c>=</c>, <c>+</c>, <c>-</c>,
/// <c>@</c>, un tabulador o un retorno sale con un apóstrofo delante, que es lo que una hoja de
/// cálculo entiende como «esto es texto» (decisión 7 del 1.11). También uno que ya empieza por un
/// apóstrofo, para que la importación pueda quitar el suyo sin quitar nada más
/// (<see cref="ApostrofoDelDialecto"/>, ADR-0075). La cabecera la escribe Bastion y no lo lleva.
/// </para>
/// <para>
/// <b>Cada página se vacía a la corriente en cuanto se ha escrito</b>, así que lo que se queda en
/// memoria es una página de elementos y el búfer del escritor, no el fichero.
/// </para>
/// </remarks>
public static class EscritorCsv
{
    /// <summary>El separador de campos del dialecto.</summary>
    public const char Separador = ';';

    /// <summary>El fin de línea, el de la RFC 4180.</summary>
    public const string FinDeLinea = "\r\n";

    // El BOM se escribe a mano y el escritor va sin él, para no depender de cuándo decide
    // `StreamWriter` escribir el preámbulo: lo escribe o no según la corriente se pueda posicionar.
    private static readonly UTF8Encoding s_sinBom = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly char[] s_obligaAComillas = [Separador, '"', '\r', '\n'];

    /// <summary>El BOM de UTF-8, con el que empieza el fichero.</summary>
    public static ReadOnlyMemory<byte> Bom { get; } = new byte[] { 0xEF, 0xBB, 0xBF };

    /// <summary>
    /// Escribe el fichero entero en <paramref name="destino"/>: el BOM, la cabecera y una línea por
    /// elemento de cada página, vaciando la corriente al final de cada una.
    /// </summary>
    /// <typeparam name="T">Lo que publica el listado de cada elemento.</typeparam>
    /// <param name="destino">Adónde se escribe. No se cierra.</param>
    /// <param name="hoja">Las columnas.</param>
    /// <param name="paginas">Las páginas, en orden. Se piden de una en una.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public static async Task EscribirAsync<T>(
        Stream destino,
        HojaDeExportacion<T> hoja,
        IAsyncEnumerable<IReadOnlyList<T>> paginas,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(destino);
        ArgumentNullException.ThrowIfNull(hoja);
        ArgumentNullException.ThrowIfNull(paginas);

        await destino.WriteAsync(Bom, cancelacion).ConfigureAwait(false);

        StreamWriter escritor = new(destino, s_sinBom, bufferSize: 16 * 1024, leaveOpen: true);

        await using (escritor.ConfigureAwait(false))
        {
            await escritor.WriteAsync(Cabecera(hoja).AsMemory(), cancelacion).ConfigureAwait(false);

            await foreach (IReadOnlyList<T> pagina in paginas
                .WithCancellation(cancelacion)
                .ConfigureAwait(false))
            {
                foreach (T elemento in pagina)
                {
                    await escritor.WriteAsync(Linea(hoja, elemento).AsMemory(), cancelacion)
                        .ConfigureAwait(false);
                }

                await escritor.FlushAsync(cancelacion).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Cómo sale un campo en el fichero: con su apóstrofo, si le toca, y entre comillas, si las
    /// necesita.
    /// </summary>
    /// <param name="campo">El campo.</param>
    public static string Escribir(CampoCsv campo) =>
        Entrecomillar(campo.EsTexto ? ApostrofoDelDialecto.Poner(campo.Escrito) : campo.Escrito);

    private static string Cabecera<T>(HojaDeExportacion<T> hoja) =>
        string.Join(Separador, hoja.Columnas.Select(columna => Entrecomillar(columna.Nombre))) + FinDeLinea;

    private static string Linea<T>(HojaDeExportacion<T> hoja, T elemento) =>
        string.Join(Separador, hoja.Columnas.Select(columna => Escribir(columna.Campo(elemento)))) + FinDeLinea;

    private static string Entrecomillar(string escrito) =>
        escrito.IndexOfAny(s_obligaAComillas) < 0
            ? escrito
            : "\"" + escrito.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}
