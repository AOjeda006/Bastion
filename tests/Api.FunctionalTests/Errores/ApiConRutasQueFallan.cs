using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bastion.Api.FunctionalTests.Errores;

/// <summary>
/// El host real de la API, con las rutas que fallan y con un segundo sumidero de Serilog que
/// captura todo lo que se registra.
/// </summary>
/// <remarks>
/// El sumidero se añade como un proveedor MÁS, no en sustitución del de la API: así lo que se
/// captura es exactamente lo que el host escribe de verdad, sin reconfigurar su registro.
/// </remarks>
public sealed class ApiConRutasQueFallan : WebApplicationFactory<Program>
{
    public RegistroCapturado Registro { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Bastion", string.Empty);
        builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", string.Empty);

        builder.ConfigureTestServices(servicios =>
        {
            servicios.AddSingleton<IStartupFilter, RutasQueFallan>();
            // `preserveStaticLogger: true` NO es un detalle: por omisión, AddSerilog deja el
            // registro creado en el `Log.Logger` ESTÁTICO y ata el contenedor a ese estático.
            // Con dos hosts de prueba levantándose en paralelo, el último en construirse le
            // pisa el registro al otro y este sumidero no recibe nada. Se ve solo al ejecutar
            // la suite entera: en aislado, el test pasaba.
            servicios.AddSerilog(
                configuracion => configuracion
                    .MinimumLevel.Verbose()
                    .Enrich.FromLogContext()
                    .WriteTo.Sink(Registro),
                preserveStaticLogger: true);
        });
    }
}
