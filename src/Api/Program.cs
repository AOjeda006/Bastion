// Composition root de Bastion: el host único donde se cablean los módulos (§4 del plan
// maestro). La construcción del sistema vive aquí, separada de su uso
// (`principios/clean-architecture.md`); ningún módulo se registra a sí mismo por su cuenta.

using System.Text.Json.Serialization;
using Bastion.Api.Arranque;
using Bastion.Auditoria.Infrastructure;
using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Domain.Bloqueos;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Bastion.BuildingBlocks.Infrastructure.Errores;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.BuildingBlocks.Infrastructure.Multiempresa;
using Bastion.BuildingBlocks.Infrastructure.Salud;
using Bastion.Identidad.Contracts;
using Bastion.Identidad.Infrastructure;
using Bastion.Identidad.Infrastructure.Seguridad;
using Bastion.Organizacion.Contracts;
using Bastion.Organizacion.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

// El modo MIGRADOR se decide antes de construir nada. El DDL no lo aplica el arranque de la API:
// lo aplica este mismo artefacto invocado con `--migrar`, que migra y sale (`MigradorDeArranque`,
// y el porqué entero en `docs/adr/adr-0021`). Aquí solo se mira si toca.
bool migrarYSalir = MigradorDeArranque.LoPiden(args);

WebApplicationBuilder builder = WebApplication.CreateBuilder(MigradorDeArranque.SinElArgumento(args));

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

        // La bandeja de salida publica la EDAD del evento pendiente más antiguo (0.8). Es lo que
        // distingue «mil eventos que salen en dos segundos» de «uno atascado desde ayer», que el
        // tamaño de la cola no distingue. No está en ninguna sonda a propósito: el porqué, en
        // `MetricasDeLaBandeja`.
        metricas.AddMeter(MetricasDeLaBandeja.Medidor);

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
// -------------------------------------------------------------- política de errores
// UNA política central para todo lo que sale como error (§9). No hay try/catch por
// controlador: un manejador por endpoint no cubre lo que pasa donde no hay endpoint.
builder.Services.AgregarPoliticaDeErrores();

string cadenaDeConexion = (builder.Configuration.GetConnectionString("Bastion") ?? string.Empty).Trim();

// La clave de firma y el par emisor/audiencia se leen ANTES de registrar nada, y sin valor por
// omisión: si falta cualquiera de las tres, esto lanza y la aplicación no llega a escuchar. Un
// secreto con valor por omisión es un secreto conocido, y el despliegue que se olvidó de ponerlo
// es exactamente el que lo conservaría.
var opcionesDeJwt = OpcionesDeJwt.De(
    builder.Configuration[OpcionesDeJwt.VariableDeEmisor],
    builder.Configuration[OpcionesDeJwt.VariableDeAudiencia],
    builder.Configuration[OpcionesDeJwt.VariableDeClave]);

// Cuánto dura un bloqueo del art. 32 aquí. A diferencia de las tres de arriba, esta SÍ tiene valor
// por omisión —seis años, el del art. 30 del Código de Comercio— porque un plazo por omisión es una
// decisión defendible y escrita, no un secreto conocido. Lo que no vale es ponerla y equivocarse:
// eso lanza aquí, antes de escuchar.
var retencion = PoliticaDeRetencion.De(builder.Configuration[PoliticaDeRetencion.VariableDelPlazo]);
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

// ------------------------------------------------------------------------- modulos
// Cada modulo se registra AQUI y solo aqui: la construccion del sistema esta separada de
// su uso. Se registra aunque la cadena venga vacia, porque `dotnet ef migrations add` monta
// este host para descubrir el DbContext y generar la migracion no abre ninguna conexion.
// R8. Va delante de los modulos porque los DbContext de los dos dependen de el: sin esto, el host
// no resuelve ni un contexto y no atiende la primera peticion. La empresa por la que filtra cada
// consulta sale de aqui, del claim, y de ningun otro sitio.
builder.Services.AgregarInquilinato();

