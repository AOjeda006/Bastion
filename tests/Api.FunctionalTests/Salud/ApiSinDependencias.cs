using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bastion.Api.FunctionalTests.Salud;

/// <summary>
/// La API levantada sin ninguna dependencia externa: ni PostgreSQL ni recolector de
/// telemetría. Las dos ausencias son EXPLÍCITAS y no heredadas del entorno de quien
/// ejecute los tests, para que el resultado no dependa de la máquina.
/// </summary>
public sealed class ApiSinDependencias : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Bastion", string.Empty);
        builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", string.Empty);
    }
}
