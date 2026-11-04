using System.Diagnostics.Metrics;
using Bastion.Api.FunctionalTests.Salud;
using Bastion.BuildingBlocks.Domain.Fechas;
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
/// justo ahí. Las empresas de mentira están en Madrid, salvo en los casos de las zonas.
/// </para>
/// <para>
/// <b>Los casos de las zonas, desde el 3.5 (ADR-0079), lo paran a las 23:30 UTC del 31 de
/// octubre de 2026</b>: en Madrid ya es noviembre, y en Canarias todavía octubre.
/// </para>
/// </remarks>
public sealed class ElTrabajoMensualAvanzaElCorteTests
{
    private static readonly DateTimeOffset s_primeraMediaHoraDelAnio = new(2027, 1, 1, 0, 30, 0, TimeSpan.Zero);

    // Madrid, el 1 de noviembre a las 00:30; Canarias, el 31 de octubre a las 23:30.
    private static readonly DateTimeOffset s_finDeOctubreEnCanarias = new(2026, 10, 31, 23, 30, 0, TimeSpan.Zero);

    private static readonly DateOnly s_julio = new(2026, 7, 1);
    private static readonly DateOnly s_septiembre = new(2026, 9, 1);
    private static readonly DateOnly s_octubre = new(2026, 10, 1);
    private static readonly DateOnly s_noviembre = new(2026, 11, 1);
    private static readonly DateOnly s_diciembre = new(2026, 12, 1);

    private static readonly Guid s_primera = Guid.Parse("01900000-0000-7000-8000-000000000001");
    private static readonly Guid s_segunda = Guid.Parse("01900000-0000-7000-8000-000000000002");
    private static readonly Guid s_tercera = Guid.Parse("01900000-0000-7000-8000-000000000003");

    [Fact]
    public void El_mes_que_toca_es_el_anterior_al_dia_mas_atrasado_de_las_zonas_de_la_empresa()
    {
        AvanzadorDelCorte.MesQueToca(new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.Zero), [ZonaHoraria.Madrid])
            .ShouldBe(s_septiembre);

        AvanzadorDelCorte.MesQueToca(s_primeraMediaHoraDelAnio, [ZonaHoraria.Madrid, ZonaHoraria.Canarias])
            .ShouldBe(s_diciembre, "en enero, el mes que toca es diciembre del año anterior");

        // EL FIN DE MES CON DOS ZONAS: octubre ha terminado en Madrid y no en Canarias.
        AvanzadorDelCorte.MesQueToca(s_finDeOctubreEnCanarias, [ZonaHoraria.Madrid])
            .ShouldBe(s_octubre, "solo en Madrid, octubre ya ha terminado");

        AvanzadorDelCorte.MesQueToca(s_finDeOctubreEnCanarias, [ZonaHoraria.Madrid, ZonaHoraria.Canarias])
            .ShouldBe(s_septiembre, "con un almacén en Canarias, octubre no ha terminado en todos");

        AvanzadorDelCorte.MesQueToca(s_finDeOctubreEnCanarias, [ZonaHoraria.Canarias, ZonaHoraria.Madrid])
            .ShouldBe(s_septiembre, "el orden de las zonas no cuenta");

        // Ceuta va con la península: a las 22:30 UTC del 31 de marzo, con la hora de verano, allí ya
        // es abril y en Canarias todavía marzo.
        DateTimeOffset finDeMarzo = new(2026, 3, 31, 22, 30, 0, TimeSpan.Zero);

        AvanzadorDelCorte.MesQueToca(finDeMarzo, [ZonaHoraria.Ceuta]).ShouldBe(new DateOnly(2026, 3, 1));
        AvanzadorDelCorte.MesQueToca(finDeMarzo, [ZonaHoraria.Ceuta, ZonaHoraria.Canarias])
            .ShouldBe(new DateOnly(2026, 2, 1));

