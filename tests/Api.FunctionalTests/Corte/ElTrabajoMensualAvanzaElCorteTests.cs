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
/// y lo que anota, y cuándo se registra (ADR-0063 §7). Lo que tarda cada mes y el retraso del corte
/// más viejo (ADR-0064 §3). Y desde el 2.18, el avance mes a mes y el cuadre de lo pendiente detrás
/// (ADR-0067).
/// </summary>
/// <remarks>
/// <para>
/// <b>El avance de verdad se prueba en el carril de integración</b>, llamándolo con su empresa, en
/// <c>LasExistenciasSonLaSumaDelLibroTests</c>. Lo que queda aquí es lo que decide el bucle, que no
/// necesita PostgreSQL: con un avance de mentira detrás, cada caso elige qué contesta cada empresa
/// en cada mes, y qué contesta su cuadre.
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

    private static readonly DateOnly s_noviembre = new(2026, 11, 1);
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
            Meses =
            {
                [s_primera] = [() => Mes(s_noviembre), () => Mes(s_diciembre)],
                [s_tercera] = [() => Mes(s_diciembre)],
            },
            Cuadres =
            {
                [s_primera] = () => Cuadrado(s_noviembre, s_diciembre),
                [s_tercera] = () => Cuadrado(s_diciembre, s_diciembre),
            },
        };

        using MetricasDelCorte metricas = new();
        using EspiaDeMedidas espia = new();
        Anotador registro = new();

        await using (ServiceProvider proveedor = Montar(libreta))
        {
            await Avanzador(proveedor, metricas, registro).UnaVueltaAsync(CancellationToken.None);
        }

        libreta.Avances.ShouldBe(
            [
                (s_primera, s_diciembre), (s_primera, s_diciembre), (s_primera, s_diciembre),
                (s_segunda, s_diciembre),
                (s_tercera, s_diciembre), (s_tercera, s_diciembre),
            ],
            "cada empresa, en el orden en que llegan y con el mes que toca, un mes por llamada hasta que " +
            "una contesta que ya llega");

        libreta.Cuadradas.ShouldBe([s_primera, s_segunda, s_tercera], "el cuadre va detrás de cada empresa");

        libreta.Instancias.ShouldBe(5, "un ámbito para la lista, uno por empresa y uno para los cortes");

        espia.Leer("bastion.inventario.corte.empresas_avanzadas").ShouldBe(2, "la que ya llegaba no cuenta");
        espia.Leer("bastion.inventario.corte.avances_fallidos").ShouldBe(0);
        espia.Leer("bastion.inventario.corte.descuadres").ShouldBe(0);

        registro.De(8701).Select(linea => linea.Mensaje).ShouldBe(
        [
            $"Corte de la empresa {s_primera} avanzado hasta 2026-11: 40 instantáneas escritas en 1500 ms.",
            $"Corte de la empresa {s_primera} avanzado hasta 2026-12: 40 instantáneas escritas en 1500 ms.",
            $"Corte de la empresa {s_tercera} avanzado hasta 2026-12: 40 instantáneas escritas en 1500 ms.",
        ]);

        registro.De(8705).Select(linea => linea.Mensaje).ShouldBe(
        [
            $"Cuadre de la empresa {s_primera} de 2026-11 a 2026-12: 7 existencias y 40 instantáneas " +
            "comparadas en 250 ms, con 0 descuadres.",
            $"Cuadre de la empresa {s_tercera} de 2026-12 a 2026-12: 7 existencias y 40 instantáneas " +
            "comparadas en 250 ms, con 0 descuadres.",
        ]);

        registro.De(8700).ShouldHaveSingleItem().Mensaje.ShouldBe(
            "Avance del corte hasta 2026-12: 3 empresas con existencias, 2 avanzadas y 0 con fallo.");

        registro.Lineas.ShouldAllBe(linea => linea.Nivel == LogLevel.Information);
    }

    [Fact]
    public async Task Una_empresa_que_ya_llega_cuadra_lo_que_le_quedo_pendiente()
    {
        // La segunda no avanza, porque su corte ya llega, pero su cuadre se quedó a medias en la
        // vuelta anterior: lo termina esta, y no cuenta como avanzada.
        Libreta libreta = new([s_primera, s_segunda])
        {
            Cuadres = { [s_segunda] = () => Cuadrado(s_noviembre, s_diciembre) },
        };

        using MetricasDelCorte metricas = new();
        using EspiaDeMedidas espia = new();
        Anotador registro = new();

        await using (ServiceProvider proveedor = Montar(libreta))
        {
            await Avanzador(proveedor, metricas, registro).UnaVueltaAsync(CancellationToken.None);
        }

        libreta.Cuadradas.ShouldBe([s_primera, s_segunda]);
        espia.Leer("bastion.inventario.corte.empresas_avanzadas").ShouldBe(0);
        registro.De(8701).ShouldBeEmpty();

        registro.De(8705).ShouldHaveSingleItem().Mensaje.ShouldBe(
            $"Cuadre de la empresa {s_segunda} de 2026-11 a 2026-12: 7 existencias y 40 instantáneas " +
            "comparadas en 250 ms, con 0 descuadres.");

        registro.De(8700).ShouldHaveSingleItem().Mensaje.ShouldBe(
            "Avance del corte hasta 2026-12: 2 empresas con existencias, 0 avanzadas y 0 con fallo.");
    }

    [Fact]
    public async Task Un_cuadre_que_falla_deja_la_empresa_avanzada_y_con_fallo()
    {
        InvalidOperationException caida = new("la base se ha ido en mitad del cuadre");

        Libreta libreta = new([s_primera, s_segunda])
        {
            Meses = { [s_primera] = [() => Mes(s_diciembre)], [s_segunda] = [() => Mes(s_diciembre)] },
            Cuadres = { [s_primera] = () => throw caida, [s_segunda] = () => Cuadrado(s_diciembre, s_diciembre) },
        };

        using MetricasDelCorte metricas = new();
        using EspiaDeMedidas espia = new();
        Anotador registro = new();

        await using (ServiceProvider proveedor = Montar(libreta))
        {
            await Avanzador(proveedor, metricas, registro).UnaVueltaAsync(CancellationToken.None);
        }

        espia.Leer("bastion.inventario.corte.empresas_avanzadas").ShouldBe(
            2, "el corte de la primera ya ha llegado cuando falla su cuadre");
        espia.Leer("bastion.inventario.corte.avances_fallidos").ShouldBe(1);

        registro.De(8702).ShouldHaveSingleItem().Fallo.ShouldBeSameAs(caida);
        registro.De(8705).ShouldHaveSingleItem().Mensaje.ShouldStartWith($"Cuadre de la empresa {s_segunda} ");

        registro.De(8700).ShouldHaveSingleItem().Mensaje.ShouldBe(
            "Avance del corte hasta 2026-12: 2 empresas con existencias, 2 avanzadas y 1 con fallo.");
    }

    [Fact]
    public async Task El_fallo_de_una_empresa_se_anota_se_cuenta_y_no_para_a_las_demas()
    {
        InvalidOperationException caida = new("la base se ha ido a mitad");

        Libreta libreta = new([s_primera, s_segunda, s_tercera])
        {
            Meses =
            {
                [s_primera] = [() => Mes(s_diciembre)],
                [s_segunda] = [() => throw caida],
                [s_tercera] = [() => Mes(s_diciembre)],
            },
        };

        using MetricasDelCorte metricas = new();
        using EspiaDeMedidas espia = new();
        Anotador registro = new();

        await using (ServiceProvider proveedor = Montar(libreta))
        {
            await Avanzador(proveedor, metricas, registro).UnaVueltaAsync(CancellationToken.None);
        }

        libreta.Avances.Select(avance => avance.EmpresaId).Distinct().ShouldBe(
            [s_primera, s_segunda, s_tercera],
            "la tercera se avanza aunque la segunda haya fallado");

        libreta.Cuadradas.ShouldBe([s_primera, s_tercera], "la que falla al avanzar no llega a su cuadre");

        espia.Leer("bastion.inventario.corte.empresas_avanzadas").ShouldBe(2);
        espia.Leer("bastion.inventario.corte.avances_fallidos").ShouldBe(1);

        Linea fallo = registro.De(8702).ShouldHaveSingleItem();

        fallo.Nivel.ShouldBe(LogLevel.Error);
        fallo.Fallo.ShouldBeSameAs(caida);
        fallo.Mensaje.ShouldBe(
            $"El avance o el cuadre del corte de la empresa {s_segunda} hasta 2026-12 ha fallado. Se " +
            "reintenta en la hora siguiente, desde el último mes que llegó a hacer.");

        registro.De(8700).ShouldHaveSingleItem().Mensaje.ShouldBe(
            "Avance del corte hasta 2026-12: 3 empresas con existencias, 2 avanzadas y 1 con fallo.");
    }

    [Fact]
    public async Task Parar_a_mitad_de_una_empresa_no_es_un_fallo_y_no_sigue_con_las_demas()
    {
        using CancellationTokenSource parada = new();

        Libreta libreta = new([s_primera, s_segunda, s_tercera])
        {
            Meses =
            {
                [s_primera] = [() => Mes(s_diciembre)],
                [s_segunda] =
                [
                    () =>
                    {
                        parada.Cancel();
                        throw new OperationCanceledException(parada.Token);
                    },
                ],
                [s_tercera] = [() => Mes(s_diciembre)],
            },
        };

        using MetricasDelCorte metricas = new();
        using EspiaDeMedidas espia = new();
        Anotador registro = new();

        await using (ServiceProvider proveedor = Montar(libreta))
        {
            await Should.ThrowAsync<OperationCanceledException>(
                () => Avanzador(proveedor, metricas, registro).UnaVueltaAsync(parada.Token));
        }

        libreta.Avances.Select(avance => avance.EmpresaId).Distinct().ShouldBe([s_primera, s_segunda]);
        espia.Leer("bastion.inventario.corte.avances_fallidos").ShouldBe(0, "apagar la API no es un fallo");
        registro.De(8702).ShouldBeEmpty();
    }

    [Fact]
    public async Task La_parada_se_mira_entre_un_mes_y_el_siguiente()
    {
        // La parada llega mientras se hace el segundo de tres meses, y el avance de mentira no mira
        // el token: lo que corta es el bucle, antes de pedir el tercero.
        using CancellationTokenSource parada = new();

        Libreta libreta = new([s_primera, s_segunda])
        {
            Meses =
            {
                [s_primera] =
                [
                    () => Mes(new DateOnly(2026, 10, 1)),
                    () =>
                    {
                        parada.Cancel();
                        return Mes(s_noviembre);
                    },
                    () => Mes(s_diciembre),
                ],
                [s_segunda] = [() => Mes(s_diciembre)],
            },
        };

        using MetricasDelCorte metricas = new();
        using EspiaDeMedidas espia = new();
        Anotador registro = new();

        await using (ServiceProvider proveedor = Montar(libreta))
        {
            await Should.ThrowAsync<OperationCanceledException>(
                () => Avanzador(proveedor, metricas, registro).UnaVueltaAsync(parada.Token));
        }

        libreta.Avances.ShouldBe([(s_primera, s_diciembre), (s_primera, s_diciembre)]);
        libreta.Cuadradas.ShouldBeEmpty();
        registro.De(8701).Count().ShouldBe(2, "los dos meses que acabaron se quedan, y se anotan");
        espia.Leer("bastion.inventario.corte.avances_fallidos").ShouldBe(0);
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
            Meses =
            {
                [s_primera] = [() => Mes(s_diciembre)],
                [s_segunda] = [() => throw ajena],
                [s_tercera] = [() => Mes(s_diciembre)],
            },
        };

        using MetricasDelCorte metricas = new();
        using EspiaDeMedidas espia = new();
        Anotador registro = new();

        await using (ServiceProvider proveedor = Montar(libreta))
        {
            await Avanzador(proveedor, metricas, registro).UnaVueltaAsync(CancellationToken.None);
        }

        libreta.Avances.Select(avance => avance.EmpresaId).Distinct().ShouldBe([s_primera, s_segunda, s_tercera]);
        espia.Leer("bastion.inventario.corte.avances_fallidos").ShouldBe(1);
        registro.De(8702).ShouldHaveSingleItem().Fallo.ShouldBeSameAs(ajena);
    }

    [Fact]
    public async Task Un_descuadre_es_un_aviso_con_sus_cifras_y_cuenta_cada_uno()
    {
        Libreta libreta = new([s_primera, s_segunda])
        {
            Meses = { [s_primera] = [() => Mes(s_diciembre)], [s_segunda] = [() => Mes(s_diciembre)] },
            Cuadres =
            {
                [s_primera] = () => Cuadrado(
                    s_noviembre,
                    s_diciembre,
                    Descuadre("instantanea-valor"),
                    Descuadre("instantanea"),
                    Descuadre("instantanea")),
                [s_segunda] = () => Cuadrado(s_diciembre, s_diciembre),
            },
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
            $"El cuadre de la empresa {s_primera}, de 2026-11 a 2026-12, encontró 3 descuadres " +
            "(instantanea, instantanea-valor) en 7 existencias y 40 instantáneas comparadas.");
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
    public void El_retraso_es_el_del_corte_mas_viejo_y_sin_corte_cuenta_desde_el_primer_movimiento()
    {
        AvanzadorDelCorte.MesesDeRetraso([], s_diciembre).ShouldBe(0, "sin empresas, nadie va atrás");

        AvanzadorDelCorte.MesesDeRetraso(
            [Corte(s_primera, s_diciembre), Corte(s_segunda, s_diciembre)], s_diciembre)
            .ShouldBe(0, "todas llegan al mes que toca");

        AvanzadorDelCorte.MesesDeRetraso(
            [Corte(s_primera, s_diciembre), Corte(s_segunda, new DateOnly(2026, 9, 1))], s_diciembre)
            .ShouldBe(3, "el más viejo manda, aunque no sea el primero de la lista");

        AvanzadorDelCorte.MesesDeRetraso([Corte(s_primera, new DateOnly(2025, 11, 1))], s_diciembre)
            .ShouldBe(13, "los años cuentan: de noviembre a diciembre del año siguiente van trece meses");

        AvanzadorDelCorte.MesesDeRetraso([Corte(s_primera, new DateOnly(2027, 2, 1))], s_diciembre)
            .ShouldBe(0, "un corte por delante del mes que toca no es un retraso negativo");

        AvanzadorDelCorte.MesesDeRetraso(
            [Corte(s_primera, new DateOnly(2027, 2, 1)), Corte(s_segunda, new DateOnly(2026, 11, 1))],
            s_diciembre)
            .ShouldBe(1, "ni compensa el retraso de otra empresa");

        // Sin corte: el avance no la ha avanzado nunca. Su corte estaría en el mes anterior al de su
        // primer movimiento, y desde ahí le faltan los meses.
        AvanzadorDelCorte.MesesDeRetraso(
            [new CorteDeUnaEmpresa(s_primera, null, new DateOnly(2026, 12, 31))], s_diciembre)
            .ShouldBe(1, "un primer movimiento de diciembre deja el corte que falta en noviembre");

        AvanzadorDelCorte.MesesDeRetraso(
            [new CorteDeUnaEmpresa(s_primera, null, new DateOnly(2026, 1, 1))], s_diciembre)
            .ShouldBe(12, "el primer movimiento del primer día cuenta su mes entero");

        AvanzadorDelCorte.MesesDeRetraso(
            [Corte(s_primera, new DateOnly(2026, 10, 1)), new CorteDeUnaEmpresa(s_segunda, null, null)],
            s_diciembre)
            .ShouldBe(2, "sin corte y sin movimientos no hay instantánea que falte");
    }

    [Fact]
    public async Task Una_vuelta_anota_lo_que_tarda_cada_avance_y_el_retraso_con_el_que_acaba()
    {
        // La más vieja al empezar es la primera, seis meses atrás, y avanza los seis. La que se queda
        // atrás es la tercera: avanza noviembre y falla en diciembre. El retraso es el suyo, y se
        // mide al acabar.
        List<Func<MesAvanzado>> deJulioADiciembre =
            [.. Enumerable.Range(7, 6).Select(mes => (Func<MesAvanzado>)(() => Mes(new DateOnly(2026, mes, 1))))];

        Libreta libreta = new([s_primera, s_segunda, s_tercera])
        {
            Meses =
            {
                [s_primera] = deJulioADiciembre,
                [s_tercera] =
                [
                    () => Mes(s_noviembre, milisegundos: 900),
                    () => throw new InvalidOperationException("el plazo de la orden se ha agotado"),
                ],
            },
        };

        libreta.Cortes[s_primera] = Corte(s_primera, new DateOnly(2026, 6, 1));
        libreta.Cortes[s_segunda] = Corte(s_segunda, s_diciembre);
        libreta.Cortes[s_tercera] = Corte(s_tercera, new DateOnly(2026, 10, 1));

        using MetricasDelCorte metricas = new();
        using EspiaDeMedidas espia = new();

        await using (ServiceProvider proveedor = Montar(libreta))
        {
            await Avanzador(proveedor, metricas, new Anotador()).UnaVueltaAsync(CancellationToken.None);
        }

        espia.Serie("bastion.inventario.corte.duracion_del_avance").ShouldBe(
            [1.5, 1.5, 1.5, 1.5, 1.5, 1.5, 0.9],
            "en segundos, una por mes, y solo los meses que terminan: ni el que ya llegaba ni el que falla");

        espia.Serie("bastion.inventario.corte.meses_de_retraso").ShouldBe(
            [1],
            "una medida por vuelta, con los cortes como quedan: el noviembre de la tercera se queda aunque " +
            "falle diciembre, y medida antes de avanzar serían 6");
    }

    [Fact]
    public void La_duracion_va_en_segundos_con_sus_cubos_y_el_retraso_en_meses_sin_la_empresa()
    {
        using MetricasDelCorte metricas = new();
        using EspiaDeMedidas espia = new();

        metricas.AnotarDuracionDelAvance(TimeSpan.FromMilliseconds(27_693));
        metricas.AnotarMesesDeRetraso(3);

        espia.Serie("bastion.inventario.corte.duracion_del_avance").ShouldBe([27.693]);
        espia.Serie("bastion.inventario.corte.meses_de_retraso").ShouldBe([3]);

        espia.Unidad("bastion.inventario.corte.duracion_del_avance").ShouldBe("s");
        espia.Unidad("bastion.inventario.corte.meses_de_retraso").ShouldBe("{mes}");

        // Los cubos que recomienda el instrumento, y en ellos lo que decide: el disparador y el techo.
        espia.Cubos("bastion.inventario.corte.duracion_del_avance").ShouldBe(MetricasDelCorte.CubosDeLaDuracion);
        MetricasDelCorte.CubosDeLaDuracion.ShouldContain(10, "el disparador del ADR-0064");
        MetricasDelCorte.CubosDeLaDuracion.ShouldContain(30, "el plazo de cada orden, que es el techo");

        espia.Etiquetas.ShouldBe(0, "una etiqueta con la empresa sería una serie por cliente en el recolector");
        espia.Medidas.ShouldBe(2, "sin medidas, contar las etiquetas no diría nada");
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

    private static MesAvanzado Mes(DateOnly mes, int milisegundos = 1500) =>
        new(mes, 40, TimeSpan.FromMilliseconds(milisegundos));

    private static CuadreHecho Cuadrado(DateOnly desde, DateOnly hasta, params Descuadre[] descuadres) =>
        new(desde, hasta, TimeSpan.FromMilliseconds(250), new CuadreDeLasExistencias(7, 40, 3, 0, 0, descuadres));

    private static CorteDeUnaEmpresa Corte(Guid empresaId, DateOnly hastaElMes) => new(empresaId, hastaElMes, null);

    private static Descuadre Descuadre(string que) =>
        new(que, Guid.CreateVersion7(), Guid.CreateVersion7(), null, null, null, s_diciembre, 1m, 2m, 1, null, null);

    /// <summary>Lo que el avance de mentira contesta, y lo que ha visto.</summary>
    /// <param name="empresas">Las empresas que devuelve la lista.</param>
    private sealed class Libreta(IReadOnlyList<Guid> empresas)
    {
        /// <summary>
        /// Lo que contesta cada paso del avance de una empresa, en orden; lanzar es fallar. Cuando se
        /// acaban, o si la empresa no tiene, el corte ya llega.
        /// </summary>
        public Dictionary<Guid, List<Func<MesAvanzado>>> Meses { get; } = [];

        /// <summary>Lo que contesta el cuadre de una empresa; si no tiene, no le quedaba nada pendiente.</summary>
        public Dictionary<Guid, Func<CuadreHecho?>> Cuadres { get; } = [];

        public IReadOnlyList<Guid> Empresas => empresas;

        /// <summary>Cada llamada al paso del avance, con su empresa y el mes al que tiene que llegar.</summary>
        public List<(Guid EmpresaId, DateOnly HastaElMes)> Avances { get; } = [];

        /// <summary>Cada llamada al cuadre, con su empresa.</summary>
        public List<Guid> Cuadradas { get; } = [];

        public int Instancias { get; set; }

        /// <summary>
        /// El corte de cada empresa: el que tiene al empezar, y el que le deja cada mes que termina.
        /// </summary>
        public Dictionary<Guid, CorteDeUnaEmpresa> Cortes { get; } = [];
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

        public async Task<MesAvanzado?> AvanzarUnMesAsync(
            Guid empresaId, DateOnly hastaElMes, CancellationToken cancelacion)
        {
            await Task.Yield();

            int hechos = _libreta.Avances.Count(avance => avance.EmpresaId == empresaId);
            _libreta.Avances.Add((empresaId, hastaElMes));

            if (!_libreta.Meses.TryGetValue(empresaId, out List<Func<MesAvanzado>>? meses) || hechos >= meses.Count)
            {
                return null;
            }

            MesAvanzado hecho = meses[hechos]();
            _libreta.Cortes[empresaId] = new CorteDeUnaEmpresa(empresaId, hecho.Mes, null);

            return hecho;
        }

        public async Task<CuadreHecho?> CuadrarLoPendienteAsync(Guid empresaId, CancellationToken cancelacion)
        {
            await Task.Yield();

            _libreta.Cuadradas.Add(empresaId);

            return _libreta.Cuadres.TryGetValue(empresaId, out Func<CuadreHecho?>? cuadre) ? cuadre() : null;
        }

        public Task<IReadOnlyList<CorteDeUnaEmpresa>> CortesAsync(CancellationToken cancelacion) =>
            Task.FromResult<IReadOnlyList<CorteDeUnaEmpresa>>(
                [.. _libreta.Cortes.Values.OrderBy(corte => corte.EmpresaId)]);
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
    // publica en los contadores, guarda en su serie cada medida del histograma y del indicador,
    // guarda la unidad y los cubos de cada uno, y cuenta las medidas que llevan alguna etiqueta.
    private sealed class EspiaDeMedidas : IDisposable
    {
        private readonly Dictionary<string, long> _valores = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<double>> _series = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string?> _unidades = new(StringComparer.Ordinal);
        private readonly Dictionary<string, IReadOnlyList<double>?> _cubos = new(StringComparer.Ordinal);
        private readonly MeterListener _oyente = new();

        public EspiaDeMedidas()
        {
            _oyente.InstrumentPublished = (instrumento, oyente) =>
            {
                if (string.Equals(instrumento.Meter.Name, ModuloDeInventario.MedidorDelCorte, StringComparison.Ordinal))
                {
                    _unidades[instrumento.Name] = instrumento.Unit;
                    _cubos[instrumento.Name] = (instrumento as Histogram<double>)?.Advice?.HistogramBucketBoundaries;
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

            _oyente.SetMeasurementEventCallback<double>((instrumento, medida, etiquetas, _) =>
                Anotar(instrumento.Name, medida, etiquetas.IsEmpty));

            _oyente.SetMeasurementEventCallback<int>((instrumento, medida, etiquetas, _) =>
                Anotar(instrumento.Name, medida, etiquetas.IsEmpty));

            _oyente.Start();
        }

        public int Medidas { get; private set; }

        public int Etiquetas { get; private set; }

        public long Leer(string instrumento) => _valores.GetValueOrDefault(instrumento);

        public string? Unidad(string instrumento) => _unidades.GetValueOrDefault(instrumento);

        public List<double> Serie(string instrumento) => _series.GetValueOrDefault(instrumento) ?? [];

        public IReadOnlyList<double>? Cubos(string instrumento) => _cubos.GetValueOrDefault(instrumento);

        private void Anotar(string instrumento, double medida, bool sinEtiquetas)
        {
            if (!_series.TryGetValue(instrumento, out List<double>? serie))
            {
                _series[instrumento] = serie = [];
            }

            serie.Add(medida);
            Medidas++;

            if (!sinEtiquetas)
            {
                Etiquetas++;
            }
        }

        public void Dispose() => _oyente.Dispose();
    }
}
