// Composition root de Bastion: el host único donde se cablean los módulos (§4 del plan
// maestro). La construcción del sistema vive aquí, separada de su uso
// (`principios/clean-architecture.md`); ningún módulo se registra a sí mismo por su cuenta.

using Bastion.BuildingBlocks.Infrastructure.Salud;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string rutaDeVida = "/health/live";
string rutaDeDisponibilidad = "/health/ready";
string etiquetaDeDisponibilidad = "disponibilidad";
string nombreDeLaBase = "base-de-datos";

// ----------------------------------------------------------------------- registro
// Estructurado y a consola: en contenedor, la salida estándar ES el transporte de logs
// (`herramientas/observabilidad.md`). `ClearProviders` evita que el proveedor de consola
// que trae el host duplique cada línea.
//
// El formato es JSON compacto también en desarrollo, a propósito: un registro que se lee
// distinto en local que en producción deja de ser el que se depura de verdad. Serilog 4
// arrastra en cada evento el TraceId y el SpanId de la actividad en curso, y este
// formateador los escribe como @tr y @sp — esa es la correlación entre traza y registro,
// sin necesidad de inventar y mantener una cabecera de correlación propia.
builder.Logging.ClearProviders();
builder.Services.AddSerilog(registro => registro
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter()));

// --------------------------------------------------------------------- telemetría
// La aplicación habla OTLP y nada más; quién hay detrás lo decide el recolector.
// Si no hay recolector configurado (tests funcionales, un `dotnet run` a pelo) NO se
// registra el exportador: cada intento de exportar sería un error de red repetido que
// ensucia el registro sin aportar nada.
string extremoDelRecolector = (builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? string.Empty).Trim();
bool hayRecolector = extremoDelRecolector.Length > 0;

builder.Services.AddOpenTelemetry()
    .ConfigureResource(recurso => recurso.AddService(
        serviceName: builder.Configuration["OTEL_SERVICE_NAME"] ?? "bastion-api",
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString(),
        serviceInstanceId: Environment.MachineName))
    .WithTracing(trazas =>
    {
        // Las sondas se consultan cada pocos segundos y no cuentan nada: trazarlas llena
        // el visor de ruido y esconde las peticiones que sí importan.
        trazas.AddAspNetCoreInstrumentation(opciones =>
            opciones.Filter = contexto => !EsSonda(contexto.Request.Path));
        trazas.AddHttpClientInstrumentation();

        if (hayRecolector)
        {
            trazas.AddOtlpExporter();
        }
    })
    .WithMetrics(metricas =>
    {
        // Las métricas sí incluyen las sondas: son agregados, no un evento por petición.
        metricas.AddAspNetCoreInstrumentation();
        metricas.AddHttpClientInstrumentation();
        metricas.AddRuntimeInstrumentation();

        if (hayRecolector)
        {
            metricas.AddOtlpExporter();
        }
    });

// ----------------------------------------------------------------- sondas de salud
// Dos sondas y no una, y la diferencia importa: VIDA responde "el proceso responde" sin
// mirar dependencia alguna; DISPONIBILIDAD responde "puedo atender tráfico" y sí mira la
// base. Si la de vida mirase la base, un corte de PostgreSQL haría que el orquestador
// reiniciara la API en bucle — y reiniciar la API no arregla la base.
string cadenaDeConexion = (builder.Configuration.GetConnectionString("Bastion") ?? string.Empty).Trim();
IHealthChecksBuilder salud = builder.Services.AddHealthChecks();

if (cadenaDeConexion.Length == 0)
{
    // Sin cadena de conexión no es que la base esté caída: es que este host no sabe a cuál
    // conectarse. Decirlo así ahorra media hora de mirar PostgreSQL.
    salud.AddCheck(
        nombreDeLaBase,
        () => HealthCheckResult.Unhealthy("Falta la cadena de conexión ConnectionStrings:Bastion."),
        tags: [etiquetaDeDisponibilidad]);
}
else
{
    builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(cadenaDeConexion));
    salud.AddCheck<ComprobacionDeBaseDeDatos>(nombreDeLaBase, tags: [etiquetaDeDisponibilidad]);
}

WebApplication app = builder.Build();

// Una línea por petición con su método, ruta, código y duración, en lugar de las tres que
// emite el host por su cuenta.
app.UseSerilogRequestLogging(opciones =>
    opciones.GetLevel = (contexto, _, excepcion) => excepcion is not null || contexto.Response.StatusCode >= 500
        ? LogEventLevel.Error
        : EsSonda(contexto.Request.Path) ? LogEventLevel.Verbose : LogEventLevel.Information);

// `Predicate = _ => false` no es un descuido: es la sonda de vida ejecutando CERO
// comprobaciones. Responde 200 si y solo si el proceso está en pie y atiende peticiones.
app.MapHealthChecks(rutaDeVida, new HealthCheckOptions { Predicate = _ => false });

app.MapHealthChecks(rutaDeDisponibilidad, new HealthCheckOptions
{
    Predicate = comprobacion => comprobacion.Tags.Contains(etiquetaDeDisponibilidad),
    ResponseWriter = EscribirEstadoDeLasDependencias,
});

app.Run();

static bool EsSonda(PathString ruta) => ruta.StartsWithSegments("/health");

// Un "Unhealthy" a secas obliga a bucear en los registros para saber QUÉ falla. Este
// cuerpo lo dice, que es justo lo que hace falta a las tres de la mañana.
static Task EscribirEstadoDeLasDependencias(HttpContext contexto, HealthReport informe) =>
    contexto.Response.WriteAsJsonAsync(new
    {
        estado = informe.Status.ToString(),
        duracionMs = Math.Round(informe.TotalDuration.TotalMilliseconds, 1),
        comprobaciones = informe.Entries.Select(entrada => new
        {
            nombre = entrada.Key,
            estado = entrada.Value.Status.ToString(),
            descripcion = entrada.Value.Description,
        }),
    });
