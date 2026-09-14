using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bastion.Auditoria.Infrastructure.Recibos;

/// <summary>
/// El trabajo de fondo que pasa la <see cref="PurgaDeRecibosCaducados"/> al arrancar y después cada
/// hora.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cada hora, y de ahí sale la cifra que se promete</b>: un recibo vence a las 24 horas y la
/// siguiente vuelta lo borra, así que dura al menos 24 y como mucho 25 mientras la API esté en marcha.
/// Parada no purga; la primera vuelta al arrancar se lleva lo que se quedó pendiente.
/// </para>
/// <para>
/// <b>Nada sale de aquí hacia arriba</b>, por lo mismo que en el publicador de la bandeja: una
/// excepción que escapa de un <see cref="BackgroundService"/> tumba el host, y una que se traga en
/// silencio deja la purga muerta sin que nadie lo sepa. El bucle captura, registra y vuelve a
/// intentarlo en la hora siguiente.
/// </para>
/// </remarks>
/// <param name="ambitos">Fábrica de ámbitos: el contexto es de ámbito y esto es un singleton.</param>
/// <param name="reloj">De dónde sale el instante de cada vuelta.</param>
/// <param name="registro">Dónde se anota lo que hace la purga.</param>
internal sealed partial class PurgadorDeRecibos(
    IServiceScopeFactory ambitos,
    TimeProvider reloj,
    ILogger<PurgadorDeRecibos> registro) : BackgroundService
{
    /// <summary>Cada cuánto se purga.</summary>
    internal static readonly TimeSpan Intervalo = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UnaVueltaAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // Este es EXACTAMENTE el sitio donde hay que capturarlo todo: lo que
            // escape de aquí tumba el host, y lo que no se registre deja la purga muerta en silencio.
            catch (Exception fallo)
#pragma warning restore CA1031
            {
                LaVueltaFallo(registro, fallo);
            }

            try
            {
                await Task.Delay(Intervalo, reloj, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task UnaVueltaAsync(CancellationToken cancelacion)
    {
        using IServiceScope ambito = ambitos.CreateScope();
        PurgaDeRecibosCaducados purga = ambito.ServiceProvider.GetRequiredService<PurgaDeRecibosCaducados>();

        int borrados = await purga.PurgarAsync(reloj.GetUtcNow(), cancelacion).ConfigureAwait(false);

        Purgados(registro, borrados);
    }

    // Solo el número: ni empresas, ni rutas, ni claves. Lo que se borra es precisamente lo que no se
    // quiere seguir guardando, y copiarlo al registro sería guardarlo en otro sitio.
    [LoggerMessage(
        EventId = 8600,
        Level = LogLevel.Information,
        Message = "Purga de recibos de idempotencia: {Borrados} caducados borrados.")]
    private static partial void Purgados(ILogger registro, int borrados);

    [LoggerMessage(
        EventId = 8601,
        Level = LogLevel.Error,
        Message = "La purga de recibos de idempotencia ha fallado. Se reintenta en la hora siguiente.")]
    private static partial void LaVueltaFallo(ILogger registro, Exception fallo);
}
