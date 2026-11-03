using Bastion.BuildingBlocks.Application.Multiempresa;
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
/// <b>Un ámbito por evento, aparte del de la vuelta</b> (3.4, hueco 1). El de la vuelta guarda el
/// cerrojo, que es de su conexión, y lee el lote sin rastrearlo. Cada evento se marca después en
/// un contexto suyo, que relee su fila: lo que un evento deja a medias en un contexto —un fallo que
/// no se pudo apuntar, una marca que no se pudo guardar— no lo guarda el siguiente con la suya.
/// </para>
/// <para>
/// <b>Un choque de versión no cuenta</b> (3.4, hueco 5). El despachador ya lo ha reintentado en el
/// acto; si aun así un manejador sigue chocando, la fila se queda pendiente como estaba, sin
/// intento ni error, y la vuelta siguiente lo vuelve a probar. Un choque no aparca nunca.
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
/// <param name="despachador">Quién entrega cada evento a sus manejadores.</param>
/// <param name="opciones">Cada cuánto y cuántos.</param>
/// <param name="metricas">Dónde se apunta la edad del más viejo.</param>
/// <param name="reloj">De dónde sale el instante.</param>
/// <param name="registro">Dónde se anota lo que hace el publicador.</param>
internal sealed partial class PublicadorDeLaBandeja(
    IServiceScopeFactory ambitos,
    IDespachadorDeEventos despachador,
    OpcionesDeLaBandeja opciones,
    MetricasDeLaBandeja metricas,
    TimeProvider reloj,
    ILogger<PublicadorDeLaBandeja> registro) : BackgroundService
{
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

        if (!await cerrojo.TomarAsync(cancelacion).ConfigureAwait(false))
        {
            OtroEstaPublicando(registro);

            return true;
        }

        try
        {
            return await VaciarAsync(contexto, cancelacion).ConfigureAwait(false);
        }
        finally
        {
            await cerrojo.SoltarAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task<bool> VaciarAsync(ContextoDeLaBandeja contexto, CancellationToken cancelacion)
    {
        List<EventoDeLaBandeja> lote;
        int aparcados;

        try
        {
            // Sin rastrear: este contexto no escribe en la cola. Cada fila se relee y se marca en
            // el contexto de su evento.
            lote = await contexto.Bandeja
                .AsNoTracking()
                .Where(fila => fila.Estado == EstadoDelEnvio.Pendiente)
                .OrderBy(fila => fila.Id)
                .Take(opciones.Tamano)
                .ToListAsync(cancelacion)
                .ConfigureAwait(false);

            // De todas las empresas, porque la vuelta corre sin inquilino; y de la base, porque un
            // aparcado sigue sin resolver aunque el proceso se reinicie (3.4, hueco 4). Lo que se
            // aparque en esta vuelta lo cuenta la siguiente.
            aparcados = await contexto.Bandeja
                .CountAsync(fila => fila.Estado == EstadoDelEnvio.Aparcado, cancelacion)
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
        metricas.AnotarAparcadosSinResolver(aparcados);

        foreach (EventoDeLaBandeja fila in lote)
        {
            await PublicarAsync(fila, cancelacion).ConfigureAwait(false);
        }

        return true;
    }

    private async Task PublicarAsync(EventoDeLaBandeja fila, CancellationToken cancelacion)
    {
        try
        {
            await despachador.DespacharAsync(fila, cancelacion).ConfigureAwait(false);

            // DESPUÉS del despacho, nunca antes. Ver la decisión de entrega, arriba.
            await using (AsyncServiceScope ambito = ambitos.CreateAsyncScope())
            {
                ContextoDeLaBandeja contexto = ambito.ServiceProvider.GetRequiredService<ContextoDeLaBandeja>();

                EventoDeLaBandeja enLaCola = await ReleerAsync(contexto, fila.Id, cancelacion).ConfigureAwait(false);

                enLaCola.DarPorPublicado(reloj.GetUtcNow());

                await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
            }

            metricas.AnotarPublicado();
        }
        catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
        {
            throw;
        }
        catch (DbUpdateConcurrencyException choque)
        {
            // Un manejador sigue chocando después de los reintentos en el acto (3.4, hueco 5): no es
            // un fallo del evento sino de su momento, así que no cuenta intento y no lo puede
            // aparcar. La fila se queda como estaba, y la vuelta siguiente lo vuelve a probar. Si
            // chocara siempre, lo dirían este registro y la edad del más viejo, que sube.
            SigueChocando(registro, fila.Id, fila.Nombre, EventoDeLaBandeja.ReintentosPorChoque, choque);
        }
#pragma warning disable CA1031 // El fallo de UN evento no puede ser el fallo de la vuelta: los
        // demás de este lote tienen que salir igual. Qué falló se apunta en la fila.
        catch (Exception fallo)
#pragma warning restore CA1031
        {
            await AnotarElFalloAsync(fila, fallo).ConfigureAwait(false);
        }
    }

    // En un ámbito suyo, y no en el de la publicación: si lo que falló fue guardar la marca, ese
    // contexto tiene la fila como publicada, y apuntar ahí el fallo la guardaría publicada y con un
    // error, dos cosas que no van juntas. Releída, sigue pendiente, y la vuelta siguiente la marca.
    private async Task AnotarElFalloAsync(EventoDeLaBandeja fila, Exception fallo)
    {
        bool aparcado;
        int intentos;

        try
        {
            await using AsyncServiceScope ambito = ambitos.CreateAsyncScope();

            ContextoDeLaBandeja contexto = ambito.ServiceProvider.GetRequiredService<ContextoDeLaBandeja>();

            EventoDeLaBandeja enLaCola =
                await ReleerAsync(contexto, fila.Id, CancellationToken.None).ConfigureAwait(false);

            aparcado = enLaCola.AnotarFallo($"{fallo.GetType().Name}: {fallo.Message}");
            intentos = enLaCola.Intentos;

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

        SeReintentara(registro, fila.Id, fila.Nombre, intentos, fallo);
    }

    private static Task<EventoDeLaBandeja> ReleerAsync(
        ContextoDeLaBandeja contexto,
        Guid filaId,
        CancellationToken cancelacion) =>
        contexto.Bandeja.SingleAsync(enLaCola => enLaCola.Id == filaId, cancelacion);

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

    [LoggerMessage(
        EventId = 8307,
        Level = LogLevel.Warning,
        Message = "El evento {Fila} ({Nombre}) sigue chocando con otra escritura tras {Reintentos} " +
                  "reintentos en el acto. Se queda pendiente sin contar intento, para la vuelta siguiente.")]
    private static partial void SigueChocando(
        ILogger registro, Guid fila, string nombre, int reintentos, Exception choque);
}
