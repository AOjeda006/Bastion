using System.Diagnostics.Metrics;
using Bastion.Api.FunctionalTests.Salud;
using Bastion.Inventario.Infrastructure;
using Bastion.Inventario.Infrastructure.Corte;
using Bastion.Inventario.Infrastructure.Persistencia.Existencias;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Corte;

/// <summary>
/// El trabajo mensual de Inventario sin base: el mes que toca, una empresa por ámbito, lo que cuenta
/// y lo que anota, y cuándo se registra (ADR-0063 §7).
/// </summary>
/// <remarks>
/// <para>
/// <b>El avance de verdad se prueba en el carril de integración</b>, llamándolo con su empresa, en
/// <c>LasExistenciasSonLaSumaDelLibroTests</c>. Lo que queda aquí es lo que decide el bucle, que no
/// necesita PostgreSQL: con un avance de mentira detrás, cada caso elige qué contesta cada empresa.
/// </para>
/// <para>
/// <b>El reloj está parado en la primera media hora de un año</b>, que es donde el mes que toca
/// cambia de año: un cálculo que restara un mes al número del mes sin tocar el año saldría mal
/// justo ahí.
/// </para>
/// </remarks>
public sealed class ElTrabajoMensualAvanzaElCorteTests
{
    private static readonly DateTimeOffset s_primeraMediaHoraDelAnio = new(2027, 1, 1, 0, 30, 0, TimeSpan.Zero);

    private static readonly DateOnly s_diciembre = new(2026, 12, 1);

    private static readonly Guid s_primera = Guid.Parse("01900000-0000-7000-8000-000000000001");
    private static readonly Guid s_segunda = Guid.Parse("01900000-0000-7000-8000-000000000002");
    private static readonly Guid s_tercera = Guid.Parse("01900000-0000-7000-8000-000000000003");

    [Fact]
    public void El_mes_que_toca_es_el_anterior_al_de_hoy_y_hoy_es_el_de_UTC()
    {
        AvanzadorDelCorte.MesQueToca(new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.Zero))
            .ShouldBe(new DateOnly(2026, 9, 1));

        AvanzadorDelCorte.MesQueToca(s_primeraMediaHoraDelAnio)
            .ShouldBe(s_diciembre, "en enero, el mes que toca es diciembre del año anterior");

        // Las once y media de la noche del 31 de marzo a dos horas por detrás de UTC: allí sigue
        // siendo marzo, y en UTC ya es abril. El mes que toca es marzo.
        AvanzadorDelCorte.MesQueToca(new DateTimeOffset(2026, 3, 31, 23, 30, 0, TimeSpan.FromHours(-2)))
            .ShouldBe(new DateOnly(2026, 3, 1), "hoy es el de UTC, no el de la hora que trae el instante");

