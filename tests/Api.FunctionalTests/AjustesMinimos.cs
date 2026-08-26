using System.Security.Cryptography;
using Bastion.Identidad.Infrastructure.Seguridad;
using Microsoft.AspNetCore.Hosting;

namespace Bastion.Api.FunctionalTests;

/// <summary>
/// Lo mínimo que hay que darle al host para que arranque sin dependencias externas.
/// </summary>
/// <remarks>
/// <para>
/// Las dos ausencias —PostgreSQL y recolector de telemetría— son EXPLÍCITAS y no heredadas del
/// entorno de quien ejecute los tests, para que el resultado no dependa de la máquina.
/// </para>
/// <para>
/// Los tres ajustes de JWT hacen falta porque el host <b>se niega a arrancar sin ellos</b>: un
/// valor por omisión para un secreto es un secreto conocido. La clave se genera al azar en este
/// proceso, no está escrita en ninguna parte y aquí no firma nada —estos tests no emiten tokens—;
/// solo sirve para que la validación del borde se pueda construir.
/// </para>
/// </remarks>
public static class AjustesMinimos
{
    private static string ClaveDeFirma { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    /// <summary>Aplica los ajustes al constructor del host de pruebas.</summary>
    /// <param name="builder">Constructor del host.</param>
    public static void Aplicar(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("ConnectionStrings:Bastion", string.Empty);
        builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", string.Empty);

        builder.UseSetting(OpcionesDeJwt.VariableDeEmisor, "https://bastion.pruebas");
        builder.UseSetting(OpcionesDeJwt.VariableDeAudiencia, "bastion-pruebas");
        builder.UseSetting(OpcionesDeJwt.VariableDeClave, ClaveDeFirma);
    }
}
