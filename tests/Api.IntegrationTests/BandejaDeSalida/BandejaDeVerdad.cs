using System.Collections.Concurrent;
using System.Text.Json;
using Bastion.BuildingBlocks.Application.Eventos;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Eventos;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.BuildingBlocks.Infrastructure.Multiempresa;
using Bastion.Organizacion.Contracts.Empresas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bastion.Api.IntegrationTests.BandejaDeSalida;

/// <summary>
/// La bandeja de salida montada con <b>su cableado de producción</b> y con manejadores de prueba.
/// </summary>
/// <remarks>
/// <para>
/// No es un doble de nada: usa <see cref="CableadoDeLaBandeja.AgregarBandejaDeSalida"/>, el
/// publicador de verdad, el despachador de verdad, el cerrojo de verdad y el mismo contexto contra
/// el PostgreSQL del contenedor. Lo único que cambia respecto al host de la API es <b>qué
/// manejadores hay registrados</b> —en la fase 0 no hay ninguno— y el intervalo del sondeo, que
/// se baja para que un test no tarde dos segundos por vuelta.
/// </para>
/// <para>
/// <b>Y el inquilino es el de producción</b>, con el <c>HttpContext</c> a nulo: fuera de un ámbito
/// sin inquilino, pedir la empresa del filtro LANZA. Es lo que convierte «el publicador abre su
/// ámbito» en algo comprobable — si esa línea desaparece, esto no publica nada.
/// </para>
/// <para>
/// <b>Desde el 3.4 hay además un módulo de prueba</b>, <see cref="ContextoDePrueba"/>, con los tres
/// interceptores de los módulos de verdad y en su orden: es donde escriben los manejadores con
/// efecto. Su tabla solo existe en la base en la que alguien la crea, así que registrarlo aquí no
/// le cuesta nada a quien no lo usa.
/// </para>
/// </remarks>
internal sealed class BandejaDeVerdad : IAsyncDisposable
{
    private readonly ServiceProvider _servicios;

    /// <summary>Monta el contenedor con lo que la bandeja necesita.</summary>
    /// <param name="cadenaDeConexion">Base de datos contra la que corre.</param>
    /// <param name="publica">Si arranca el trabajo de fondo que vacía la cola.</param>
    /// <param name="manejadores">Qué manejadores se registran, si hace falta alguno.</param>
    /// <param name="intervalo">
    /// Cada cuánto mira la cola. Por omisión, cien milisegundos: lo justo para que un test no
    /// espere dos segundos por vuelta. Un test que necesite mirar la cola MIENTRAS hay algo
    /// pendiente lo sube, porque con vueltas muy rápidas la ventana se cierra enseguida.
    /// </param>
    public BandejaDeVerdad(
        string cadenaDeConexion,
        bool publica,
        Action<IServiceCollection>? manejadores = null,
        TimeSpan? intervalo = null)
    {
        ServiceCollection servicios = new();

        servicios.AddLogging(registro => registro
            .SetMinimumLevel(LogLevel.Debug)
            .AddProvider(Registro));

        servicios.AgregarInquilinato();
        servicios.AgregarAuditoria();

        // ANTES del cableado: allí se registra con `TryAdd`, así que este gana. Cien milisegundos
        // en vez de dos segundos; lo demás es lo que se despliega.
        servicios.AddSingleton(new OpcionesDeLaBandeja
        {
            Intervalo = intervalo ?? TimeSpan.FromMilliseconds(100),
        });

        servicios.AgregarBandejaDeSalida(publica);

        servicios.AddDbContext<ContextoDeLaBandeja>(opciones => opciones
            .UseNpgsql(cadenaDeConexion)
            .UseSnakeCaseNamingConvention());

        // Como los módulos: la traza, la marca de tiempo y los eventos, en este orden.
        servicios.AddDbContext<ContextoDePrueba>((alcance, opciones) => opciones
            .UseNpgsql(cadenaDeConexion)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(
                alcance.GetRequiredService<InterceptorDeAuditoria>(),
                alcance.GetRequiredService<InterceptorDeMarcasDeTiempo>(),
                alcance.GetRequiredService<InterceptorDeLaBandeja>()));

        // Los eventos que puede haber en la cola de esta base: el de verdad —que lo deja ahí
        // cualquier otro test que dé de alta una empresa— y los de prueba. Sin el primero, este
        // publicador se encontraría filas con un nombre que no conoce y las iría aparcando.
        servicios.DeclararEvento<EmpresaCreada>(EmpresaCreada.Nombre);
        servicios.DeclararEvento<HechoDePrueba>(HechoDePrueba.Nombre);
        servicios.DeclararEvento<RespuestaDePrueba>(RespuestaDePrueba.Nombre);

        manejadores?.Invoke(servicios);

        _servicios = servicios.BuildServiceProvider();
    }