// R11. Tambien delante: registra el interceptor que cada modulo con persistencia engancha a su
// DbContext en la linea siguiente. Enchufarlo es cosa de cada modulo, a la vista en su
// `AddDbContext`; lo que se registra aqui es el servicio, una sola vez.
builder.Services.AgregarAuditoria();

// R12. También delante, y por lo mismo: registra el interceptor que llena la bandeja de salida y
// que cada módulo con persistencia engancha a su DbContext en la línea siguiente.
//
// El trabajo de fondo que la VACÍA solo se registra si hay base de datos a la que conectarse. Sin
// cadena de conexión —los tests funcionales levantan el host entero sin dependencia ninguna— un
// publicador sondeando sería un error por vuelta desde el arranque, y ese ruido esconde los
// errores de verdad.
builder.Services.AgregarBandejaDeSalida(publica: cadenaDeConexion.Length > 0);

// R10. Pone el filtro de idempotencia en la tubería de MVC, para TODAS las acciones: solo unas
// pocas admiten la cabecera, pero el filtro tiene que ver también las que no para poder
// contestarles 400 en vez de tragarse la cabecera y dejar al cliente creyendo que su reintento
// está protegido. El almacén de cada módulo se registra en la línea de ese módulo, con su clave.
builder.Services.AgregarIdempotencia();

builder.Services.AgregarModuloDeAuditoria(cadenaDeConexion);
builder.Services.AgregarModuloDeOrganizacion(cadenaDeConexion, retencion);
builder.Services.AgregarModuloDeIdentidad(cadenaDeConexion, opcionesDeJwt);

// --------------------------------------------------------------------- autenticación
// Quién es quien llama, leído del token de acceso y de ningún otro sitio. Las cuatro
// comprobaciones van juntas y todas encendidas: la FIRMA dice que lo emitimos nosotros, el
// EMISOR y la AUDIENCIA que lo emitimos para esta aplicación, y la CADUCIDAD que sigue
// vigente. Apagar cualquiera de las tres últimas deja la firma validando tokens que no son
// para aquí — y un sistema que valida firmas parece seguro.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opciones =>
    {
        // Sin esto, la pila de Microsoft TRADUCE los nombres entrantes: `sub` se convierte en la
        // URI larga de ClaimTypes.NameIdentifier y buscar `sub` no encuentra nada. El emisor
        // escribe `sub`; aquí se lee `sub`.
        opciones.MapInboundClaims = false;

        opciones.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = opcionesDeJwt.Clave,
            ValidateIssuer = true,
            ValidIssuer = opcionesDeJwt.Emisor,
            ValidateAudience = true,
            ValidAudience = opcionesDeJwt.Audiencia,
            ValidateLifetime = true,

            // Por omisión la biblioteca regala CINCO MINUTOS de gracia sobre la caducidad. Con un
            // token de quince minutos, eso es un tercio de su vida: un token revocado por
            // caducidad seguiría entrando un tercio de tiempo más.
            ClockSkew = TimeSpan.Zero,

            NameClaimType = ClaimsDeBastion.Nombre,
        };
    });

// ---------------------------------------------------------------------- autorización
// El catálogo se compone AQUÍ con lo que declara cada módulo. Identidad valida contra él los
// permisos de un rol sin ver a los otros quince módulos (§4).
builder.Services.AgregarAutorizacionPorPermisos(
    [.. PermisosDeOrganizacion.Todos, .. PermisosDeIdentidad.Todos]);

