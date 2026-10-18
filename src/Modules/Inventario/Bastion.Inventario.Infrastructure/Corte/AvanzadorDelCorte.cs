using System.Globalization;
using Bastion.BuildingBlocks.Domain.Fechas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bastion.Inventario.Infrastructure.Corte;

/// <summary>
/// El trabajo mensual de Inventario: cada hora, avanza el corte de las instantáneas de cada empresa
/// hasta el mes anterior al de hoy, y cuadra las que avanza (ADR-0063 §7).
/// </summary>
/// <remarks>
/// <para>
/// <b>Cada hora y no una vez al mes</b>, con la forma de la purga de recibos. El avance no hace nada
/// si el corte ya llega, así que despertarse de más cuesta una consulta por empresa; y un trabajo
/// que solo corriera el día uno se perdería el mes entero si la API estuviera parada ese día.
/// </para>
/// <para>
/// <b>Cada empresa en su propio ámbito y su propio <c>try</c>.</b> El fallo de una se registra, cuenta
/// en su métrica y no para a las demás, y la hora siguiente lo reintenta. Si lo que falla es el
/// cuadre, el corte ya ha avanzado y la hora siguiente no lo repite: queda el error en el registro,
/// con la empresa.
/// </para>
/// <para>
/// <b>Nada sale de aquí hacia arriba</b>, por lo mismo que en la purga: una excepción que escapa de
/// un <see cref="BackgroundService"/> tumba el host, y una que se traga en silencio deja el trabajo
/// muerto sin que nadie lo sepa.
/// </para>
/// </remarks>
/// <param name="ambitos">Fábrica de ámbitos: el contexto es de ámbito y esto es un singleton.</param>
/// <param name="reloj">De dónde sale «hoy», y con él el mes que toca.</param>
/// <param name="metricas">Los contadores, la duración y el retraso del trabajo.</param>
/// <param name="registro">Dónde se anota lo que hace cada vuelta.</param>
internal sealed partial class AvanzadorDelCorte(
    IServiceScopeFactory ambitos,
    TimeProvider reloj,
    MetricasDelCorte metricas,
    ILogger<AvanzadorDelCorte> registro) : BackgroundService
{
    /// <summary>Cada cuánto se despierta.</summary>
    internal static readonly TimeSpan Intervalo = TimeSpan.FromHours(1);

    /// <summary>El mes al que tiene que llegar el corte en un instante: el anterior al de hoy, en UTC.</summary>
    /// <remarks>
    /// <b>El anterior, porque es el último que ya no cambia</b> por el paso del tiempo. El de hoy
    /// todavía recibe movimientos con fecha de hoy, y su instantánea sería una foto a medias.
    /// </remarks>
    /// <param name="ahora">El instante de la vuelta.</param>
    /// <returns>El primer día del mes anterior.</returns>
    internal static DateOnly MesQueToca(DateTimeOffset ahora)
    {
        DateOnly hoy = DiaDeNegocio.De(ahora);

        return new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(-1);
    }

    /// <summary>
    /// Los meses que le faltan al corte más viejo para llegar al mes que toca (ADR-0064 §3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Una empresa sin corte cuenta desde el mes anterior a su primer movimiento</b>, que es
    /// donde estaría su corte si no le faltara ninguna instantánea: no es una empresa al día, es
    /// una a la que el avance no ha conseguido avanzar nunca, y es la que más tiene que verse.
    /// </para>
    /// <para>
    /// <b>Un corte por delante del mes que toca no resta</b>: lo pone quien recalcula a mano, y no
    /// compensa el retraso de otra empresa.
    /// </para>
    /// </remarks>
    /// <param name="cortes">El corte de cada empresa con existencias.</param>
    /// <param name="mesQueToca">El primer día del mes al que tiene que llegar cada corte.</param>
    /// <returns>Los meses del más retrasado, o cero si todos llegan.</returns>
    internal static int MesesDeRetraso(IEnumerable<CorteDeUnaEmpresa> cortes, DateOnly mesQueToca)
    {
        int mayor = 0;

        foreach (CorteDeUnaEmpresa corte in cortes)
        {
            DateOnly? desde = corte.HastaElMes
                ?? (corte.PrimerMovimiento is { } primero
                    ? new DateOnly(primero.Year, primero.Month, 1).AddMonths(-1)
                    : null);

            if (desde is { } mes)
            {
                mayor = Math.Max(
                    mayor, ((mesQueToca.Year - mes.Year) * 12) + mesQueToca.Month - mes.Month);
            }
        }

        return mayor;
    }

    /// <summary>Una vuelta: todas las empresas con existencias, una a una.</summary>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>La tarea de la vuelta.</returns>
    internal async Task UnaVueltaAsync(CancellationToken cancelacion)
    {
        DateOnly mes = MesQueToca(reloj.GetUtcNow());
        string elMes = ElMes(mes);
        IReadOnlyList<Guid> empresas;

        using (IServiceScope ambito = ambitos.CreateScope())
        {
            empresas = await ambito.ServiceProvider
                .GetRequiredService<IAvanceDelCorte>()
                .EmpresasConExistenciasAsync(cancelacion)
                .ConfigureAwait(false);
        }

        int avanzadas = 0;
        int fallidas = 0;

        foreach (Guid empresaId in empresas)
        {
            try
            {
                if (await AvanzarAsync(empresaId, mes, elMes, cancelacion).ConfigureAwait(false))
                {
                    avanzadas++;
                }
            }
            catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
            {
                throw;
            }
#pragma warning disable CA1031 // El fallo de una empresa no para a las demás: se registra, se cuenta
            // y la hora siguiente lo reintenta.
            catch (Exception fallo)
#pragma warning restore CA1031
            {
                fallidas++;
                metricas.AnotarFallido();
                LaEmpresaFallo(registro, empresaId, elMes, fallo);
            }
        }

        Vuelta(registro, elMes, empresas.Count, avanzadas, fallidas);

        // Después de la vuelta y no antes: lo que importa es dónde se han quedado los cortes, no
        // dónde estaban. En su propio ámbito, como la lista.
        using (IServiceScope ambito = ambitos.CreateScope())
        {
            IReadOnlyList<CorteDeUnaEmpresa> cortes = await ambito.ServiceProvider
                .GetRequiredService<IAvanceDelCorte>()
                .CortesAsync(cancelacion)
                .ConfigureAwait(false);

            metricas.AnotarMesesDeRetraso(MesesDeRetraso(cortes, mes));
        }
    }

    /// <inheritdoc/>
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
            // escape de aquí tumba el host, y lo que no se registre deja el trabajo muerto en silencio.
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

    // Uno por empresa, en su propio ámbito: el contexto de una no arrastra lo que haya dejado a
    // medias la anterior.
    private async Task<bool> AvanzarAsync(
        Guid empresaId, DateOnly mes, string elMes, CancellationToken cancelacion)
    {
        using IServiceScope ambito = ambitos.CreateScope();

        AvanceHecho? hecho = await ambito.ServiceProvider
            .GetRequiredService<IAvanceDelCorte>()
            .AvanzarAsync(empresaId, mes, cancelacion)
            .ConfigureAwait(false);

        if (hecho is null)
        {
            return false;
        }

        metricas.AnotarAvanzada();
        metricas.AnotarDuracionDelAvance(hecho.DuracionDelAvance);

        int descuadres = hecho.Cuadre.Descuadres.Count;

        Avanzada(
            registro,
            empresaId,
            elMes,
            hecho.Repuestas,
            (long)hecho.DuracionDelAvance.TotalMilliseconds,
            hecho.Cuadre.ExistenciasComparadas,
            hecho.Cuadre.InstantaneasComparadas,
            (long)hecho.DuracionDelCuadre.TotalMilliseconds,
            descuadres);

        if (descuadres > 0)
        {
            metricas.AnotarDescuadres(descuadres);
            Descuadrada(
                registro,
                empresaId,
                elMes,
                descuadres,
                string.Join(
                    ", ",
                    hecho.Cuadre.Descuadres
                        .Select(descuadre => descuadre.Que)
                        .Distinct()
                        .Order(StringComparer.Ordinal)),
                hecho.Cuadre.ExistenciasComparadas,
                hecho.Cuadre.InstantaneasComparadas);
        }

        return true;
    }

    // El mes, escrito con su año delante y en cultura invariante: el registro se lee igual en un
    // servidor en castellano que en uno en inglés, y una fecha entera haría pensar en un día.
    private static string ElMes(DateOnly mes) => mes.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    [LoggerMessage(
        EventId = 8700,
        Level = LogLevel.Information,
        Message = "Avance del corte hasta {Mes}: {Empresas} empresas con existencias, {Avanzadas} " +
            "avanzadas y {Fallidas} con fallo.")]
    private static partial void Vuelta(ILogger registro, string mes, int empresas, int avanzadas, int fallidas);

    // La empresa va en el registro y no en la métrica: es lo que se mira cuando salta la alerta, y
    // en el registro no multiplica las series del recolector.
    [LoggerMessage(
        EventId = 8701,
        Level = LogLevel.Information,
        Message = "Corte de la empresa {EmpresaId} avanzado hasta {Mes}: {Repuestas} instantáneas " +
            "rehechas en {MilisegundosDelAvance} ms. El cuadre comparó {Existencias} existencias y " +
            "{Instantaneas} instantáneas en {MilisegundosDelCuadre} ms, con {Descuadres} descuadres.")]
    private static partial void Avanzada(
        ILogger registro,
        Guid empresaId,
        string mes,
        int repuestas,
        long milisegundosDelAvance,
        long existencias,
        long instantaneas,
        long milisegundosDelCuadre,
        int descuadres);

    [LoggerMessage(
        EventId = 8702,
        Level = LogLevel.Error,
        Message = "El avance del corte de la empresa {EmpresaId} hasta {Mes} ha fallado. Se " +
            "reintenta en la hora siguiente.")]
    private static partial void LaEmpresaFallo(ILogger registro, Guid empresaId, string mes, Exception fallo);

    [LoggerMessage(
        EventId = 8703,
        Level = LogLevel.Warning,
        Message = "El cuadre de la empresa {EmpresaId}, tras avanzar el corte hasta {Mes}, encontró " +
            "{Descuadres} descuadres ({Tipos}) en {Existencias} existencias y {Instantaneas} " +
            "instantáneas comparadas.")]
    private static partial void Descuadrada(
        ILogger registro,
        Guid empresaId,
        string mes,
        int descuadres,
        string tipos,
        long existencias,
        long instantaneas);

    [LoggerMessage(
        EventId = 8704,
        Level = LogLevel.Error,
        Message = "La vuelta del avance del corte ha fallado. Se reintenta en la hora siguiente.")]
    private static partial void LaVueltaFallo(ILogger registro, Exception fallo);
}
