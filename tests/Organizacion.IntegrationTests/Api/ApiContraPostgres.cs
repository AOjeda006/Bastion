using Bastion.Organizacion.IntegrationTests.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bastion.Organizacion.IntegrationTests.Api;

/// <summary>
/// La API real, con su <c>Program.cs</c> y su contenedor de dependencias, apuntando al PostgreSQL
/// del contenedor de pruebas.
/// </summary>
/// <remarks>
/// <para>
/// Lo único que se sustituye es la cadena de conexión, y por el sitio por el que la configuración
/// entra de verdad. Nada de reemplazar servicios en el contenedor: en cuanto se cambia un
/// registro, lo que se prueba deja de ser el sistema que se despliega.
/// </para>
/// <para>
/// El recolector de telemetría se apaga EXPLÍCITAMENTE, y no se hereda del entorno de quien
/// ejecute los tests: si no, en una máquina con <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> puesto cada
/// test intentaría exportar a un sitio que no está, y el resultado dependería de la máquina.
/// </para>
/// </remarks>
public sealed class ApiContraPostgres(PostgresDeVerdad postgres) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("ConnectionStrings:Bastion", postgres.CadenaDeConexion);
        builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", string.Empty);
    }
}