// DENEGAR POR DEFECTO. La política de respaldo se aplica a todo endpoint que no traiga metadatos
// de autorización propios, así que olvidarse de poner el atributo CIERRA la puerta en vez de
// abrirla. Lo contrario —abierto salvo que se marque— es la clase de descuido que no se ve en
// ninguna revisión: el endpoint nuevo funciona, y funciona para cualquiera.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// ---------------------------------------------------------------------- controladores
// Los controladores viven en los proyectos Endpoints de cada módulo, no aquí. `AddControllers`
// descubre los del ensamblado de entrada y los de los que referencia, así que basta con que el
// host los referencie (ver Bastion.Api.csproj).
//
// Los enumerados se serializan como TEXTO: un ordinal es un contrato que se rompe solo con
// reordenar el enumerado, y el que lo reordena no ve que está rompiendo un cliente.
builder.Services
    .AddControllers()
    .AddJsonOptions(json => json.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Las URL que genera el enrutador van en minúsculas. Sin esto, el token `[controller]` toma el
// nombre de la clase tal cual y el `Location` de una creación sale como
// `/api/v1/organizacion/Empresas/…`: el nombre de un tipo de C# asomando en el contrato público,
// y una ruta que no coincide con la que está escrita en el OpenAPI ni en la documentación.
builder.Services.AddRouting(rutas => rutas.LowercaseUrls = true);

// ---------------------------------------------------------------------------- OpenAPI
// El contrato de la API, generado de las descripciones que ya publica ASP.NET Core. De aquí
// sale `docs/api/openapi.json` y de ese fichero sale el cliente de TypeScript del frontal: el
// contrato se escribe UNA vez, en los controladores y los DTO, y todo lo demás se deriva.
//
// Se registra el servicio pero NO se mapea el endpoint: el documento se genera al compilar
// (ver Bastion.Api.csproj) y no se sirve por HTTP. Un `/openapi/v1.json` anónimo sería el único
// hueco de una API que deniega por defecto, y uno autenticado no serviría para generar nada.
builder.Services.AgregarContratoDeLaApi();

WebApplication app = builder.Build();

// Se sale AQUÍ, antes de montar la tubería y antes de sembrar: el migrador no atiende peticiones
// y no crea datos. Aplica el DDL que falte, dice en el registro qué ha aplicado y devuelve el
// código de salida que el compose lee para decidir si arranca la API.
if (migrarYSalir)
{
    return await app.MigrarYSalirAsync();
}

// Lo PRIMERO de la tubería: un manejador de excepciones solo cubre lo que tiene por dentro.
// Y va por fuera del registro de peticiones a propósito: si fuera al revés, cada 500 se
// registraría dos veces con su traza entera, una por cada uno.
app.UsarPoliticaDeErrores();

// Una línea por petición con su método, ruta, código y duración, en lugar de las tres que
// emite el host por su cuenta.
app.UseSerilogRequestLogging(opciones =>
    opciones.GetLevel = (contexto, _, excepcion) => excepcion is not null || contexto.Response.StatusCode >= 500
        ? LogEventLevel.Error
        : EsSonda(contexto.Request.Path) ? LogEventLevel.Verbose : LogEventLevel.Information);

// `Predicate = _ => false` no es un descuido: es la sonda de vida ejecutando CERO
// comprobaciones. Responde 200 si y solo si el proceso está en pie y atiende peticiones.
// Autenticar va SIEMPRE antes de autorizar: la segunda decide sobre el usuario que ha
// reconstruido la primera. Al revés, la autorización miraría un principal anónimo y respondería
// 401 a todo el mundo, token o no.
app.UseAuthentication();
app.UseAuthorization();

// `AllowAnonymous` explícito en las dos sondas, y NO por comodidad: con la política de respaldo
// puesta, un orquestador que consulta /health/live sin token recibiría 401 y reiniciaría el
// contenedor en bucle. No exponen nada: la de vida no ejecuta ninguna comprobación y la de
// disponibilidad solo dice si la base responde.
app.MapHealthChecks(rutaDeVida, new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();

app.MapHealthChecks(rutaDeDisponibilidad, new HealthCheckOptions
{
    Predicate = comprobacion => comprobacion.Tags.Contains(etiquetaDeDisponibilidad),
    ResponseWriter = EscribirEstadoDeLasDependencias,
}).AllowAnonymous();

app.MapControllers();

// Sin cadena de conexión no hay base a la que sembrar: es el caso de los tests funcionales, que
// levantan el host entero sin dependencia externa ninguna.
if (cadenaDeConexion.Length > 0)
{
    await app.SembrarAsync();
}

await app.RunAsync();

return 0;

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