    /// <summary>La empresa de las filas que encolan y despachan los casos, mientras no digan otra.</summary>
    public static Guid EmpresaDePrueba { get; } = Guid.Parse("0196f3a0-7c00-7000-8000-000000003401");

    /// <summary>Lo que ha ido escribiendo en el registro, para poder mirarlo.</summary>
    public RegistroDeLaBandeja Registro { get; } = new();

    /// <summary>El contenedor, para pedirle el despachador o el contexto.</summary>
    public IServiceProvider Servicios => _servicios;

    /// <summary>Arranca los trabajos de fondo que haya registrados.</summary>
    public async Task ArrancarAsync()
    {
        foreach (IHostedService trabajo in _servicios.GetServices<IHostedService>())
        {
            await trabajo.StartAsync(CancellationToken.None);
        }
    }

    /// <summary>Deja un evento de prueba en la cola, como lo dejaría el interceptor.</summary>
    /// <remarks>
    /// El interceptor tiene sus propios tests —es la primera cláusula del R12, y se comprueba
    /// mirando en qué transacción entró la fila—. Aquí lo que se prueba es lo que pasa DESPUÉS,
    /// así que la fila se pone directamente y con la misma forma.
    /// </remarks>
    /// <param name="evento">El hecho que se encola.</param>
    /// <param name="nombre">
    /// Con qué nombre se encola. Se puede decir uno que no declare nadie, que es la forma de
    /// provocar la fila envenenada que ningún manejador puede llegar a ver.
    /// </param>
    /// <param name="ocurridoEn">
    /// Cuándo ocurrió el hecho. Por omisión, ahora; se dice otro para poder comprobar la métrica
    /// que de verdad importa, que es la EDAD del pendiente más viejo.
    /// </param>
    /// <param name="sinInquilino">
    /// El motivo de una fila escrita bajo un ámbito sin inquilino, que no lleva empresa. Por omisión
    /// la lleva, y es <see cref="EmpresaDePrueba"/>.
    /// </param>
    /// <param name="causa">Quién lo causó. Por omisión, nadie con sesión y una correlación nueva.</param>
    public async Task EncolarAsync(
        EventoDeIntegracion evento,
        string? nombre = null,
        DateTimeOffset? ocurridoEn = null,
        MotivoSinInquilino? sinInquilino = null,
        Causa? causa = null)
    {
        ArgumentNullException.ThrowIfNull(evento);

        using IServiceScope ambito = _servicios.CreateScope();

        ContextoDeLaBandeja contexto = ambito.ServiceProvider.GetRequiredService<ContextoDeLaBandeja>();

        contexto.Bandeja.Add(EventoDeLaBandeja.De(
            evento.EventoId,
            ocurridoEn ?? DateTimeOffset.UtcNow,
            empresaId: sinInquilino is null ? EmpresaDePrueba : null,
            sinInquilino: sinInquilino,
            nombre ?? HechoDePrueba.Nombre,
            JsonSerializer.Serialize(evento, evento.GetType(), Json),
            causa ?? SinCausa()));

        await contexto.SaveChangesAsync();
    }

    /// <summary>Entrega un evento a sus manejadores, como lo entrega el publicador.</summary>
    /// <remarks>
    /// La fila se arma en memoria, con la forma de la del interceptor, y no se escribe: lo que se
    /// ejercita es el despachador, que es la capa que decide, sin esperar a ninguna vuelta. Se le
    /// entrega dentro del ámbito sin inquilino con su motivo, como en cada vuelta del publicador: la
    /// cola no es de ninguna empresa en particular, y la del manejador la pone la fila.
    /// </remarks>
    /// <param name="evento">El hecho que se entrega.</param>
    /// <param name="empresaId">La empresa de la fila. Por omisión, <see cref="EmpresaDePrueba"/>.</param>
    /// <param name="sinInquilino">El motivo de una fila sin empresa; con él, no lleva ninguna.</param>
    /// <param name="causa">Quién lo causó. Por omisión, nadie con sesión y una correlación nueva.</param>
    /// <returns>Cuántos manejadores lo han atendido.</returns>
    public async Task<int> DespacharAsync(
        EventoDeIntegracion evento,
        Guid? empresaId = null,
        MotivoSinInquilino? sinInquilino = null,
        Causa? causa = null)
    {
        ArgumentNullException.ThrowIfNull(evento);

        var fila = EventoDeLaBandeja.De(
            evento.EventoId,
            DateTimeOffset.UtcNow,
            empresaId: sinInquilino is null ? empresaId ?? EmpresaDePrueba : null,
            sinInquilino: sinInquilino,
            _servicios.GetRequiredService<CatalogoDeEventos>().NombreDe(evento.GetType()),
            JsonSerializer.Serialize(evento, evento.GetType(), Json),
            causa ?? SinCausa());

        return await DespacharLaFilaAsync(fila);
    }

