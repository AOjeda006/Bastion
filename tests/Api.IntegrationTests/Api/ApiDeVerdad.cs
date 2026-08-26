using System.Security.Cryptography;
using Bastion.Api.Arranque;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.Identidad.Infrastructure.Seguridad;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bastion.Api.IntegrationTests.Api;

/// <summary>
/// La API real —su <c>Program.cs</c>, su contenedor y su autorización— contra el PostgreSQL del
/// contenedor de pruebas.
/// </summary>
/// <remarks>
/// <para>
/// <b>No se sustituye ni un solo servicio del contenedor.</b> Lo único que se inyecta es
/// configuración, por el sitio por el que la configuración entra de verdad. En cuanto se reemplaza
/// un registro, lo que se prueba deja de ser el sistema que se despliega — y la autorización es
/// justo donde ese atajo convierte el test en una ceremonia: un manejador de permisos falso da
/// verde con la cadena real rota.
/// </para>
/// <para>
/// <b>Ni la clave de firma ni la contraseña de la semilla están escritas en ninguna parte.</b> Las
/// dos se generan al azar al construir la fábrica y viven en memoria mientras dura el proceso. Un
/// secreto escrito en un test es un secreto escrito, aunque el test sea de mentira: acaba copiado
/// a un `appsettings` el día que alguien quiere «probarlo en local».
/// </para>
/// </remarks>
public sealed class ApiDeVerdad(PostgresConTodosLosModulos postgres) : WebApplicationFactory<Program>
{
    /// <summary>NIF de la empresa que crea la semilla. Válido, con su carácter de control.</summary>
    public const string NifDeLaSemilla = "99999999R";

    /// <summary>Correo de la cuenta que crea la semilla.</summary>
    public const string CorreoDelAdministrador = "administracion@bastion.pruebas";

    /// <summary>
    /// Contraseña de la semilla, generada en este proceso y solo para él.
    /// </summary>
    /// <remarks>
    /// <b>Estática, una por proceso, y no una por fábrica.</b> La semilla solo se aplica mientras
    /// no hay ningún usuario: la crea el primer host que arranque, y los siguientes se la
    /// encuentran hecha. Con una contraseña por instancia, la segunda clase de tests intentaría
    /// entrar con una que nunca se guardó y recibiría 401 sin que nada estuviera roto.
    /// </remarks>
    public static string ContrasenaDelAdministrador { get; } =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));

    /// <summary>Emisor que se le configura al host de pruebas.</summary>
    public const string Emisor = "https://bastion.pruebas";

    /// <summary>Audiencia que se le configura al host de pruebas.</summary>
    public const string Audiencia = "bastion-pruebas";

    /// <summary>
    /// La clave con la que firma este proceso.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Una sola por proceso: un token emitido por un host tiene que valer en otro, que es lo que
    /// pasa cuando dos clases de test comparten la base.
    /// </para>
    /// <para>
    /// Está expuesta para <see cref="TokenForjado"/>, que la usa para fabricar tokens que el borde
    /// TIENE QUE RECHAZAR —caducados, con otro emisor, con otra audiencia—. No hay manera de
    /// comprobar que se valida la caducidad sin presentar uno caducado. Lo que ningún test hace es
    /// fabricar un token válido para saltarse el inicio de sesión.
    /// </para>
    /// </remarks>
    public static string ClaveDeFirma { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    /// <summary>
    /// Un cliente HTTP con la dirección base en <c>https</c>.
    /// </summary>
    /// <remarks>
    /// El <c>https</c> NO es decorativo: la cookie del refresco lleva <c>Secure</c> y el prefijo
    /// <c>__Host-</c>, así que un cliente con dirección base <c>http://localhost</c> la descarta al
    /// recibirla y no la devuelve nunca. Los tests de rotación darían todos 401 por un motivo que
    /// no tiene nada que ver con lo que prueban.
    /// </remarks>
    public HttpClient CrearCliente() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
    });

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("ConnectionStrings:Bastion", postgres.CadenaDeConexion);

        // Apagado EXPLÍCITAMENTE y no heredado del entorno: en una máquina con el recolector
        // configurado, cada test intentaría exportar a un sitio que no está.
        builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", string.Empty);

        builder.UseSetting(OpcionesDeJwt.VariableDeEmisor, Emisor);
        builder.UseSetting(OpcionesDeJwt.VariableDeAudiencia, Audiencia);
        builder.UseSetting(OpcionesDeJwt.VariableDeClave, ClaveDeFirma);

        builder.UseSetting(SemillaDeArranque.VariableDeCorreo, CorreoDelAdministrador);
        builder.UseSetting(SemillaDeArranque.VariableDeContrasena, ContrasenaDelAdministrador);
        builder.UseSetting(SemillaDeArranque.VariableDeNif, NifDeLaSemilla);
        builder.UseSetting(SemillaDeArranque.VariableDeRazonSocial, "Semilla de pruebas, S.L.");
        builder.UseSetting(SemillaDeArranque.VariableDeCalle, "Calle de la Semilla");
        builder.UseSetting(SemillaDeArranque.VariableDeCodigoPostal, "28001");
        builder.UseSetting(SemillaDeArranque.VariableDePoblacion, "Madrid");
    }
}
