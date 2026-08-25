using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Bastion.BuildingBlocks.Infrastructure.Salud;

/// <summary>
/// Comprueba que PostgreSQL acepta conexiones y responde a una consulta.
/// </summary>
/// <remarks>
/// Abre una conexión y ejecuta <c>SELECT 1</c>. No vale con mirar si el puerto está abierto:
/// una base que acepta el saludo TCP pero está recuperándose no puede atender tráfico, y una
/// sonda que dijera "sano" ahí es peor que no tenerla.
///
/// Solo la usa la sonda de DISPONIBILIDAD. La de vida no mira dependencias a propósito.
/// </remarks>
public sealed class ComprobacionDeBaseDeDatos(NpgsqlDataSource origen) : IHealthCheck
{
    /// <summary>
    /// Margen de la comprobación. Corto y propio: una sonda que tarda lo que el tiempo de
    /// espera de la cadena de conexión deja de ser una sonda y pasa a ser parte del problema.
    /// </summary>
    private static readonly TimeSpan s_margen = TimeSpan.FromSeconds(3);

    /// <inheritdoc />
    // Los nombres de los parámetros son los de `IHealthCheck`, no los del proyecto: en una
    // implementación de interfaz el llamador puede usarlos con nombre (regla CA1725).
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(s_margen);

        try
        {
            await using NpgsqlConnection conexion = await origen.OpenConnectionAsync(limite.Token);
            await using NpgsqlCommand consulta = conexion.CreateCommand();
            consulta.CommandText = "SELECT 1";
            _ = await consulta.ExecuteScalarAsync(limite.Token);

            return HealthCheckResult.Healthy("PostgreSQL acepta conexiones y responde.");
        }
        catch (OperationCanceledException excepcion) when (!cancellationToken.IsCancellationRequested)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                $"PostgreSQL no ha respondido en {s_margen.TotalSeconds:0} s.",
                excepcion);
        }
        catch (NpgsqlException excepcion)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "PostgreSQL no responde.",
                excepcion);
        }
    }
}