        // El mismo instante, escrito a dos horas por delante de UTC: dice 1 de abril, y en Canarias
        // sigue siendo el 31 de marzo. Hoy es el de la zona, no el de la hora que trae el instante.
        AvanzadorDelCorte.MesQueToca(finDeMarzo.ToOffset(TimeSpan.FromHours(2)), [ZonaHoraria.Canarias])
            .ShouldBe(new DateOnly(2026, 2, 1));
    }

    [Fact]
    public async Task Cada_empresa_avanza_hasta_el_mes_de_sus_zonas_y_su_cuadre_recibe_las_mismas()
    {
        // El caso de fin de mes con dos zonas. La primera está solo en Madrid, y ha terminado octubre.
        // La segunda es de Madrid con un almacén en Canarias, y la tercera, solo canaria: las dos
        // siguen en octubre. Con la zona de la empresa, la segunda avanzaría hasta octubre.
        Libreta libreta = new([s_primera, s_segunda, s_tercera])
        {
            Zonas =
            {
                [s_primera] = () => [ZonaHoraria.Madrid],
                [s_segunda] = () => [ZonaHoraria.Madrid, ZonaHoraria.Canarias],
                [s_tercera] = () => [ZonaHoraria.Canarias],
            },
            Meses =
            {
                [s_primera] = [() => Mes(s_octubre)],
                [s_segunda] = [() => Mes(s_septiembre)],
            },
        };

        using MetricasDelCorte metricas = new();
        using EspiaDeMedidas espia = new();
        Anotador registro = new();

        await using (ServiceProvider proveedor = Montar(libreta))
        {
            await Avanzador(proveedor, metricas, registro, s_finDeOctubreEnCanarias)
                .UnaVueltaAsync(CancellationToken.None);
        }

        libreta.Avances.ShouldBe(
            [
                (s_primera, s_octubre), (s_primera, s_octubre),
                (s_segunda, s_septiembre), (s_segunda, s_septiembre),
                (s_tercera, s_septiembre),
            ],
            "cada empresa, hasta el mes anterior al día más atrasado de sus zonas");

        // El cuadre de cada una recibe las zonas que se leyeron para su mes.
        libreta.Cuadradas.ShouldBe([s_primera, s_segunda, s_tercera]);
        libreta.ZonasDelCuadre[s_primera].ShouldBe([ZonaHoraria.Madrid]);
        libreta.ZonasDelCuadre[s_segunda].ShouldBe([ZonaHoraria.Madrid, ZonaHoraria.Canarias]);
        libreta.ZonasDelCuadre[s_tercera].ShouldBe([ZonaHoraria.Canarias]);

        libreta.Instancias.ShouldBe(5, "las zonas se leen en el ámbito de su empresa, sin uno más");

        registro.De(8701).Select(linea => linea.Mensaje).ShouldBe(
        [
            $"Corte de la empresa {s_primera} avanzado hasta 2026-10: 40 instantáneas escritas en 1500 ms.",
            $"Corte de la empresa {s_segunda} avanzado hasta 2026-09: 40 instantáneas escritas en 1500 ms.",
        ]);

        registro.De(8700).ShouldHaveSingleItem().Mensaje.ShouldBe(
            "Vuelta del avance del corte: 3 empresas con existencias, 2 avanzadas y 0 con fallo.");

        espia.Serie("bastion.inventario.corte.meses_de_retraso").ShouldBe(
            [0], "cada una ha llegado al mes de sus zonas");
    }

    [Fact]
    public async Task El_retraso_mide_cada_corte_contra_su_mes_y_unas_zonas_que_no_se_leen_son_un_fallo_que_tambien_cuenta()
    {
        using MetricasDelCorte metricas = new();
        using EspiaDeMedidas espia = new();

        // PRIMERA VUELTA: la primera, solo en Madrid, falla al avanzar octubre y se queda en
        // septiembre, un mes por detrás del suyo. Contra el de la segunda, que tiene un almacén en
        // Canarias, no le faltaría ninguno.
        Libreta cadaUnaElSuyo = new([s_primera, s_segunda])
        {
            Zonas =
            {
                [s_primera] = () => [ZonaHoraria.Madrid],
                [s_segunda] = () => [ZonaHoraria.Madrid, ZonaHoraria.Canarias],
            },
            Meses = { [s_primera] = [() => throw new InvalidOperationException("el plazo de la orden se ha agotado")] },
        };

        cadaUnaElSuyo.Cortes[s_primera] = Corte(s_primera, s_septiembre);
        cadaUnaElSuyo.Cortes[s_segunda] = Corte(s_segunda, s_septiembre);

        await using (ServiceProvider proveedor = Montar(cadaUnaElSuyo))
        {
            await Avanzador(proveedor, metricas, new Anotador(), s_finDeOctubreEnCanarias)
                .UnaVueltaAsync(CancellationToken.None);
        }

        // SEGUNDA VUELTA: las zonas de la primera no se leen. Es un fallo de su empresa, que no
        // avanza ni cuadra, y no para a la segunda. Su corte, de julio, se mide igual, contra el
        // mes de la más atrasada de las zonas admitidas: septiembre.
        InvalidOperationException caida = new("Organización no contesta");

        Libreta sinZonas = new([s_primera, s_segunda])
        {
            Zonas = { [s_primera] = () => throw caida },
            Meses = { [s_segunda] = [() => Mes(s_octubre)] },
        };

        sinZonas.Cortes[s_primera] = Corte(s_primera, s_julio);
        sinZonas.Cortes[s_segunda] = Corte(s_segunda, s_septiembre);

        Anotador registro = new();

        await using (ServiceProvider proveedor = Montar(sinZonas))
        {
            await Avanzador(proveedor, metricas, registro, s_finDeOctubreEnCanarias)
                .UnaVueltaAsync(CancellationToken.None);
        }

        espia.Serie("bastion.inventario.corte.meses_de_retraso").ShouldBe(
            [1, 2],
            "la primera vuelta, contra el octubre de la primera; la segunda, con la empresa sin zonas " +
            "medida desde julio: sin medirla, sería 0, y una empresa cuyas zonas fallan cada hora no " +
            "avanzaría nunca sin que nada lo dijera");

        sinZonas.Avances.ShouldBe([(s_segunda, s_octubre), (s_segunda, s_octubre)]);
        sinZonas.Cuadradas.ShouldBe([s_segunda], "sin zonas no hay día hasta el que cuadrar");

        espia.Leer("bastion.inventario.corte.avances_fallidos").ShouldBe(2, "uno en cada vuelta");

        Linea fallo = registro.De(8702).ShouldHaveSingleItem();
        fallo.Fallo.ShouldBeSameAs(caida);
        fallo.Mensaje.ShouldBe(
            $"El avance o el cuadre del corte de la empresa {s_primera} ha fallado. Se reintenta en la " +
            "hora siguiente, desde el último mes que llegó a hacer.");

        registro.De(8700).ShouldHaveSingleItem().Mensaje.ShouldBe(
            "Vuelta del avance del corte: 2 empresas con existencias, 1 avanzadas y 1 con fallo.");
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
            "Vuelta del avance del corte: 3 empresas con existencias, 2 avanzadas y 0 con fallo.");

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
            "Vuelta del avance del corte: 2 empresas con existencias, 0 avanzadas y 0 con fallo.");
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
            "Vuelta del avance del corte: 2 empresas con existencias, 2 avanzadas y 1 con fallo.");
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
            $"El avance o el cuadre del corte de la empresa {s_segunda} ha fallado. Se reintenta en la " +
            "hora siguiente, desde el último mes que llegó a hacer.");

        registro.De(8700).ShouldHaveSingleItem().Mensaje.ShouldBe(
            "Vuelta del avance del corte: 3 empresas con existencias, 2 avanzadas y 1 con fallo.");
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
        static DateOnly Diciembre(Guid empresaId) => s_diciembre;

        AvanzadorDelCorte.MesesDeRetraso([], Diciembre).ShouldBe(0, "sin empresas, nadie va atrás");

        AvanzadorDelCorte.MesesDeRetraso(
            [Corte(s_primera, s_diciembre), Corte(s_segunda, s_diciembre)], Diciembre)
            .ShouldBe(0, "todas llegan al mes que toca");

        AvanzadorDelCorte.MesesDeRetraso(
            [Corte(s_primera, s_diciembre), Corte(s_segunda, s_septiembre)], Diciembre)
            .ShouldBe(3, "el más viejo manda, aunque no sea el primero de la lista");

        AvanzadorDelCorte.MesesDeRetraso([Corte(s_primera, new DateOnly(2025, 11, 1))], Diciembre)
            .ShouldBe(13, "los años cuentan: de noviembre a diciembre del año siguiente van trece meses");

        AvanzadorDelCorte.MesesDeRetraso([Corte(s_primera, new DateOnly(2027, 2, 1))], Diciembre)
            .ShouldBe(0, "un corte por delante del mes que toca no es un retraso negativo");

        AvanzadorDelCorte.MesesDeRetraso(
            [Corte(s_primera, new DateOnly(2027, 2, 1)), Corte(s_segunda, s_noviembre)], Diciembre)
            .ShouldBe(1, "ni compensa el retraso de otra empresa");

        // Sin corte: el avance no la ha avanzado nunca. Su corte estaría en el mes anterior al de su
        // primer movimiento, y desde ahí le faltan los meses.
        AvanzadorDelCorte.MesesDeRetraso(
            [new CorteDeUnaEmpresa(s_primera, null, new DateOnly(2026, 12, 31))], Diciembre)
            .ShouldBe(1, "un primer movimiento de diciembre deja el corte que falta en noviembre");

        AvanzadorDelCorte.MesesDeRetraso(
            [new CorteDeUnaEmpresa(s_primera, null, new DateOnly(2026, 1, 1))], Diciembre)
            .ShouldBe(12, "el primer movimiento del primer día cuenta su mes entero");

        AvanzadorDelCorte.MesesDeRetraso(
            [Corte(s_primera, s_octubre), new CorteDeUnaEmpresa(s_segunda, null, null)], Diciembre)
            .ShouldBe(2, "sin corte y sin movimientos no hay instantánea que falte");

        // Cada corte, contra el mes de su empresa (ADR-0079): los dos en septiembre, y solo a la
        // primera le tocaba octubre.
        AvanzadorDelCorte.MesesDeRetraso(
            [Corte(s_primera, s_septiembre), Corte(s_segunda, s_septiembre)],
            empresaId => empresaId == s_primera ? s_octubre : s_septiembre)
            .ShouldBe(1, "a la primera le falta octubre, y a la segunda nada");
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
    public void Lo_que_falta_por_cuadrar_va_del_primer_mes_sin_cuadrar_al_corte_y_manda_la_mas_atrasada()
    {
        AvanzadorDelCorte.MesesSinCuadrar([]).ShouldBe(0, "sin empresas, no falta nada");

        AvanzadorDelCorte.MesesSinCuadrar([Corte(s_primera, s_diciembre), Corte(s_segunda, s_noviembre)])
            .ShouldBe(0, "las dos lo tienen cuadrado hasta su corte, aunque los cortes no sean el mismo");

        AvanzadorDelCorte.MesesSinCuadrar([SinCuadrar(s_primera, s_diciembre, s_diciembre)])
            .ShouldBe(1, "el mes del corte sin cuadrar es un mes, no cero: los dos bordes cuentan");

        AvanzadorDelCorte.MesesSinCuadrar(
            [SinCuadrar(s_primera, s_diciembre, s_diciembre), SinCuadrar(s_segunda, s_diciembre, new DateOnly(2026, 9, 1))])
            .ShouldBe(4, "la más atrasada manda, aunque no sea la primera de la lista");

        AvanzadorDelCorte.MesesSinCuadrar([SinCuadrar(s_primera, s_diciembre, new DateOnly(2025, 11, 1))])
            .ShouldBe(14, "los años cuentan: de noviembre a diciembre del año siguiente van catorce meses");

        // Hasta el corte, y no hasta el mes que toca: lo que el corte no alcanza lo cuenta el retraso.
        AvanzadorDelCorte.MesesSinCuadrar([SinCuadrar(s_primera, new DateOnly(2026, 6, 1), new DateOnly(2026, 5, 1))])
            .ShouldBe(2, "de mayo a un corte de junio, aunque el mes que toca sea diciembre");

        AvanzadorDelCorte.MesesSinCuadrar([new CorteDeUnaEmpresa(s_primera, null, new DateOnly(2026, 1, 1))])
            .ShouldBe(0, "sin corte no hay instantáneas que cuadrar");
    }

    [Fact]
    public async Task Una_vuelta_anota_lo_que_falta_por_cuadrar_y_un_cuadre_parado_lo_deja_por_encima_de_cero()
    {
        // Las tres llegan al mes que toca, así que el retraso acaba en cero. La primera avanza
        // diciembre y lo cuadra. A la segunda, que ya llegaba, le falta desde octubre, y su cuadre
        // falla otra vez en octubre. La tercera, cuadrada hasta junio, avanza noviembre y diciembre, y
        // su cuadre para en noviembre: le queda diciembre, y antes de cuadrar le faltaban seis meses.
        Libreta libreta = new([s_primera, s_segunda, s_tercera])
        {
            Meses =
            {
                [s_primera] = [() => Mes(s_diciembre)],
                [s_tercera] = [() => Mes(s_noviembre), () => Mes(s_diciembre)],
            },
            Cuadres =
            {
                [s_primera] = () => Cuadrado(s_diciembre, s_diciembre),
                [s_segunda] = () => throw new InvalidOperationException("el plazo de la orden se ha agotado"),
                [s_tercera] = () => Cuadrado(new DateOnly(2026, 7, 1), s_noviembre),
            },
        };

        libreta.Cortes[s_primera] = Corte(s_primera, s_noviembre);
        libreta.Cortes[s_segunda] = SinCuadrar(s_segunda, s_diciembre, new DateOnly(2026, 10, 1));
        libreta.Cortes[s_tercera] = SinCuadrar(s_tercera, new DateOnly(2026, 10, 1), new DateOnly(2026, 7, 1));

        using MetricasDelCorte metricas = new();
        using EspiaDeMedidas espia = new();

        await using (ServiceProvider proveedor = Montar(libreta))
        {
            await Avanzador(proveedor, metricas, new Anotador()).UnaVueltaAsync(CancellationToken.None);
        }

        espia.Serie("bastion.inventario.corte.meses_de_retraso").ShouldBe(
            [0], "todas llegan, y por eso hace falta otro indicador");

        espia.Serie("bastion.inventario.corte.meses_sin_cuadrar").ShouldBe(
            [3],
            "una medida por vuelta, con lo cuadrado como queda: de octubre a diciembre en la segunda, " +
            "que manda sobre el diciembre de la tercera; y la primera, cuadrada, no suma. Medida antes " +
            "de los cuadres, la tercera daría 6");
    }

    [Fact]
    public void La_duracion_va_en_segundos_con_sus_cubos_y_los_dos_indicadores_en_meses_sin_la_empresa()
    {
        using MetricasDelCorte metricas = new();
        using EspiaDeMedidas espia = new();

        metricas.AnotarDuracionDelAvance(TimeSpan.FromMilliseconds(27_693));
        metricas.AnotarMesesDeRetraso(3);
        metricas.AnotarMesesSinCuadrar(2);

        espia.Serie("bastion.inventario.corte.duracion_del_avance").ShouldBe([27.693]);
        espia.Serie("bastion.inventario.corte.meses_de_retraso").ShouldBe([3]);
        espia.Serie("bastion.inventario.corte.meses_sin_cuadrar").ShouldBe([2]);

        espia.Unidad("bastion.inventario.corte.duracion_del_avance").ShouldBe("s");
        espia.Unidad("bastion.inventario.corte.meses_de_retraso").ShouldBe("{mes}");
        espia.Unidad("bastion.inventario.corte.meses_sin_cuadrar").ShouldBe("{mes}");

        // Los cubos que recomienda el instrumento, y en ellos lo que decide: el disparador y el techo.
        espia.Cubos("bastion.inventario.corte.duracion_del_avance").ShouldBe(MetricasDelCorte.CubosDeLaDuracion);
        MetricasDelCorte.CubosDeLaDuracion.ShouldContain(10, "el disparador del ADR-0064");
        MetricasDelCorte.CubosDeLaDuracion.ShouldContain(30, "el plazo de cada orden, que es el techo");

        espia.Etiquetas.ShouldBe(0, "una etiqueta con la empresa sería una serie por cliente en el recolector");
        espia.Medidas.ShouldBe(3, "sin medidas, contar las etiquetas no diría nada");
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
        ServiceProvider proveedor, MetricasDelCorte metricas, Anotador registro, DateTimeOffset? ahora = null) =>
        new(
            proveedor.GetRequiredService<IServiceScopeFactory>(),
            new RelojParado(ahora ?? s_primeraMediaHoraDelAnio),
            metricas,
            registro);

    private static MesAvanzado Mes(DateOnly mes, int milisegundos = 1500) =>
        new(mes, 40, TimeSpan.FromMilliseconds(milisegundos));

    private static CuadreHecho Cuadrado(DateOnly desde, DateOnly hasta, params Descuadre[] descuadres) =>
        new(desde, hasta, TimeSpan.FromMilliseconds(250), new CuadreDeLasExistencias(7, 40, 3, 0, 0, descuadres));

    private static CorteDeUnaEmpresa Corte(Guid empresaId, DateOnly hastaElMes) => new(empresaId, hastaElMes, null);

    private static CorteDeUnaEmpresa SinCuadrar(Guid empresaId, DateOnly hastaElMes, DateOnly desde) =>
        new(empresaId, hastaElMes, null, desde);

    private static Descuadre Descuadre(string que) =>
        new(que, Guid.CreateVersion7(), Guid.CreateVersion7(), null, null, null, s_diciembre, 1m, 2m, 1, null, null);

    /// <summary>Lo que el avance de mentira contesta, y lo que ha visto.</summary>
    /// <param name="empresas">Las empresas que devuelve la lista.</param>
    private sealed class Libreta(IReadOnlyList<Guid> empresas)
    {
        /// <summary>
        /// Lo que contestan las zonas de una empresa; lanzar es fallar. Si no tiene, está en Madrid.
        /// </summary>
        public Dictionary<Guid, Func<IReadOnlyList<ZonaHoraria>>> Zonas { get; } = [];

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

        /// <summary>Las zonas que ha recibido el cuadre de cada empresa.</summary>
        public Dictionary<Guid, IReadOnlyCollection<ZonaHoraria>> ZonasDelCuadre { get; } = [];

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

        public async Task<IReadOnlyList<ZonaHoraria>> ZonasAsync(Guid empresaId, CancellationToken cancelacion)
        {
            await Task.Yield();

            return _libreta.Zonas.TryGetValue(empresaId, out Func<IReadOnlyList<ZonaHoraria>>? zonas)
                ? zonas()
                : [ZonaHoraria.Madrid];
        }

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

            // Lo que no estaba cuadrado sigue sin estarlo, y el mes nuevo se suma a lo que falta.
            DateOnly? sinCuadrarDesde = _libreta.Cortes.TryGetValue(empresaId, out CorteDeUnaEmpresa? antes)
                ? antes.SinCuadrarDesde ?? hecho.Mes
                : hecho.Mes;
            _libreta.Cortes[empresaId] = new CorteDeUnaEmpresa(empresaId, hecho.Mes, null, sinCuadrarDesde);

            return hecho;
        }

        public async Task<CuadreHecho?> CuadrarLoPendienteAsync(
            Guid empresaId, IReadOnlyCollection<ZonaHoraria> zonas, CancellationToken cancelacion)
        {
            await Task.Yield();

            _libreta.Cuadradas.Add(empresaId);
            _libreta.ZonasDelCuadre[empresaId] = zonas;

            CuadreHecho? hecho = _libreta.Cuadres.TryGetValue(empresaId, out Func<CuadreHecho?>? cuadre)
                ? cuadre()
                : null;

            // Lo cuadrado se queda anotado hasta su último mes, como en la base. Un cuadre que lanza
            // no llega aquí, y lo que faltaba sigue faltando.
            if (hecho is not null && _libreta.Cortes.TryGetValue(empresaId, out CorteDeUnaEmpresa? corte))
            {
                _libreta.Cortes[empresaId] = corte with
                {
                    SinCuadrarDesde = hecho.Hasta == corte.HastaElMes ? null : hecho.Hasta.AddMonths(1),
                };
            }

            return hecho;
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
