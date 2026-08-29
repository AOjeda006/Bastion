using System.Text.Json;
using Bastion.BuildingBlocks.Application.Eventos;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Eventos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;

/// <summary>
/// El trabajo de fondo que vacía la bandeja de salida: la segunda cláusula del criterio del 0.8.
/// </summary>
/// <remarks>
/// <para>
/// <b>Entrega AL MENOS UNA VEZ, y está elegido.</b> La fila se marca como publicada <b>después</b>
/// de que todos sus manejadores hayan terminado. Al revés —marcar antes— el evento se perdería sin
/// dejar rastro en cuanto el proceso se cayera entre la marca y el manejador, y un evento perdido
/// no se recupera: nadie sabe que faltaba. Marcando después, el peor caso es que un manejador
/// reciba dos veces el mismo hecho, y para eso está la deduplicación del despachador. Se cambia
/// una pérdida irreparable y silenciosa por una repetición detectable y resuelta.
/// </para>
/// <para>
/// <b>Un solo publicador a la vez</b>, garantizado por un cerrojo consultivo de PostgreSQL y no por
/// suponer que solo hay una instancia desplegada (el porqué, entero, en
/// <see cref="CerrojoDeLaBandeja"/>). Quien no consigue el cerrojo no espera: se vuelve y lo
/// intenta en la vuelta siguiente.
/// </para>
/// <para>
/// <b>Sin petición no hay empresa activa</b>, así que cada vuelta abre un ámbito con
/// <see cref="MotivoSinInquilino.PublicacionDeEventos"/>. No es un rodeo del inquilinato: es el
/// único mecanismo que lo apaga a propósito, con motivo de una lista cerrada y anotado en el
/// registro. Sin el ámbito, la primera consulta a la cola lanzaría <c>FaltaLaEmpresaActiva</c>.
/// </para>
/// <para>
/// <b>Nada sale de aquí hacia arriba.</b> Una excepción que escape de un <see cref="BackgroundService"/>
/// se lleva el host por delante en cuanto el arranque termina; y una que se trague en silencio deja
/// el publicador muerto sin que nadie se entere. Las dos cosas están cerradas: el bucle captura y
/// registra, y el único caso en el que se detiene a propósito —el esquema no está— se registra
/// diciendo exactamente eso.
/// </para>
/// </remarks>
/// <param name="ambitos">Fábrica de ámbitos: los contextos son de ámbito y esto es un singleton.</param>
/// <param name="catalogo">Qué tipo hay detrás de cada nombre de la cola.</param>
/// <param name="opciones">Cada cuánto y cuántos.</param>
/// <param name="metricas">Dónde se apunta la edad del más viejo.</param>
/// <param name="reloj">De dónde sale el instante.</param>
/// <param name="registro">Dónde se anota lo que hace el publicador.</param>
internal sealed partial class PublicadorDeLaBandeja(
    IServiceScopeFactory ambitos,
    CatalogoDeEventos catalogo,
    OpcionesDeLaBandeja opciones,
    MetricasDeLaBandeja metricas,
    TimeProvider reloj,
    ILogger<PublicadorDeLaBandeja> registro) : BackgroundService
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Arranca(registro, opciones.Intervalo.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            bool seguir;

            try
            {
                seguir = await UnaVueltaAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // Este es EXACTAMENTE el sitio donde hay que capturarlo todo: lo que
            // escape de aquí tumba el host, y lo que no se registre deja el publicador muerto en
            // silencio. Un fallo de una vuelta no puede ser el fallo del proceso.
            catch (Exception fallo)
#pragma warning restore CA1031
            {
                LaVueltaFallo(registro, fallo);

                seguir = true;
            }

            if (!seguir)
            {
                return;
            }

            try
            {
                await Task.Delay(opciones.Intervalo, reloj, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    // Devuelve `false` cuando lo correcto es dejar de dar vueltas.
    private async Task<bool> UnaVueltaAsync(CancellationToken cancelacion)
    {
        using IServiceScope ambito = ambitos.CreateScope();

        IInquilinoActual inquilino = ambito.ServiceProvider.GetRequiredService<IInquilinoActual>();

        using IDisposable sinInquilino = inquilino.SinInquilino(MotivoSinInquilino.PublicacionDeEventos);

        ContextoDeLaBandeja contexto = ambito.ServiceProvider.GetRequiredService<ContextoDeLaBandeja>();
        CerrojoDeLaBandeja cerrojo = ambito.ServiceProvider.GetRequiredService<CerrojoDeLaBandeja>();
        IDespachadorDeEventos despachador = ambito.ServiceProvider.GetRequiredService<IDespachadorDeEventos>();

        if (!await cerrojo.TomarAsync(cancelacion).ConfigureAwait(false))
        {
            OtroEstaPublicando(registro);

            return true;
        }

        try
        {
            return await VaciarAsync(contexto, despachador, cancelacion).ConfigureAwait(false);
        }
        finally
        {
            await cerrojo.SoltarAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task<bool> VaciarAsync(
        ContextoDeLaBandeja contexto,
        IDespachadorDeEventos despachador,
        CancellationToken cancelacion)
    {
        List<EventoDeLaBandeja> lote;

        try
        {
            lote = await contexto.Bandeja
                .Where(fila => fila.Estado == EstadoDelEnvio.Pendiente)
                .OrderBy(fila => fila.Id)
                .Take(opciones.Tamano)
                .ToListAsync(cancelacion)
                .ConfigureAwait(false);
        }
        catch (Exception fallo) when (EsQueNoEstaLaTabla(fallo))
        {
            // El riesgo abierto del compose: nadie aplica las migraciones en el entorno local, así
            // que la tabla puede no existir. Sin esto serían dos errores por segundo desde el
            // arranque hasta que alguien apagara el contenedor —ruido que además esconde los
            // errores de verdad—. Se para, y se dice por qué: quien lea el registro sabe qué falta
            // y qué hacer. Quién aplica las migraciones en un despliegue es el ítem 0.13.
            NoEstaLaTabla(registro, ConfiguracionDeLaBandeja.Esquema, ConfiguracionDeLaBandeja.Tabla);

            return false;
        }

        metricas.AnotarAntiguedad(AntiguedadEnSegundos(lote));

        foreach (EventoDeLaBandeja fila in lote)
        {
            await PublicarAsync(contexto, despachador, fila, cancelacion).ConfigureAwait(false);
        }

        return true;
    }

    private async Task PublicarAsync(
        ContextoDeLaBandeja contexto,
        IDespachadorDeEventos despachador,
        EventoDeLaBandeja fila,
        CancellationToken cancelacion)
    {
        try
        {
            await despachador.DespacharAsync(Reconstruir(fila), cancelacion).ConfigureAwait(false);

            // DESPUÉS del despacho, nunca antes. Ver la decisión de entrega, arriba.
            fila.DarPorPublicado(reloj.GetUtcNow());

            await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

            metricas.AnotarPublicado();
        }
        catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // El fallo de UN evento no puede ser el fallo de la vuelta: los
        // demás de este lote tienen que salir igual. Qué falló se apunta en la fila.
        catch (Exception fallo)
#pragma warning restore CA1031
        {
            await AnotarElFalloAsync(contexto, fila, fallo).ConfigureAwait(false);
        }
    }

    private async Task AnotarElFalloAsync(
        ContextoDeLaBandeja contexto,
        EventoDeLaBandeja fila,
        Exception fallo)
    {
        bool aparcado = fila.AnotarFallo($"{fallo.GetType().Name}: {fallo.Message}");

        try
        {
            await contexto.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Si ni siquiera se puede apuntar el fallo, lo que queda es dejarlo
        // dicho en el registro. Volver a lanzar aquí tumbaría la vuelta entera por el evento que
        // ya había fallado.
        catch (Exception alApuntar)
#pragma warning restore CA1031
        {
            NoSePudoApuntar(registro, fila.Id, alApuntar);

            return;
        }

        if (aparcado)
        {
            metricas.AnotarAparcado();
            SeAparca(registro, fila.Id, fila.Nombre, EventoDeLaBandeja.IntentosAntesDeAparcar, fallo);

            return;
        }

        SeReintentara(registro, fila.Id, fila.Nombre, fila.Intentos, fallo);
    }

    private EventoDeIntegracion Reconstruir(EventoDeLaBandeja fila)
    {
        Type tipo = catalogo.TipoDe(fila.Nombre) ?? throw new InvalidOperationException(
            $"En la cola hay un evento «{fila.Nombre}» que ya no declara nadie. Un nombre que " +
            "deja de estar declarado es un contrato retirado con filas todavía dentro: o se " +
            "vuelve a declarar, o esas filas se resuelven a mano.");

        return (EventoDeIntegracion)JsonSerializer.Deserialize(fila.Cuerpo, tipo, s_json)!;
    }

    private double AntiguedadEnSegundos(List<EventoDeLaBandeja> lote) => lote.Count == 0
        ? 0
        : Math.Max(0, (reloj.GetUtcNow() - lote[0].OcurridoEn).TotalSeconds);

    // La excepción de PostgreSQL puede venir envuelta —EF Core envuelve las de guardado—, así que
    // se mira la cadena entera y no solo la de fuera.
    //
    // UN SOLO CÓDIGO, y es el que cubre los dos casos: contra una base a la que nadie ha aplicado
    // las migraciones no falta solo la tabla, falta el esquema entero, y aun así PostgreSQL
    // contesta a un SELECT con «undefined_table» (42P01) —«relation … does not exist»—, no
    // con «invalid_schema_name» (3F000). El 3F000 lo dan las órdenes que CREAN algo dentro de un
    // esquema que no está, y aquí no se crea nada. Comprobado contra la misma imagen que usan los
    // tests; lo fijan los dos casos de SinLaTablaElPublicadorSeParaTests.
    private static bool EsQueNoEstaLaTabla(Exception fallo)
    {
        for (Exception? actual = fallo; actual is not null; actual = actual.InnerException)
        {
            if (actual is PostgresException postgres
                && string.Equals(postgres.SqlState, PostgresErrorCodes.UndefinedTable, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    [LoggerMessage(
        EventId = 8300,
        Level = LogLevel.Information,
        Message = "Publicador de la bandeja de salida en marcha, mirando cada {Segundos} s.")]
    private static partial void Arranca(ILogger registro, double segundos);

    [LoggerMessage(
        EventId = 8301,
        Level = LogLevel.Debug,
        Message = "Otro publicador tiene el cerrojo; esta vuelta no hace nada.")]
    private static partial void OtroEstaPublicando(ILogger registro);

    [LoggerMessage(
        EventId = 8302,
        Level = LogLevel.Error,
        Message = "La vuelta del publicador ha fallado entera. Se reintenta en la siguiente.")]
    private static partial void LaVueltaFallo(ILogger registro, Exception fallo);

    [LoggerMessage(
        EventId = 8303,
        Level = LogLevel.Warning,
        Message = "La tabla {Esquema}.{Tabla} no existe: nadie ha aplicado las migraciones contra " +
                  "esta base. El publicador SE DETIENE en vez de fallar una vez por vuelta. " +
                  "Aplique las migraciones y reinicie el proceso.")]
    private static partial void NoEstaLaTabla(ILogger registro, string esquema, string tabla);

    [LoggerMessage(
        EventId = 8304,
        Level = LogLevel.Warning,
        Message = "El evento {Fila} ({Nombre}) ha fallado {Intentos} veces; se reintentará.")]
    private static partial void SeReintentara(
        ILogger registro, Guid fila, string nombre, int intentos, Exception fallo);

    [LoggerMessage(
        EventId = 8305,
        Level = LogLevel.Error,
        Message = "El evento {Fila} ({Nombre}) ha fallado {Intentos} veces seguidas y queda APARCADO. " +
                  "No se volverá a intentar: hay que mirarlo.")]
    private static partial void SeAparca(
        ILogger registro, Guid fila, string nombre, int intentos, Exception fallo);

    [LoggerMessage(
        EventId = 8306,
        Level = LogLevel.Error,
        Message = "No se ha podido ni apuntar el fallo del evento {Fila}.")]
    private static partial void NoSePudoApuntar(ILogger registro, Guid fila, Exception fallo);
}
