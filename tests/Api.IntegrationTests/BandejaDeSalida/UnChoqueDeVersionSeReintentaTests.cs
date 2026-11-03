using System.Collections.Concurrent;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.BandejaDeSalida;

/// <summary>
/// El hueco 5 del 3.4, su segunda mitad: <b>un choque de versión se reintenta en el acto, y no
/// aparca nunca</b>.
/// </summary>
/// <remarks>
/// <para>
/// El choque es de dos transacciones de verdad sobre un documento con su <c>xmin</c>: lo provoca
/// <see cref="ManejadorQueChoca"/>, que lee su documento en la transacción del despachador, deja
/// que otra conexión lo cambie y confirme, y al guardar se encuentra con que la fila ya no es la que
/// leyó. No hay un doble que lance la excepción: la lanza EF Core, por lo que contesta PostgreSQL.
/// </para>
/// <para>
/// Se mira el efecto —cuántas veces se contó el documento, cuántos intentos hubo y con qué
/// contexto, y lo que la fila dice de sí misma—, y del registro, solo lo que no deja otro rastro.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor compartido.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class UnChoqueDeVersionSeReintentaTests(PostgresConTodosLosModulos postgres)
{
    // Lo que escriben en el registro el despachador, a cada reintento, y el publicador, cuando aun
    // así sigue chocando.
    private const int SeReintentaEnElActo = 8201;
    private const int SigueChocando = 8307;

    private const string Consumidor = "pruebas.el-que-choca";

    [Fact]
    public async Task Tres_choques_se_reintentan_en_el_acto_cada_uno_con_un_contexto_nuevo()
    {
        string cadena = await ContextoDePrueba.BaseNuevaAsync(postgres);

        await using BandejaDeVerdad bandeja = new(cadena, publica: false, ConElQueChoca);

        HechoDePrueba hecho = await QueChocaAsync(cadena, EventoDeLaBandeja.ReintentosPorChoque);

        // La huella del intento que chocó se deshace con él. Si se hubiera quedado, el cuarto daría
        // el evento por repetido, y saldría un cero.
        (await bandeja.DespacharAsync(hecho)).ShouldBe(1, "al cuarto intento ya no choca, y lo atiende");

        IReadOnlyList<Guid> contextos = ManejadorQueChoca.ContextosDe(hecho.Marca);

        contextos.Count.ShouldBe(EventoDeLaBandeja.ReintentosPorChoque + 1);
        contextos.Distinct().Count().ShouldBe(
            contextos.Count,
            "cada reintento, con un contexto nuevo: el que chocó guarda lo que leyó antes del choque");

        // La semilla, uno por choque —el de la otra transacción—, y el del manejador, una sola vez.
        (await VecesAsync(cadena, hecho.Marca)).ShouldBe(1 + EventoDeLaBandeja.ReintentosPorChoque + 1);
        (await HuellasAsync(cadena, hecho)).ShouldBe(1);

        bandeja.Registro.Veces(SeReintentaEnElActo).ShouldBe(EventoDeLaBandeja.ReintentosPorChoque);
    }

    [Fact]
    public async Task La_espera_antes_de_cada_reintento_se_dobla_y_lleva_azar()
    {
        // Las esperas que pide el despachador, y no las que mide un reloj: el temporizador del
        // sistema no es tan fino como un tope de 25 ms, y un caso que dependiera de él fallaría por
        // azar. Cuatro eventos, doce esperas: si el azar quitara todo el tope en vez de la mitad,
        // las doce caerían en su mitad de arriba una vez de cada 4.096.
        const int Eventos = 4;

        string cadena = await ContextoDePrueba.BaseNuevaAsync(postgres);

        RelojQueApuntaLasEsperas reloj = new();

        await using BandejaDeVerdad bandeja = new(cadena, publica: false, servicios =>
        {
            ConElQueChoca(servicios);

            // Después del cableado, que lo registra con `TryAdd`: este es el que se resuelve.
            servicios.AddSingleton<TimeProvider>(reloj);
        });

        for (int evento = 0; evento < Eventos; evento++)
        {
            HechoDePrueba hecho = await QueChocaAsync(cadena, EventoDeLaBandeja.ReintentosPorChoque);

            (await bandeja.DespacharAsync(hecho)).ShouldBe(1);
        }

        IReadOnlyList<TimeSpan> esperas = reloj.Esperas;

        esperas.Count.ShouldBe(Eventos * EventoDeLaBandeja.ReintentosPorChoque);

        List<TimeSpan> topes = [];

        for (int cual = 0; cual < esperas.Count; cual++)
        {
            int reintento = (cual % EventoDeLaBandeja.ReintentosPorChoque) + 1;
            TimeSpan tope = EventoDeLaBandeja.EsperaTrasUnChoque * (1 << (reintento - 1));

            esperas[cual].ShouldBeGreaterThanOrEqualTo(tope / 2, $"el reintento {reintento} espera de menos");
            esperas[cual].ShouldBeLessThanOrEqualTo(tope, $"el reintento {reintento} espera de más");

            topes.Add(tope);
        }

        // La pareja del azar: sin él, todas serían su tope, y estarían todas dentro de lo de arriba.
        esperas.Where((espera, cual) => espera < topes[cual]).ShouldNotBeEmpty("las esperas no llevan azar");
    }

    [Fact]
    public async Task El_cuarto_choque_sale_del_despachador_y_no_deja_huella()
    {
        string cadena = await ContextoDePrueba.BaseNuevaAsync(postgres);

        await using BandejaDeVerdad bandeja = new(cadena, publica: false, ConElQueChoca);

        HechoDePrueba hecho = await QueChocaAsync(cadena, EventoDeLaBandeja.ReintentosPorChoque + 1);

        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => bandeja.DespacharAsync(hecho));

        ManejadorQueChoca.ContextosDe(hecho.Marca).Count.ShouldBe(
            EventoDeLaBandeja.ReintentosPorChoque + 1,
            "el primero y sus tres reintentos, y ni uno más");

        (await VecesAsync(cadena, hecho.Marca)).ShouldBe(
            1 + EventoDeLaBandeja.ReintentosPorChoque + 1,
            "la semilla y los cuatro de la otra transacción: el manejador no llegó a guardar");
        (await HuellasAsync(cadena, hecho)).ShouldBe(0);

        bandeja.Registro.Veces(SeReintentaEnElActo).ShouldBe(EventoDeLaBandeja.ReintentosPorChoque);
    }

    [Fact]
    public async Task Otro_fallo_no_se_reintenta_en_el_acto()
    {
        // Lo que no es un choque cuenta como hasta el 3.4: sale a la primera, y el publicador lo
        // apunta, lo reintenta en la vuelta siguiente y, a la quinta, lo aparca.
        string cadena = await ContextoDePrueba.BaseNuevaAsync(postgres);

        await using BandejaDeVerdad bandeja = new(
            cadena,
            publica: false,
            servicios => servicios.AgregarManejadorDeEvento<ManejadorQueFallaLaPrimeraVez, ContextoDeLaBandeja>());

        HechoDePrueba hecho = new(Marca());

        await Should.ThrowAsync<InvalidOperationException>(() => bandeja.DespacharAsync(hecho));

        bandeja.Registro.Veces(SeReintentaEnElActo).ShouldBe(0);

        // La pareja: falla solo la primera vez, así que un reintento en el acto lo habría atendido.
        (await bandeja.DespacharAsync(hecho)).ShouldBe(1);
    }

    [Fact]
    public async Task Un_choque_que_no_cesa_no_cuenta_intento_ni_aparca_y_la_vuelta_siguiente_lo_publica()
    {
        string cadena = await ContextoDePrueba.BaseNuevaAsync(postgres);

        await using BandejaDeVerdad bandeja = new(cadena, publica: true, ConElQueChoca);

        HechoDePrueba hecho = await QueChocaAsync(cadena, int.MaxValue);

        await bandeja.EncolarAsync(hecho);
        await bandeja.ArrancarAsync();

        // Más vueltas que intentos antes de aparcar: si un choque contara, ya estaría aparcada.
        (await BandejaDeVerdad.EsperarAsync(() => Task.FromResult(
                bandeja.Registro.Veces(SigueChocando) > EventoDeLaBandeja.IntentosAntesDeAparcar)))
            .ShouldBeTrue("el publicador no ha dicho, vuelta tras vuelta, que el evento sigue chocando");

        EventoDeLaBandeja chocando = (await bandeja.EnLaColaAsync(hecho.EventoId)).ShouldNotBeNull();

        chocando.Estado.ShouldBe(EstadoDelEnvio.Pendiente);
        chocando.Intentos.ShouldBe(0, "un choque no es un fallo del evento, y no cuenta");
        chocando.UltimoError.ShouldBeNull();

        // Deja de chocar, y la vuelta siguiente lo atiende.
        ManejadorQueChoca.Chocara(hecho.Marca, 0);

        (await BandejaDeVerdad.EsperarAsync(async () =>
                await bandeja.EnLaColaAsync(hecho.EventoId) is { Estado: EstadoDelEnvio.Publicado }))
            .ShouldBeTrue("en cuanto deja de chocar, se publica");

        EventoDeLaBandeja publicado = (await bandeja.EnLaColaAsync(hecho.EventoId)).ShouldNotBeNull();

        publicado.Intentos.ShouldBe(0);
        (await HuellasAsync(cadena, hecho)).ShouldBe(1);
    }

    private static void ConElQueChoca(IServiceCollection servicios) =>
        servicios.AgregarManejadorDeEvento<ManejadorQueChoca, ContextoDePrueba>();

    // Cada caso se lleva su marca, por lo mismo que en los demás de la bandeja.
    private static string Marca() => Guid.CreateVersion7().ToString();

    // Un hecho con su documento ya en la base, de la empresa de las filas de prueba, y un manejador
    // que chocará con él esas veces.
    private static async Task<HechoDePrueba> QueChocaAsync(string cadena, int veces)
    {
        HechoDePrueba hecho = new(Marca());

        await ContextoDePrueba.SembrarAsync(cadena, BandejaDeVerdad.EmpresaDePrueba, hecho.Marca);

        ManejadorQueChoca.Chocara(hecho.Marca, veces);

        return hecho;
    }

    private static async Task<int> VecesAsync(string cadena, string marca)
    {
        await using NpgsqlConnection conexion = new(cadena);
        await conexion.OpenAsync();

        await using NpgsqlCommand consulta = new(
            $"SELECT veces FROM {ContextoDePrueba.Esquema}.{ContextoDePrueba.Tabla} WHERE marca = @marca",
            conexion);
        consulta.Parameters.AddWithValue("marca", marca);

        return (int)(await consulta.ExecuteScalarAsync()).ShouldNotBeNull();
    }

    private static async Task<long> HuellasAsync(string cadena, HechoDePrueba hecho)
    {
        await using NpgsqlConnection conexion = new(cadena);
        await conexion.OpenAsync();

        await using NpgsqlCommand consulta = new(
            "SELECT count(*) FROM auditoria.eventos_procesados WHERE evento_id = @evento AND consumidor = @consumidor",
            conexion);
        consulta.Parameters.AddWithValue("evento", hecho.EventoId);
        consulta.Parameters.AddWithValue("consumidor", Consumidor);

        return (long)(await consulta.ExecuteScalarAsync()).ShouldNotBeNull();
    }

    // El reloj del sistema, que además apunta cada espera que se le pide. El despachador espera
    // con el reloj del contenedor, y una espera es un temporizador.
    private sealed class RelojQueApuntaLasEsperas : TimeProvider
    {
        private readonly ConcurrentQueue<TimeSpan> _esperas = new();

        public IReadOnlyList<TimeSpan> Esperas => [.. _esperas];

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _esperas.Enqueue(dueTime);

            return TimeProvider.System.CreateTimer(callback, state, dueTime, period);
        }
    }
}