        // Y al revés: la media hora del 1 de abril a dos horas por delante sigue siendo marzo en UTC.
        AvanzadorDelCorte.MesQueToca(new DateTimeOffset(2026, 4, 1, 0, 30, 0, TimeSpan.FromHours(2)))
            .ShouldBe(new DateOnly(2026, 2, 1));
    }

    [Fact]
    public async Task Una_vuelta_avanza_cada_empresa_en_su_ambito_y_cuenta_las_que_avanzan()
    {
        Libreta libreta = new([s_primera, s_segunda, s_tercera])
        {
            [s_primera] = () => Hecho(),
            [s_segunda] = () => null,
            [s_tercera] = () => Hecho(),
        };

        using MetricasDelCorte metricas = new();
        using EspiaDeMedidas espia = new();
        Anotador registro = new();

        await using (ServiceProvider proveedor = Montar(libreta))
        {
            await Avanzador(proveedor, metricas, registro).UnaVueltaAsync(CancellationToken.None);
        }

        libreta.Avances.ShouldBe(
            [(s_primera, s_diciembre), (s_segunda, s_diciembre), (s_tercera, s_diciembre)],
            "cada empresa, en el orden en que llegan, con el mes que toca");

        libreta.Instancias.ShouldBe(4, "un ámbito para la lista y uno por empresa");

        espia.Leer("bastion.inventario.corte.empresas_avanzadas").ShouldBe(2, "la que ya llegaba no cuenta");
        espia.Leer("bastion.inventario.corte.avances_fallidos").ShouldBe(0);
        espia.Leer("bastion.inventario.corte.descuadres").ShouldBe(0);

        registro.De(8701).Select(linea => linea.Mensaje).ShouldBe(
        [
            $"Corte de la empresa {s_primera} avanzado hasta 2026-12: 40 instantáneas rehechas en 1500 ms. " +
            "El cuadre comparó 7 existencias y 40 instantáneas en 250 ms, con 0 descuadres.",
            $"Corte de la empresa {s_tercera} avanzado hasta 2026-12: 40 instantáneas rehechas en 1500 ms. " +
            "El cuadre comparó 7 existencias y 40 instantáneas en 250 ms, con 0 descuadres.",
        ]);

        registro.De(8700).ShouldHaveSingleItem().Mensaje.ShouldBe(
            "Avance del corte hasta 2026-12: 3 empresas con existencias, 2 avanzadas y 0 con fallo.");

        registro.Lineas.ShouldAllBe(linea => linea.Nivel == LogLevel.Information);
    }

    [Fact]
    public async Task El_fallo_de_una_empresa_se_anota_se_cuenta_y_no_para_a_las_demas()
    {
        InvalidOperationException caida = new("la base se ha ido a mitad");

        Libreta libreta = new([s_primera, s_segunda, s_tercera])
        {
            [s_primera] = () => Hecho(),
            [s_segunda] = () => throw caida,
            [s_tercera] = () => Hecho(),
        };

        using MetricasDelCorte metricas = new();
        using EspiaDeMedidas espia = new();
        Anotador registro = new();

        await using (ServiceProvider proveedor = Montar(libreta))
        {
            await Avanzador(proveedor, metricas, registro).UnaVueltaAsync(CancellationToken.None);
        }

        libreta.Avances.Select(avance => avance.EmpresaId).ShouldBe(
            [s_primera, s_segunda, s_tercera],
            "la tercera se avanza aunque la segunda haya fallado");

        espia.Leer("bastion.inventario.corte.empresas_avanzadas").ShouldBe(2);
        espia.Leer("bastion.inventario.corte.avances_fallidos").ShouldBe(1);

        Linea fallo = registro.De(8702).ShouldHaveSingleItem();

        fallo.Nivel.ShouldBe(LogLevel.Error);
        fallo.Fallo.ShouldBeSameAs(caida);
        fallo.Mensaje.ShouldBe(
            $"El avance del corte de la empresa {s_segunda} hasta 2026-12 ha fallado. Se reintenta " +
            "en la hora siguiente.");

        registro.De(8700).ShouldHaveSingleItem().Mensaje.ShouldBe(
            "Avance del corte hasta 2026-12: 3 empresas con existencias, 2 avanzadas y 1 con fallo.");
    }

    [Fact]
    public async Task Parar_a_mitad_de_una_empresa_no_es_un_fallo_y_no_sigue_con_las_demas()
    {
        using CancellationTokenSource parada = new();

        Libreta libreta = new([s_primera, s_segunda, s_tercera])
        {
            [s_primera] = () => Hecho(),
            [s_segunda] = () =>
            {
                parada.Cancel();
                throw new OperationCanceledException(parada.Token);
            },
            [s_tercera] = () => Hecho(),
        };

        using MetricasDelCorte metricas = new();
        using EspiaDeMedidas espia = new();
        Anotador registro = new();

        await using (ServiceProvider proveedor = Montar(libreta))
        {
            await Should.ThrowAsync<OperationCanceledException>(
                () => Avanzador(proveedor, metricas, registro).UnaVueltaAsync(parada.Token));
        }

        libreta.Avances.Select(avance => avance.EmpresaId).ShouldBe([s_primera, s_segunda]);
        espia.Leer("bastion.inventario.corte.avances_fallidos").ShouldBe(0, "apagar la API no es un fallo");
        registro.De(8702).ShouldBeEmpty();
    }

    [Fact]
    public async Task Una_cancelacion_que_no_es_la_parada_es_un_fallo_de_su_empresa()
    {
        // La pareja del caso anterior: una cancelación que nadie ha pedido, la de un token que no es
        // el de la parada. Tratarla como apagar la API dejaría a las empresas de detrás sin avanzar
        // hasta la hora siguiente.
        OperationCanceledException ajena = new("una cancelación que no viene de la parada");

        Libreta libreta = new([s_primera, s_segunda, s_tercera])
        {
            [s_primera] = () => Hecho(),
            [s_segunda] = () => throw ajena,
            [s_tercera] = () => Hecho(),
        };

        using MetricasDelCorte metricas = new();
        using EspiaDeMedidas espia = new();
        Anotador registro = new();

        await using (ServiceProvider proveedor = Montar(libreta))
        {
            await Avanzador(proveedor, metricas, registro).UnaVueltaAsync(CancellationToken.None);
        }

        libreta.Avances.Select(avance => avance.EmpresaId).ShouldBe([s_primera, s_segunda, s_tercera]);
        espia.Leer("bastion.inventario.corte.avances_fallidos").ShouldBe(1);
        registro.De(8702).ShouldHaveSingleItem().Fallo.ShouldBeSameAs(ajena);
    }

    [Fact]
    public async Task Un_descuadre_es_un_aviso_con_sus_cifras_y_cuenta_cada_uno()
    {
        Libreta libreta = new([s_primera, s_segunda])
        {
            [s_primera] = () => Hecho(Descuadre("instantanea-valor"), Descuadre("instantanea"), Descuadre("instantanea")),
            [s_segunda] = () => Hecho(),
        };

        using MetricasDelCorte metricas = new();
        using EspiaDeMedidas espia = new();
        Anotador registro = new();

        await using (ServiceProvider proveedor = Montar(libreta))
        {
            await Avanzador(proveedor, metricas, registro).UnaVueltaAsync(CancellationToken.None);
        }

        espia.Leer("bastion.inventario.corte.descuadres").ShouldBe(3, "cada descuadre, no cada empresa");
        espia.Leer("bastion.inventario.corte.empresas_avanzadas").ShouldBe(2);

        Linea aviso = registro.De(8703).ShouldHaveSingleItem("solo la empresa que no cuadra");

        aviso.Nivel.ShouldBe(LogLevel.Warning);
        aviso.Mensaje.ShouldBe(
            $"El cuadre de la empresa {s_primera}, tras avanzar el corte hasta 2026-12, encontró 3 " +
            "descuadres (instantanea, instantanea-valor) en 7 existencias y 40 instantáneas comparadas.");
    }

    [Fact]
    public void Las_tres_cifras_van_a_su_propio_contador_y_ninguna_lleva_la_empresa()
    {
        using MetricasDelCorte metricas = new();
        using EspiaDeMedidas espia = new();

        metricas.AnotarAvanzada();
        metricas.AnotarAvanzada();
        metricas.AnotarFallido();
        metricas.AnotarDescuadres(5);

        espia.Leer("bastion.inventario.corte.empresas_avanzadas").ShouldBe(2);
        espia.Leer("bastion.inventario.corte.avances_fallidos").ShouldBe(1);
        espia.Leer("bastion.inventario.corte.descuadres").ShouldBe(5);

        espia.Unidad("bastion.inventario.corte.empresas_avanzadas").ShouldBe("{empresa}");
        espia.Unidad("bastion.inventario.corte.avances_fallidos").ShouldBe("{empresa}");
        espia.Unidad("bastion.inventario.corte.descuadres").ShouldBe("{descuadre}");

        espia.Etiquetas.ShouldBe(0, "una etiqueta con la empresa sería una serie por cliente en el recolector");
        espia.Medidas.ShouldBe(4, "sin medidas, contar las etiquetas no diría nada");

        ModuloDeInventario.MedidorDelCorte.ShouldBe(
            "Bastion.Inventario.Corte", "es el nombre que se registra en OpenTelemetry");
    }

    [Fact]
    public void El_modulo_registra_siempre_el_avance_y_el_trabajo_solo_si_se_le_pide()
    {
        ServiceCollection encendido = [];
        encendido.AgregarModuloDeInventario("Host=ninguno", avanceDelCorte: true);

        ServiceCollection apagado = [];
        apagado.AgregarModuloDeInventario("Host=ninguno", avanceDelCorte: false);

        TrabajosDe(encendido).ShouldBe([typeof(AvanzadorDelCorte)]);
        TrabajosDe(apagado).ShouldBeEmpty();

        foreach (ServiceCollection servicios in new[] { encendido, apagado })
        {
            servicios.ShouldContain(servicio =>
                servicio.ServiceType == typeof(IAvanceDelCorte)
                && servicio.ImplementationType == typeof(ElAvanceDelCorte)
                && servicio.Lifetime == ServiceLifetime.Scoped);
        }
    }

    [Fact]
    public void Sin_base_de_datos_el_host_no_arranca_el_trabajo_mensual()
    {
        using ApiSinDependencias api = new();

        string[] trabajos = [.. api.Services.GetServices<IHostedService>().Select(trabajo => trabajo.GetType().Name)];

        trabajos.ShouldNotBeEmpty("si el host no tuviera ningún trabajo, no se vería tampoco el que falta");
        trabajos.ShouldNotContain(nameof(AvanzadorDelCorte));
    }

    [Fact]
    public void La_variable_vale_encendido_o_apagado_y_sin_ella_va_encendido()
    {
        ModuloDeInventario.ElAvanceDelCorteVaEncendido(null).ShouldBeTrue();
        ModuloDeInventario.ElAvanceDelCorteVaEncendido(string.Empty).ShouldBeTrue();
        ModuloDeInventario.ElAvanceDelCorteVaEncendido("   ").ShouldBeTrue();
        ModuloDeInventario.ElAvanceDelCorteVaEncendido("encendido").ShouldBeTrue();
        ModuloDeInventario.ElAvanceDelCorteVaEncendido(" Encendido ").ShouldBeTrue();
        ModuloDeInventario.ElAvanceDelCorteVaEncendido("apagado").ShouldBeFalse();
        ModuloDeInventario.ElAvanceDelCorteVaEncendido("APAGADO").ShouldBeFalse();

        foreach (string mal in new[] { "no", "false", "0", "apagada" })
        {
            Should.Throw<InvalidOperationException>(() => ModuloDeInventario.ElAvanceDelCorteVaEncendido(mal))
                .Message.ShouldContain(ModuloDeInventario.VariableDelAvanceDelCorte);
        }
    }

    [Fact]
    public void Un_valor_mal_puesto_para_el_arranque_aunque_no_haya_base()
    {
        // Sin base, el trabajo no se registraría de todas formas, y por eso es el caso que importa:
        // si la variable solo se leyera con base, el error se quedaría escondido hasta el día que
        // la hubiera, que es en producción.
        using ApiSinDependencias sinBase = new();
        using WebApplicationFactory<Program> conLaVariable = sinBase.WithWebHostBuilder(
            builder => builder.UseSetting(ModuloDeInventario.VariableDelAvanceDelCorte, "quizá"));

        Exception fallo = Should.Throw<Exception>(() => conLaVariable.Services);

        Cadena(fallo).ShouldContain(
            causa => causa is InvalidOperationException
                && causa.Message.Contains(ModuloDeInventario.VariableDelAvanceDelCorte, StringComparison.Ordinal));
    }

    private static Type?[] TrabajosDe(ServiceCollection servicios) =>
        [.. servicios
            .Where(servicio => servicio.ServiceType == typeof(IHostedService))
            .Select(servicio => servicio.ImplementationType)];

    private static IEnumerable<Exception> Cadena(Exception? fallo)
    {
        for (; fallo is not null; fallo = fallo.InnerException)
        {
            yield return fallo;
        }
    }

    private static ServiceProvider Montar(Libreta libreta)
    {
        ServiceCollection servicios = [];

        servicios.AddSingleton(libreta);
        servicios.AddScoped<IAvanceDelCorte, AvanceDeMentira>();

        return servicios.BuildServiceProvider(validateScopes: true);
    }

    private static AvanzadorDelCorte Avanzador(
        ServiceProvider proveedor, MetricasDelCorte metricas, Anotador registro) =>
        new(
            proveedor.GetRequiredService<IServiceScopeFactory>(),
            new RelojParado(s_primeraMediaHoraDelAnio),
            metricas,
            registro);

    private static AvanceHecho Hecho(params Descuadre[] descuadres) =>
        new(
            40,
            TimeSpan.FromMilliseconds(1500),
            TimeSpan.FromMilliseconds(250),
            new CuadreDeLasExistencias(7, 40, 3, 0, 0, descuadres));

    private static Descuadre Descuadre(string que) =>
        new(que, Guid.CreateVersion7(), Guid.CreateVersion7(), null, null, null, s_diciembre, 1m, 2m, 1, null, null);

    /// <summary>Lo que el avance de mentira contesta, y lo que ha visto.</summary>
    /// <param name="empresas">Las empresas que devuelve la lista.</param>
    private sealed class Libreta(IReadOnlyList<Guid> empresas)
    {
        private readonly Dictionary<Guid, Func<AvanceHecho?>> _respuestas = [];

        /// <summary>Lo que contesta el avance de una empresa; lanzar es fallar.</summary>
        /// <param name="empresaId">La empresa.</param>
        public Func<AvanceHecho?> this[Guid empresaId]
        {
            get => _respuestas[empresaId];
            init => _respuestas[empresaId] = value;
        }

        public IReadOnlyList<Guid> Empresas => empresas;

        public List<(Guid EmpresaId, DateOnly HastaElMes)> Avances { get; } = [];

        public int Instancias { get; set; }
    }

    /// <summary>Un avance que contesta lo que diga la libreta, y cuenta cuántas veces se crea.</summary>
    private sealed class AvanceDeMentira : IAvanceDelCorte
    {
        private readonly Libreta _libreta;

        public AvanceDeMentira(Libreta libreta)
        {
            _libreta = libreta;
            _libreta.Instancias++;
        }

        public Task<IReadOnlyList<Guid>> EmpresasConExistenciasAsync(CancellationToken cancelacion) =>
            Task.FromResult(_libreta.Empresas);

        public async Task<AvanceHecho?> AvanzarAsync(
            Guid empresaId, DateOnly hastaElMes, CancellationToken cancelacion)
        {
            await Task.Yield();

            _libreta.Avances.Add((empresaId, hastaElMes));

            return _libreta[empresaId]();
        }
    }

    /// <summary>Un <c>TimeProvider</c> que siempre contesta lo mismo.</summary>
    /// <param name="instante">Lo que contesta, siempre.</param>
    private sealed class RelojParado(DateTimeOffset instante) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instante;
    }

    /// <summary>Una línea del registro, ya formateada.</summary>
    private sealed record Linea(LogLevel Nivel, int Evento, string Mensaje, Exception? Fallo);

    /// <summary>Un registro que guarda lo que se le anota.</summary>
    private sealed class Anotador : ILogger<AvanzadorDelCorte>
    {
        public List<Linea> Lineas { get; } = [];

        public IEnumerable<Linea> De(int evento) => Lineas.Where(linea => linea.Evento == evento);

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Lineas.Add(new Linea(logLevel, eventId.Id, formatter(state, exception), exception));
    }

    // Un oyente de los instrumentos del medidor del corte, como el de la bandeja: suma lo que se
    // publica, guarda la unidad de cada uno y cuenta las medidas que llevan alguna etiqueta.
    private sealed class EspiaDeMedidas : IDisposable
    {
        private readonly Dictionary<string, long> _valores = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string?> _unidades = new(StringComparer.Ordinal);
        private readonly MeterListener _oyente = new();

        public EspiaDeMedidas()
        {
            _oyente.InstrumentPublished = (instrumento, oyente) =>
            {
                if (string.Equals(instrumento.Meter.Name, ModuloDeInventario.MedidorDelCorte, StringComparison.Ordinal))
                {
                    _unidades[instrumento.Name] = instrumento.Unit;
                    oyente.EnableMeasurementEvents(instrumento);
                }
            };

            _oyente.SetMeasurementEventCallback<long>((instrumento, medida, etiquetas, _) =>
            {
                _valores[instrumento.Name] = _valores.GetValueOrDefault(instrumento.Name) + medida;
                Medidas++;

                if (!etiquetas.IsEmpty)
                {
                    Etiquetas++;
                }
            });

            _oyente.Start();
        }

        public int Medidas { get; private set; }

        public int Etiquetas { get; private set; }

        public long Leer(string instrumento) => _valores.GetValueOrDefault(instrumento);

        public string? Unidad(string instrumento) => _unidades.GetValueOrDefault(instrumento);

        public void Dispose() => _oyente.Dispose();
    }
}
