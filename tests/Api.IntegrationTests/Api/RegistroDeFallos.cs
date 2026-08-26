using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

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
/// aserción, que sí sale publicado como anotación. No toca ni un servicio del contenedor: es un
/// proveedor de registro más.
/// </para>
/// </remarks>
public sealed class RegistroDeFallos : ILoggerProvider
{
    private const int Recordados = 10;

    private static readonly ConcurrentQueue<string> s_fallos = new();

    /// <summary>Lo último que la API dio por error, o cadena vacía.</summary>
    public static string Ultimos => s_fallos.IsEmpty
        ? string.Empty
        : Environment.NewLine + "· registro del servidor: " + string.Join(
            Environment.NewLine + "· ", s_fallos);

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName) => new Anotador(categoryName);

    /// <inheritdoc/>
    public void Dispose() => GC.SuppressFinalize(this);

    private sealed class Anotador(string categoria) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            ArgumentNullException.ThrowIfNull(formatter);

            // El tipo y el mensaje de la excepción, y la primera línea de la pila: lo justo para
            // saber a qué fichero ir. La pila entera convertiría cada rojo en una pared de texto.
            string donde = exception is null
                ? string.Empty
                : $" — {exception.GetType().Name}: {exception.Message} @ {PrimeraLinea(exception)}";

            s_fallos.Enqueue($"[{categoria}] {formatter(state, exception)}{donde}");

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
}