    /// <summary>Entrega una fila tal como está, como la entrega el publicador al leerla de la cola.</summary>
    /// <remarks>
    /// Para la fila que no se puede armar con la factoría: la que se escribió antes del 3.4, sin
    /// correlación, que se lee de la base después de quitársela.
    /// </remarks>
    /// <param name="fila">La fila que se entrega.</param>
    /// <returns>Cuántos manejadores la han atendido.</returns>
    public async Task<int> DespacharLaFilaAsync(EventoDeLaBandeja fila)
    {
        ArgumentNullException.ThrowIfNull(fila);

        using IServiceScope ambito = _servicios.CreateScope();

        IInquilinoActual inquilino = ambito.ServiceProvider.GetRequiredService<IInquilinoActual>();

        using IDisposable deLaVuelta = inquilino.SinInquilino(MotivoSinInquilino.PublicacionDeEventos);

        return await ambito.ServiceProvider
            .GetRequiredService<IDespachadorDeEventos>()
            .DespacharAsync(fila, CancellationToken.None);
    }

    /// <summary>Lee la fila de la cola de un evento, con lo que el publicador haya dejado escrito.</summary>
    /// <param name="eventoId">Identificador del evento.</param>
    public async Task<EventoDeLaBandeja?> EnLaColaAsync(Guid eventoId)
    {
        using IServiceScope ambito = _servicios.CreateScope();

        IInquilinoActual inquilino = ambito.ServiceProvider.GetRequiredService<IInquilinoActual>();

        using IDisposable _ = inquilino.SinInquilino(MotivoSinInquilino.PublicacionDeEventos);

        ContextoDeLaBandeja contexto = ambito.ServiceProvider.GetRequiredService<ContextoDeLaBandeja>();

        return await contexto.Bandeja.AsNoTracking().SingleOrDefaultAsync(fila => fila.EventoId == eventoId);
    }

    /// <summary>Espera a que se cumpla algo, o se rinde. Devuelve si se cumplió.</summary>
    /// <remarks>
    /// El publicador es asíncrono por definición: no hay manera de preguntarle «¿ya?» que no sea
    /// mirar el efecto y volver a mirar. El plazo es generoso a propósito —la CI es más lenta que
    /// una máquina de desarrollo—, y sólo se agota cuando el test va a fallar de todas formas.
    /// </remarks>
    /// <param name="condicion">Lo que se está esperando.</param>
    public static async Task<bool> EsperarAsync(Func<Task<bool>> condicion)
    {
        ArgumentNullException.ThrowIfNull(condicion);

        DateTimeOffset limite = DateTimeOffset.UtcNow.AddSeconds(30);

        while (DateTimeOffset.UtcNow < limite)
        {
            if (await condicion())
            {
                return true;
            }

            await Task.Delay(50);
        }

        return false;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        foreach (IHostedService trabajo in _servicios.GetServices<IHostedService>())
        {
            await trabajo.StopAsync(CancellationToken.None);
        }

        await _servicios.DisposeAsync();
    }

    private static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web);

    // La de un evento que no causó nadie con sesión ni dentro de una traza: la de un trabajo de
    // fondo. Su correlación es nueva, como lo sería la de su ámbito.
    private static Causa SinCausa() => new(null, Guid.CreateVersion7(), null);
}

/// <summary>Un hecho que solo existe en los tests, para probar la fontanería sin tocar el negocio.</summary>
/// <param name="Marca">
/// Qué instancia del test lo ha emitido. Los manejadores comparten proceso, así que cuentan sus
/// efectos por esta marca; sin ella, dos tests en paralelo se contarían el uno al otro.
/// </param>
public sealed record HechoDePrueba(string Marca) : EventoDeIntegracion
{
    /// <summary>Con qué nombre viaja en la cola.</summary>
    public const string Nombre = "pruebas.hecho-de-prueba";
}

