using System.Globalization;
using Bastion.Pruebas.Comun;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Errores;

/// <summary>
/// El arnés tiene arnés: lo que <see cref="CapturaDeRegistro"/> engancha al host <b>recibe</b> lo
/// que la API escribe, con su identificador de suceso dentro.
/// </summary>
/// <remarks>
/// <para>
/// <b>Esto existe por un falso verde que costó un run entero.</b> Los dos captadores de
/// <c>Api.IntegrationTests</c> estaban escritos como <see cref="ILoggerProvider"/> y añadidos con
/// <c>ConfigureLogging(r =&gt; r.AddProvider(...))</c>. No recogían nada, y no porque estuvieran
/// mal: <c>Program.cs</c> hace <c>ClearProviders()</c> y luego <c>AddSerilog(...)</c>, que sustituye
/// la fábrica de registro por la de Serilog — y esa fábrica ignora todo proveedor ajeno. El
/// resultado era el peor posible: un test preguntaba «¿cuántos sucesos se anotaron?», recibía
/// <b>cero</b>, y cero se lee como «la API no anota» cuando significaba «no hay cable».
/// </para>
/// <para>
/// <b>Por qué está aquí y no en el carril de integración.</b> Porque lo que se rompió no necesita
/// base de datos ni contenedor: se rompió el <b>enganche</b>. Puesto donde solo corre con Docker
/// delante, este canario habría avisado en la CI y no en la máquina de quien lo rompe — que es
/// exactamente lo que pasó.
/// </para>
/// <para>
/// <b>Y por qué ejerce <see cref="CapturaDeRegistro"/> y no una copia.</b> Un canario que montara su
/// propio enganche comprobaría su propia copia y dejaría el de verdad sin vigilar. La línea que se
/// ejecuta aquí es la misma que ejecuta <c>ApiDeVerdad</c>.
/// </para>
/// </remarks>
public sealed partial class ElCaptadorDeRegistroCapturaTests
{
    private const int SucesoDePrueba = 424_242;

    [Fact]
    public void Lo_que_la_API_escribe_llega_al_sumidero_con_su_identificador_de_suceso()
    {
        Espia espia = new();

        using ApiConElEspia fabrica = new(espia);
        using IServiceScope ambito = fabrica.Services.CreateScope();

        ILogger<ElCaptadorDeRegistroCapturaTests> registro =
            ambito.ServiceProvider.GetRequiredService<ILogger<ElCaptadorDeRegistroCapturaTests>>();

        Anotar(registro, 1);

        IReadOnlyList<LogEvent> recogidos = espia.Recogidos();

        // La mitad que faltaba: que llegue ALGO. Sin ella, todo lo de abajo se cumple en vacío.
        recogidos.ShouldNotBeEmpty(
            "el sumidero enganchado con CapturaDeRegistro.Registrar no ha recibido nada. Es el " +
            "fallo que este canario existe para cazar: la fábrica de registro del host es la de " +
            "Serilog, y a esa no le llega un ILoggerProvider");

        // Y que llegue CON su identificador de suceso legible: es por lo que filtra
        // `RegistroDeSucesos`, y leerlo del evento de Serilog no es trivial —viaja como propiedad
        // estructurada, no como número suelto—, así que es justo lo que puede romperse en silencio.
        recogidos.Select(CapturaDeRegistro.EventIdDe).ShouldContain(SucesoDePrueba);

        // Y el mensaje renderizado, que es lo que las aserciones miran. En cultura invariante, que
        // es lo que el captador usa: sin fijarla, esta línea depende de la máquina que la ejecuta.
        recogidos
            .First(evento => CapturaDeRegistro.EventIdDe(evento) == SucesoDePrueba)
            .RenderMessage(CultureInfo.InvariantCulture)
            .ShouldBe("Un suceso de prueba con 1 propiedad dentro.");
    }

    /// <summary>
    /// Un evento sin identificador de suceso no se lee como si tuviera uno.
    /// </summary>
    /// <remarks>
    /// La otra mitad del canario, y no es simetría por gusto: si
    /// <see cref="CapturaDeRegistro.EventIdDe"/> devolviera cualquier cosa —cero, o el primer entero
    /// que encontrara entre las propiedades—, el caso de arriba seguiría verde y
    /// <c>RegistroDeSucesos</c> recogería líneas que no son las suyas. Un captador que recoge de más
    /// miente igual que uno que no recoge. Los eventos se construyen a mano porque lo que se afirma
    /// es de la lectura, no del host.
    /// </remarks>
    [Fact]
    public void Un_evento_sin_identificador_de_suceso_no_se_lee_como_si_tuviera_uno()
    {
        CapturaDeRegistro.EventIdDe(Evento(propiedad: null)).ShouldBeNull();

        // Una propiedad que NO es el EventId tampoco cuenta, aunque sea un número.
        CapturaDeRegistro.EventIdDe(Evento(new LogEventProperty(
            "OtroNumero", new ScalarValue(SucesoDePrueba)))).ShouldBeNull();

        // Y las dos formas en las que el EventId sí viaja: estructurada —la del puente de
        // Microsoft.Extensions.Logging— y suelta.
        CapturaDeRegistro.EventIdDe(Evento(new LogEventProperty(
            "EventId",
            new StructureValue([
                new LogEventProperty("Id", new ScalarValue(SucesoDePrueba)),
                new LogEventProperty("Name", new ScalarValue("Anotar")),
            ])))).ShouldBe(SucesoDePrueba);

        CapturaDeRegistro.EventIdDe(Evento(new LogEventProperty(
            "EventId", new ScalarValue(SucesoDePrueba)))).ShouldBe(SucesoDePrueba);
    }

    [LoggerMessage(
        EventId = SucesoDePrueba,
        Level = LogLevel.Information,
        Message = "Un suceso de prueba con {Cuantos} propiedad dentro.")]
    private static partial void Anotar(ILogger registro, int cuantos);

    private static LogEvent Evento(LogEventProperty? propiedad)
    {
        LogEvent evento = new(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            exception: null,
            new MessageTemplate("Da igual el texto.", []),
            []);

        if (propiedad is not null)
        {
            evento.AddPropertyIfAbsent(propiedad);
        }

        return evento;
    }

    private sealed class Espia : ILogEventSink
    {
        private readonly Lock _cerrojo = new();
        private readonly List<LogEvent> _eventos = [];

        public void Emit(LogEvent logEvent)
        {
            lock (_cerrojo)
            {
                _eventos.Add(logEvent);
            }
        }

        public IReadOnlyList<LogEvent> Recogidos()
        {
            lock (_cerrojo)
            {
                return [.. _eventos];
            }
        }
    }

    private sealed class ApiConElEspia(ILogEventSink espia) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            AjustesMinimos.Aplicar(builder);
            builder.ConfigureTestServices(servicios => CapturaDeRegistro.Registrar(servicios, espia));
        }
    }
}
