using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Bastion.Pruebas.Comun;

/// <summary>
/// Engancha sumideros de Serilog al host de pruebas, que es la <b>única</b> forma de observar lo
/// que la API escribe en su registro.
/// </summary>
/// <remarks>
/// <para>
/// <b>Existe por un falso verde que costó un run.</b> Los dos captadores de
/// <c>Api.IntegrationTests</c> estaban escritos como <see cref="Microsoft.Extensions.Logging.
/// ILoggerProvider"/> y añadidos con <c>ConfigureLogging(r =&gt; r.AddProvider(...))</c>. Eso
/// <b>no recoge nada</b>, y no por un error de los captadores: <c>Program.cs</c> hace
/// <c>ClearProviders()</c> y luego <c>AddSerilog(...)</c>, que sustituye la fábrica de registro por
/// la de Serilog — y esa fábrica <b>ignora todo proveedor</b> que no sea el suyo. La API escribía,
/// el captador existía, y entre los dos no había ningún cable. Un test que preguntara «¿cuántos
/// sucesos se anotaron?» recibía cero y lo interpretaba como «la API no anota», que es la respuesta
/// equivocada a la pregunta equivocada.
/// </para>
/// <para>
/// <b>Y por qué esto está compartido en vez de copiado.</b> El fallo no estaba en el captador sino
/// en <b>cómo se enganchaba</b>, así que un canario que copiara el enganche no habría probado nada:
/// habría comprobado su propia copia. Al vivir el enganche aquí, el canario del carril rápido
/// —<c>ElCaptadorDeRegistroCapturaTests</c>, que no necesita ni base de datos ni contenedor—
/// ejercita <b>esta misma línea</b>, que es la que se rompió.
/// </para>
/// <para>
/// <c>preserveStaticLogger: true</c> no es un detalle: por omisión <c>AddSerilog</c> deja el
/// registro creado en el <c>Log.Logger</c> estático y ata el contenedor a ese estático, así que con
/// dos hosts de prueba levantándose a la vez el último le pisa el registro al otro. La misma razón
/// —y la misma línea— que <c>ApiConRutasQueFallan</c> ya llevaba escrita en el carril funcional.
/// </para>
/// </remarks>
public static class CapturaDeRegistro
{
    /// <summary>
    /// Añade los sumideros al registro del host, <b>sin sustituir</b> el que la API configura.
    /// </summary>
    /// <param name="servicios">Contenedor del host de pruebas.</param>
    /// <param name="sumideros">Los captadores que quieren ver lo que la API escribe.</param>
    public static void Registrar(IServiceCollection servicios, params ILogEventSink[] sumideros)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(sumideros);

        servicios.AddSerilog(
            configuracion =>
            {
                // `Verbose` y no el mínimo de la API: lo que cada captador decide mirar es cosa
                // suya, y un mínimo aquí volvería a producir un cero que se lee como «no pasó».
                configuracion.MinimumLevel.Verbose();
                configuracion.Enrich.FromLogContext();

                foreach (ILogEventSink sumidero in sumideros)
                {
                    configuracion.WriteTo.Sink(sumidero);
                }
            },
            preserveStaticLogger: true);
    }

    /// <summary>
    /// El identificador de suceso de un evento, o <c>null</c> si no lleva ninguno.
    /// </summary>
    /// <remarks>
    /// El puente de <c>Microsoft.Extensions.Logging</c> a Serilog escribe el <c>EventId</c> como
    /// una propiedad <b>estructurada</b> con <c>Id</c> y <c>Name</c> dentro, no como un número
    /// suelto. Leerlo a mano es feo, y la alternativa —que cada captador se lo invente— es peor:
    /// dos formas distintas de leer lo mismo divergen el día que una se equivoca.
    /// </remarks>
    /// <param name="evento">El evento tal como lo recibe el sumidero.</param>
    public static int? EventIdDe(LogEvent evento)
    {
        ArgumentNullException.ThrowIfNull(evento);

        if (!evento.Properties.TryGetValue("EventId", out LogEventPropertyValue? valor))
        {
            return null;
        }

        if (valor is ScalarValue suelto && suelto.Value is int numero)
        {
            return numero;
        }

        if (valor is not StructureValue estructura)
        {
            return null;
        }

        foreach (LogEventProperty campo in estructura.Properties)
        {
            if (campo.Name == "Id" && campo.Value is ScalarValue { Value: int id })
            {
                return id;
            }
        }

        return null;
    }
}