/// <summary>Lo que han hecho los manejadores de prueba, contado por marca.</summary>
/// <remarks>
/// Es <b>el efecto</b>, y es contra el efecto contra lo que se comprueba que reprocesar no
/// duplica. Mirar la tabla de huellas sería mirar el reflejo: esa tabla la escribe justo el código
/// que se está poniendo a prueba.
/// </remarks>
public static class Efectos
{
    private static readonly ConcurrentDictionary<string, int> s_veces = new(StringComparer.Ordinal);

    /// <summary>Apunta que un manejador ha atendido algo.</summary>
    /// <param name="clave">Marca del hecho, más el consumidor.</param>
    public static void Anotar(string clave) => s_veces.AddOrUpdate(clave, 1, (_, cuantas) => cuantas + 1);

    /// <summary>Cuántas veces se ha atendido.</summary>
    /// <param name="clave">Marca del hecho, más el consumidor.</param>
    public static int Veces(string clave) => s_veces.GetValueOrDefault(clave);
}

/// <summary>Un consumidor que solo cuenta que ha pasado por aquí.</summary>
public sealed class ManejadorQueCuenta : ManejadorDeEvento<HechoDePrueba>
{
    /// <inheritdoc/>
    public override string Consumidor => "pruebas.el-que-cuenta";

    /// <inheritdoc/>
    protected override Task AtenderAsync(HechoDePrueba evento, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(evento);

        Efectos.Anotar($"{evento.Marca}/{Consumidor}");

        return Task.CompletedTask;
    }
}

/// <summary>Otro consumidor del mismo hecho, para ver que cada uno tiene su turno.</summary>
public sealed class OtroManejadorQueCuenta : ManejadorDeEvento<HechoDePrueba>
{
    /// <inheritdoc/>
    public override string Consumidor => "pruebas.el-otro-que-cuenta";

    /// <inheritdoc/>
    protected override Task AtenderAsync(HechoDePrueba evento, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(evento);

        Efectos.Anotar($"{evento.Marca}/{Consumidor}");

        return Task.CompletedTask;
    }
}

/// <summary>Un consumidor que revienta la primera vez y va bien la segunda.</summary>
/// <remarks>
/// Es el fallo que de verdad ocurre: una red que se cae, un bloqueo, un servicio que tarda. Con él
/// se distingue «entrega al menos una vez» de «entrega como mucho una vez»: si la fila se marcara
/// como publicada ANTES de que el manejador terminase, este evento no llegaría nunca.
/// </remarks>
public sealed class ManejadorQueFallaLaPrimeraVez : ManejadorDeEvento<HechoDePrueba>
{
    private static readonly ConcurrentDictionary<string, int> s_intentos = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public override string Consumidor => "pruebas.el-que-falla-una-vez";

    /// <inheritdoc/>
    protected override Task AtenderAsync(HechoDePrueba evento, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(evento);

        if (s_intentos.AddOrUpdate(evento.Marca, 1, (_, cuantos) => cuantos + 1) == 1)
        {
            throw new InvalidOperationException("La primera vez no, gracias.");
        }

        Efectos.Anotar($"{evento.Marca}/{Consumidor}");

        return Task.CompletedTask;
    }
}

/// <summary>Un consumidor que no funciona nunca: el evento envenenado.</summary>
public sealed class ManejadorQueSiempreFalla : ManejadorDeEvento<HechoDePrueba>
{
    /// <inheritdoc/>
    public override string Consumidor => "pruebas.el-que-nunca-va";

    /// <inheritdoc/>
    protected override Task AtenderAsync(HechoDePrueba evento, CancellationToken cancelacion) =>
        throw new InvalidOperationException("Este consumidor no funciona, y no va a funcionar.");
}

/// <summary>Lo que la bandeja escribe en el registro, guardado para poder mirarlo.</summary>
/// <remarks>
/// Un trabajo de fondo que se para tiene que DECIRLO, y decirlo una vez y no una por vuelta. Eso
/// no se comprueba leyendo el código: se comprueba leyendo lo que escribió.
/// </remarks>
public sealed class RegistroDeLaBandeja : ILoggerProvider
{
    private readonly ConcurrentQueue<(int Evento, string Mensaje)> _lineas = new();

    /// <summary>Cuántas veces se ha escrito una línea con ese identificador de evento.</summary>
    /// <param name="evento">Identificador del evento de registro.</param>
    public int Veces(int evento) => _lineas.Count(linea => linea.Evento == evento);

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName) => new Anotador(_lineas);

    /// <inheritdoc/>
    public void Dispose() => GC.SuppressFinalize(this);

    private sealed class Anotador(ConcurrentQueue<(int Evento, string Mensaje)> lineas) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            lineas.Enqueue((eventId.Id, formatter(state, exception)));
        }
    }
}
