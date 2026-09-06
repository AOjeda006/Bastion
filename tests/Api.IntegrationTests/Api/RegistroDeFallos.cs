using System.Collections.Concurrent;
using System.Globalization;
using Serilog.Core;
using Serilog.Events;

namespace Bastion.Api.IntegrationTests.Api;

/// <summary>
/// Se queda con lo que la API escribe en su registro al nivel de error, para que un
/// <c>500</c> en la CI diga <b>qué</b> ha reventado.
/// </summary>
/// <remarks>
/// <para>
/// La respuesta de un <c>500</c> no cuenta nada de dentro —eso es una regla del producto y se
/// comprueba en <c>EntradaHostilTests</c>—, así que desde fuera un fallo del servidor es una
/// pared: «error interno» y un identificador de traza. En local se mira el registro; en la CI
/// <b>no se puede</b>, porque los registros de un <i>job</i> devuelven 403 sin autenticar.
/// </para>
/// <para>
/// Esto lo arregla por el único sitio por el que se puede: el host de pruebas se ejecuta en el
/// mismo proceso que el test, así que su registro se puede capturar y adjuntar al mensaje de la
/// aserción, que sí sale publicado como anotación. No toca ni un servicio del contenedor.
/// </para>
/// <para>
/// <b>Y hasta el ítem 1.5 no capturaba nada</b>, escrito como <c>ILoggerProvider</c>: la fábrica de
/// registro del host es la de Serilog e ignora todo proveedor ajeno. Se vio con las dos respuestas
/// de una mutación en la CI —un <c>500</c> de verdad, y detrás ni una línea de registro—, o sea en
/// el escenario exacto para el que esto existe. Ahora es un sumidero, y el enganche está en
/// <see cref="Bastion.Pruebas.Comun.CapturaDeRegistro"/> con su canario en el carril rápido.
/// </para>
/// </remarks>
public sealed class RegistroDeFallos : ILogEventSink
{
    private const int Recordados = 10;

    private static readonly ConcurrentQueue<string> s_fallos = new();

    /// <summary>Lo último que la API dio por error, o cadena vacía.</summary>
    public static string Ultimos => s_fallos.IsEmpty
        ? string.Empty
        : Environment.NewLine + "· registro del servidor: " + string.Join(
            Environment.NewLine + "· ", s_fallos);

    /// <inheritdoc/>
    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        if (logEvent.Level < LogEventLevel.Error)
        {
            return;
        }

        // El tipo y el mensaje de la excepción, y la primera línea de la pila: lo justo para
        // saber a qué fichero ir. La pila entera convertiría cada rojo en una pared de texto.
        string donde = logEvent.Exception is null
            ? string.Empty
            : $" — {logEvent.Exception.GetType().Name}: {logEvent.Exception.Message}"
              + $" @ {PrimeraLinea(logEvent.Exception)}";

        string categoria = logEvent.Properties.TryGetValue("SourceContext", out LogEventPropertyValue? origen)
            ? origen.ToString().Trim('"')
            : "sin categoría";

        s_fallos.Enqueue(
            $"[{categoria}] {logEvent.RenderMessage(CultureInfo.InvariantCulture)}{donde}");

        while (s_fallos.Count > Recordados)
        {
            s_fallos.TryDequeue(out _);
        }
    }

    private static string PrimeraLinea(Exception excepción)
    {
        Exception raiz = excepción;

        while (raiz.InnerException is not null)
        {
            raiz = raiz.InnerException;
        }

        string pila = raiz.StackTrace ?? string.Empty;
        int salto = pila.IndexOf('\n', StringComparison.Ordinal);
        string primera = salto < 0 ? pila : pila[..salto];

        return raiz == excepción ? primera.Trim() : $"{raiz.GetType().Name}: {raiz.Message}";
    }
}
