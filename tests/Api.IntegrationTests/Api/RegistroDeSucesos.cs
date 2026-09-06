using System.Collections.Concurrent;
using System.Globalization;
using Bastion.Pruebas.Comun;
using Serilog.Core;
using Serilog.Events;

namespace Bastion.Api.IntegrationTests.Api;

/// <summary>
/// Se queda con los sucesos que algún test tiene que poder observar: los que la API anota <b>por
/// dentro</b> y no cuenta en la respuesta.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es el gemelo de <see cref="RegistroDeFallos"/> y existe por el motivo contrario.</b> Aquel
/// recoge errores para que un <c>500</c> de la CI diga qué reventó; este recoge información que la
/// API anota <b>a propósito</b> y que ninguna respuesta contiene, porque contarla sería la fuga.
/// El caso que lo estrena es el del ítem 1.5: cuando un alta de tercero choca contra un
/// identificador ocupado, quien pregunta no puede enterarse de si el que estorba está bloqueado —y
/// la traza sí tiene que decirlo, porque el art. 32 obliga a saber quién miró datos reservados—.
/// Sin esto, «la traza lo registra» sería una promesa escrita en un comentario.
/// </para>
/// <para>
/// <b>Es un sumidero de Serilog y no un <c>ILoggerProvider</c>, y esa palabra costó un run.</b>
/// Escrito como proveedor no recogía nada: <c>Program.cs</c> sustituye la fábrica de registro por
/// la de Serilog, que ignora todo proveedor ajeno. El enganche vive en
/// <see cref="CapturaDeRegistro"/>, compartido, para que el canario del carril rápido ejerza la
/// misma línea que el host de integración.
/// </para>
/// <para>
/// <b>Solo los identificadores declarados en <see cref="Observados"/>.</b> Recoger todo lo que la
/// API escribe al nivel de información sería quedarse con cada consulta de cada test de este
/// carril. La lista es corta a propósito: lo que entra aquí es lo que un test mira.
/// </para>
/// </remarks>
public sealed class RegistroDeSucesos : ILogEventSink
{
    /// <summary>
    /// Los identificadores de suceso que este captador recoge, y quién los mira.
    /// </summary>
    /// <remarks>
    /// 8400 — <c>RepositorioDeTerceros</c>, alta rechazada por identificador ocupado. Lo mira
    /// <c>ElConflictoQueNoRevelaTests</c>.
    /// </remarks>
    public static readonly int[] Observados = [8400];

    private const int Recordados = 50;

    private static readonly ConcurrentQueue<Suceso> s_sucesos = new();

    /// <summary>Lo anotado hasta ahora con ese identificador de suceso.</summary>
    /// <param name="eventId">El identificador del suceso.</param>
    public static IReadOnlyList<Suceso> Con(int eventId) =>
        [.. s_sucesos.Where(suceso => suceso.EventId == eventId)];

    /// <summary>Vacía lo recogido, para que un test no lea lo que anotó otro.</summary>
    public static void Olvidar()
    {
        while (s_sucesos.TryDequeue(out _))
        {
            // Vaciar es el efecto; no hace falta nada con lo que sale.
        }
    }

    /// <inheritdoc/>
    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        int? eventId = CapturaDeRegistro.EventIdDe(logEvent);

        if (eventId is null || !Observados.Contains(eventId.Value))
        {
            return;
        }

        // Cultura invariante y no la de la máquina: hay tests que afirman sobre este texto, y un
        // mensaje con un número o una fecha dentro se renderiza distinto en cada portátil.
        s_sucesos.Enqueue(
            new Suceso(eventId.Value, logEvent.RenderMessage(CultureInfo.InvariantCulture)));

        while (s_sucesos.Count > Recordados && s_sucesos.TryDequeue(out _))
        {
            // El anillo se queda con los últimos; lo viejo ya no lo mira nadie.
        }
    }

    /// <summary>Un suceso anotado: qué fue y con qué texto.</summary>
    /// <param name="EventId">Identificador del suceso.</param>
    /// <param name="Mensaje">El mensaje ya formateado.</param>
    public sealed record Suceso(int EventId, string Mensaje);
}
