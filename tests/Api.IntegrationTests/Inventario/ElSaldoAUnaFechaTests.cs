using System.Globalization;
using System.Net;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Inventario.Contracts.Saldos;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Transferencias;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Saldos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// El saldo a una fecha, por la API: el de cada par es el libro sumado hasta el final de ese día, y
/// lo que vuela hacia el almacén va aparte (ítem 2.14, ADR-0063 §2 a §4 y §8).
/// </summary>
/// <remarks>
/// <para>
/// <b>El oráculo es el libro, y no la sentencia escrita otra vez.</b> El saldo de un par a D es la
/// suma de sus movimientos hasta D. Lo que vuela hacia un almacén a D es lo que salió hacia él y no
/// ha llegado ni ha vuelto, y eso también está en el libro: la suma de los movimientos de las
/// transferencias hacia ese almacén, y de sus inversos, hasta D, con el signo cambiado. Al enviar
/// sale del origen, al recibir entra en el destino, y al anular una que vuela vuelve al origen; las
/// tres patas, con el mismo valor. Ninguna de las dos sumas mira las fechas de la transferencia, que
/// son lo que la sentencia compara.
/// </para>
/// <para>
/// <b>La instantánea se prueba estropeándola</b>, en una transacción que se deshace, porque contra un
/// libro sano dice lo mismo que el libro: una sentencia que leyera el libro entero saldría bien. Con
/// una marca distinta en dos meses, el saldo dice cuál ha leído.
/// </para>
/// <para>
/// <b>Lo que no está aquí.</b> El <c>401</c> y el <c>403</c> los cobra el barrido de
/// <c>LaPuertaDeCadaAccionTests</c>, que descubre la acción como cualquier otra. Qué tablas, columnas
/// y parámetros nombra la sentencia lo dice <c>LaSentenciaDelSaldoNombraLasTablasYLaEmpresaTests</c>,
/// en el carril rápido.
/// </para>
/// <para>
/// <b>Semillas.</b> La 866, la empresa del libro y su primer artículo; la 867, el segundo. La 868, la
/// empresa de la instantánea. La 869, la empresa de la consulta y su primer artículo; la 870, el
/// segundo. La 871 y la 872, las dos empresas que no se ven. Del 873 al 899 quedan libres, por
/// literal y por cálculo.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElSaldoAUnaFechaTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Saldos = "/api/v1/inventario/saldos";

    private readonly ApiDeVerdad _api = new(postgres);
    private readonly List<HttpClient> _clientes = [];

    private static DateOnly Hoy => EscenaDeTransferencia.Hoy;

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (HttpClient cliente in _clientes)
        {
            cliente.Dispose();
        }

        _api.Dispose();
    }

    /// <summary>
    /// El saldo de cada día es el libro hasta ese día, y lo que vuela, lo que salió sin llegar ni
    /// volver: sin corte y con corte, desde antes del primer movimiento hasta hoy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Los días que se piden son los que deciden</b>: cada día con movimiento y el anterior, el
    /// primero y el último de cada mes, ayer y hoy. Una comparación con <c>&lt;</c> en vez de
    /// <c>&lt;=</c> solo se ve el día exacto, y el mes que se lee de la instantánea, en sus bordes.
    /// </para>
    /// <para>
    /// <b>Cuatro transferencias hacia B</b>: una que llegó, una que se anula hoy volando, una que
    /// sigue volando y un borrador con su fecha, que no ha salido. Y una salida de C el día 1 del mes
    /// siguiente al corte, que es el primer día que el saldo lee del libro y no de la instantánea.
    /// </para>
    /// <para>
    /// <b>Todo en el año pasado</b>, para que las fechas hayan pasado cualquier día; las dos
    /// anulaciones son de hoy, y por eso hace falta el ejercicio de este año.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task El_saldo_de_cada_dia_es_el_libro_hasta_ese_dia_y_lo_que_vuela_lo_que_salio_sin_llegar_ni_volver()
    {
        int anio = Hoy.Year - 1;
        EscenaDeTransferencia escena = await MontarAsync(866, "SAF-A", anio);
        EscenaDeTransferencia otro = await ConOtroArticuloAsync(escena, 867);

        await LosMaestrosPorLaApi.CrearEjercicioAsync(escena.Cliente, Hoy.Year);

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 10m, 5m, new DateOnly(anio, 3, 10));
        await escena.SalirAsync(postgres, escena.AlmacenA, escena.UbicacionA, 3m, new DateOnly(anio, 3, 20));
        Guid deMayo = await escena.EntrarAsync(
            postgres, escena.AlmacenA, escena.UbicacionA, 3m, 9m, new DateOnly(anio, 5, 15));

        TransferenciaDto llego = await escena.EnviarAsync(postgres, 2m, new DateOnly(anio, 6, 10));

        using (HttpResponseMessage recepcion = await escena.RecibirPorLaApiAsync(llego.Id, new DateOnly(anio, 7, 5)))
        {
            recepcion.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(recepcion));
        }

        TransferenciaDto seAnula = await escena.EnviarAsync(postgres, 1m, new DateOnly(anio, 8, 20));

        await otro.EntrarAsync(postgres, escena.AlmacenC, escena.UbicacionC, 4m, 2m, new DateOnly(anio, 9, 10));
        await otro.SalirAsync(postgres, escena.AlmacenC, escena.UbicacionC, 1m, new DateOnly(anio, 10, 1));

        TransferenciaDto vuela = await escena.EnviarAsync(postgres, 1m, new DateOnly(anio, 11, 20));

        var delBorrador = new DateOnly(anio, 12, 1);
        await escena.AbrirAsync(postgres, 1m, delBorrador);

        using (HttpResponseMessage anulada = await escena.AnularPorLaApiAsync(seAnula.Id))
        {
            anulada.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(anulada));
        }

        using (HttpResponseMessage anulado = await escena.AnularElAjustePorLaApiAsync(deMayo))
        {
            anulado.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(anulado));
        }

        (Guid, Guid)[] pares =
        [
            (escena.ArticuloId, escena.AlmacenA),
            (escena.ArticuloId, escena.AlmacenB),
            (otro.ArticuloId, escena.AlmacenC),
        ];

        IReadOnlyList<MovimientoStock> libro = await ElLibroAsync(escena.EmpresaId);
        SortedSet<DateOnly> dias = LosDiasQueDeciden(libro, delBorrador, new DateOnly(anio, 2, 1));

        // SIN CORTE, el libro entero hasta D: no hay instantánea que leer.
        await using (InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId))
        {
            (await contexto.CortesDeLaInstantanea.CountAsync()).ShouldBe(0, "el trabajo mensual está apagado aquí");
        }

        (await CadaDiaContraElLibroAsync(escena, libro, dias, pares)).ShouldBeEmpty();

        // CON CORTE en septiembre, la instantánea del mes anterior a D o la del corte, y el libro
        // desde el mes siguiente.
        await LasExistencias.RecalcularAsync(postgres, escena.EmpresaId, new DateOnly(anio, 9, 1));

        (await CadaDiaContraElLibroAsync(escena, libro, dias, pares)).ShouldBeEmpty();

        // LA PAREJA DEL ORÁCULO, escrita a mano, por si el libro y la sentencia se equivocaran igual.
        // Antes del primer movimiento, los tres pares con ceros.
        SaldosAUnaFechaDto antes = await SaldosAsync(escena.Cliente, $"?fecha={Dia(new DateOnly(anio, 3, 9))}");

        antes.Saldos.Total.ShouldBe(3);
        antes.Saldos.Elementos.ShouldAllBe(saldo =>
            saldo.Cantidad == 0m && saldo.Valor == 0m && saldo.CantidadEnTransito == 0m && saldo.ValorEnTransito == 0m);

        // El día de la entrada cuenta, y la salida de cinco días después, no.
        (await ElDeAsync(escena, escena.AlmacenA, new DateOnly(anio, 3, 15)))
            .ShouldBe(new SaldoDto(escena.ArticuloId, escena.AlmacenA, 10m, 50m, 0m, 0m, "EUR"));

        // Siete a 5 y tres a 9 son diez a 6,20; el día del envío salen dos, que vuelan hacia B.
        SaldosAUnaFechaDto envio = await SaldosAsync(escena.Cliente, $"?fecha={Dia(new DateOnly(anio, 6, 10))}");

        Ver(envio, escena.ArticuloId, escena.AlmacenA).Cantidad.ShouldBe(8m);
        Ver(envio, escena.ArticuloId, escena.AlmacenB)
            .ShouldBe(new SaldoDto(escena.ArticuloId, escena.AlmacenB, 0m, 0m, 2m, 12.4m, "EUR"));

        // El día de la recepción ya no vuelan: están en B.
        (await ElDeAsync(escena, escena.AlmacenB, new DateOnly(anio, 7, 4))).CantidadEnTransito.ShouldBe(2m);
        (await ElDeAsync(escena, escena.AlmacenB, new DateOnly(anio, 7, 5)))
            .ShouldBe(new SaldoDto(escena.ArticuloId, escena.AlmacenB, 2m, 12.4m, 0m, 0m, "EUR"));

        // Ayer volaban la que hoy se anula y la que sigue; el borrador, nunca.
        (await ElDeAsync(escena, escena.AlmacenB, Hoy.AddDays(-1)))
            .ShouldBe(new SaldoDto(escena.ArticuloId, escena.AlmacenB, 2m, 12.4m, 2m, 12.4m, "EUR"));

        // HOY, EL SALDO ES LA VALORACIÓN, que es la otra cuenta del mismo libro: la cantidad, el
        // valor y lo que vuela de cada par.
        SaldosAUnaFechaDto hoy = await SaldosAsync(escena.Cliente, $"?fecha={Dia(Hoy)}");

        Ver(hoy, escena.ArticuloId, escena.AlmacenB).CantidadEnTransito.ShouldBe(1m, "solo la que sigue volando");

        foreach ((EscenaDeTransferencia de, Guid almacen) in new[]
        {
            (escena, escena.AlmacenA),
            (escena, escena.AlmacenB),
            (otro, escena.AlmacenC),
        })
        {
            Valoracion valoracion = await de.LaValoracionDeAsync(postgres, almacen);

            Ver(hoy, de.ArticuloId, almacen).ShouldBe(new SaldoDto(
                de.ArticuloId,
                almacen,
                valoracion.Cantidad,
                valoracion.Valor.Cantidad,
                valoracion.EnTransito,
                valoracion.ValorEnTransito.Cantidad,
                valoracion.Divisa));
        }

        (await escena.LaTransferenciaAsync(postgres, vuela.Id)).Estado.ShouldBe(EstadoDeTransferencia.Enviada);
    }

    /// <summary>
    /// El saldo lee la instantánea del mes anterior a D, o la del corte si es anterior, y el libro desde
    /// el mes siguiente: estropeadas dos, cada día dice cuál ha leído.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Agosto lleva diez de más y septiembre, cien</b>, y el corte es septiembre. El 31 de agosto
    /// lee julio y no ve ninguna; del 1 al 30 de septiembre lee agosto; desde el 1 de octubre, y hasta
    /// hoy, septiembre, que pasado octubre ya es el corte y no el mes anterior.
    /// </para>
    /// <para>
    /// <b>Corre la lectura de dentro con el contexto de la transacción</b>, que se deshace: estropear
    /// fuera dejaría la empresa descuadrada para el cuadre de cualquier otro caso que la mirara.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task El_saldo_lee_la_instantanea_del_mes_anterior_o_la_del_corte_y_el_libro_desde_el_mes_siguiente()
    {
        int anio = Hoy.Year - 1;
        EscenaDeTransferencia escena = await MontarAsync(868, "SAF-B", anio);

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 10m, 3m, new DateOnly(anio, 7, 10));
        await escena.SalirAsync(postgres, escena.AlmacenA, escena.UbicacionA, 2m, new DateOnly(anio, 8, 10));
        await escena.SalirAsync(postgres, escena.AlmacenA, escena.UbicacionA, 1m, new DateOnly(anio, 9, 10));
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 5m, 3m, new DateOnly(anio, 10, 10));

        var agosto = new DateOnly(anio, 8, 1);
        var septiembre = new DateOnly(anio, 9, 1);

        await LasExistencias.RecalcularAsync(postgres, escena.EmpresaId, septiembre);

        IReadOnlyList<MovimientoStock> libro = await ElLibroAsync(escena.EmpresaId);

        (DateOnly Dia, decimal Fisico, decimal Valor)[] marcas =
        [
            (new DateOnly(anio, 8, 31), 0m, 0m),
            (septiembre, 10m, 2m),
            (new DateOnly(anio, 9, 30), 10m, 2m),
            (new DateOnly(anio, 10, 1), 100m, 2000m),
            (new DateOnly(anio, 10, 10), 100m, 2000m),
            (new DateOnly(anio, 12, 31), 100m, 2000m),
            (Hoy, 100m, 2000m),
        ];

        await using InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId);
        await using IDbContextTransaction estropeando = await contexto.Database.BeginTransactionAsync();

        LosSaldosAUnaFecha saldos = new(contexto, new InquilinoFijo(escena.EmpresaId));

        // PRIMERO SANAS: tres meses de instantánea, julio, agosto y septiembre, y el saldo es el libro.
        (await contexto.InstantaneasMensuales.CountAsync()).ShouldBe(3);

        foreach ((DateOnly dia, decimal _, decimal _) in marcas)
        {
            (await ElDelArticuloAsync(saldos, escena, dia))
                .ShouldBe(LoQueDiceElLibro(libro, [], escena.ArticuloId, escena.AlmacenA, dia), $"el {Dia(dia)}");
        }

        (await contexto.Database.ExecuteSqlRawAsync(
            "UPDATE inventario.instantaneas_mensuales SET fisico = fisico + 10, valor = valor + 2 " +
            "WHERE empresa_id = {0} AND mes = {1}",
            escena.EmpresaId,
            agosto)).ShouldBe(1);

        (await contexto.Database.ExecuteSqlRawAsync(
            "UPDATE inventario.instantaneas_mensuales SET fisico = fisico + 100, valor = valor + 2000 " +
            "WHERE empresa_id = {0} AND mes = {1}",
            escena.EmpresaId,
            septiembre)).ShouldBe(1);

        foreach ((DateOnly dia, decimal fisico, decimal valor) in marcas)
        {
            SaldoDto debido = LoQueDiceElLibro(libro, [], escena.ArticuloId, escena.AlmacenA, dia);

            (await ElDelArticuloAsync(saldos, escena, dia)).ShouldBe(
                debido with { Cantidad = debido.Cantidad + fisico, Valor = debido.Valor + valor },
                $"el {Dia(dia)}");
        }
    }

    /// <summary>
    /// La consulta acota por almacén y por artículo, pagina por los dos, dice la fecha que contesta y
    /// rechaza la de mañana y cualquier orden.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Los pares nacen al revés de como se ordenan</b>: el segundo artículo en C, después en A, y el
    /// primero en B y en A. Una página que tomara las filas en el orden en que se escribieron dejaría
    /// fuera las que no son, y el total no lo notaría.
    /// </para>
    /// <para>
    /// <b>La tercera página está vacía y dice el total igual</b>: la sentencia devuelve entonces una
    /// fila con el total y lo demás nulo, y un total contado de las filas diría cero.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task La_consulta_acota_por_almacen_y_articulo_pagina_y_dice_la_fecha_que_contesta()
    {
        EscenaDeTransferencia escena = await MontarAsync(869, "SAF-C");
        EscenaDeTransferencia otro = await ConOtroArticuloAsync(escena, 870);

        await otro.EntrarAsync(postgres, escena.AlmacenC, escena.UbicacionC, 1m, 1m);
        await otro.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 2m, 1m);
        await escena.EntrarAsync(postgres, escena.AlmacenB, escena.UbicacionB, 3m, 1m);
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 4m, 1m);

        (Guid, Guid)[] altas =
        [
            (otro.ArticuloId, escena.AlmacenC),
            (otro.ArticuloId, escena.AlmacenA),
            (escena.ArticuloId, escena.AlmacenB),
            (escena.ArticuloId, escena.AlmacenA),
        ];

        List<(Guid, Guid)> ordenados = [.. altas.OrderBy(par => par.Item1).ThenBy(par => par.Item2)];

        ordenados.ShouldBe(Enumerable.Reverse(altas), "el orden es el contrario al de las altas");

        // CON CORTE en el mes pasado, que no tiene instantánea de ninguno: todo sale del libro de este.
        await LasExistencias.RecalcularAsync(postgres, escena.EmpresaId, LasExistencias.MesDe(Hoy).AddMonths(-1));

        // SIN FECHA, HOY, y lo dice.
        SaldosAUnaFechaDto sinFecha = await SaldosAsync(escena.Cliente, "");

        sinFecha.Fecha.ShouldBe(Hoy);
        Pares(sinFecha).ShouldBe(ordenados);
        sinFecha.Saldos.Total.ShouldBe(4);
        sinFecha.Saldos.Elementos.Select(saldo => saldo.Cantidad).ShouldBe([4m, 3m, 2m, 1m]);
        (await SaldosAsync(escena.Cliente, $"?fecha={Dia(Hoy)}")).ShouldBeEquivalentTo(sinFecha);

        // LAS PÁGINAS, que juntas son la lista, y la vacía con su total.
        SaldosAUnaFechaDto primera = await SaldosAsync(escena.Cliente, "?page=1&size=3");
        SaldosAUnaFechaDto segunda = await SaldosAsync(escena.Cliente, "?page=2&size=3");
        SaldosAUnaFechaDto tercera = await SaldosAsync(escena.Cliente, "?page=3&size=3");

        Pares(primera).ShouldBe(ordenados.Take(3));
        Pares(segunda).ShouldBe(ordenados.Skip(3));
        tercera.Saldos.Elementos.ShouldBeEmpty();

        foreach (SaldosAUnaFechaDto pagina in new[] { primera, segunda, tercera })
        {
            pagina.Saldos.Total.ShouldBe(4);
            pagina.Saldos.Tamanio.ShouldBe(3);
        }

        // LOS DOS FILTROS, cada uno solo y juntos, y un almacén que no es de nadie.
        Pares(await SaldosAsync(escena.Cliente, $"?almacen={escena.AlmacenA}"))
            .ShouldBe([(escena.ArticuloId, escena.AlmacenA), (otro.ArticuloId, escena.AlmacenA)]);
        Pares(await SaldosAsync(escena.Cliente, $"?articulo={otro.ArticuloId}"))
            .ShouldBe(ordenados.Where(par => par.Item1 == otro.ArticuloId));
        Pares(await SaldosAsync(escena.Cliente, $"?almacen={escena.AlmacenA}&articulo={otro.ArticuloId}"))
            .ShouldBe([(otro.ArticuloId, escena.AlmacenA)]);

        SaldosAUnaFechaDto deNadie = await SaldosAsync(escena.Cliente, $"?almacen={Guid.CreateVersion7()}");

        deNadie.Saldos.Total.ShouldBe(0);
        deNadie.Saldos.Elementos.ShouldBeEmpty();

        // EL PRIMER DÍA DEL CALENDARIO no desborda el mes anterior: contesta ceros.
        SaldosAUnaFechaDto alPrincipio = await SaldosAsync(escena.Cliente, $"?fecha={Dia(DateOnly.MinValue)}");

        alPrincipio.Saldos.Total.ShouldBe(4);
        alPrincipio.Saldos.Elementos.ShouldAllBe(saldo => saldo.Cantidad == 0m && saldo.Valor == 0m);

        // MAÑANA, UN 400 CON SU TYPE y el campo que lo causa; una fecha ilegible y un orden, también.
        using (HttpResponseMessage manana = await escena.Cliente.GetAsync($"{Saldos}?fecha={Dia(Hoy.AddDays(1))}"))
        {
            manana.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await Escenario.Detalle(manana));
            (await EscenaDeTransferencia.TipoDelProblemaAsync(manana)).ShouldBe("/errors/saldo-con-fecha-futura");

            using var problema = JsonDocument.Parse(await manana.Content.ReadAsStringAsync());

            problema.RootElement.GetProperty("errors").TryGetProperty("fecha", out _).ShouldBeTrue();
        }

        foreach ((string consulta, string? tipo) in new[]
        {
            ("?fecha=ayer", (string?)null),
            ("?sort=cantidad", "/errors/orden-no-admitido"),
        })
        {
            using HttpResponseMessage rechazo = await escena.Cliente.GetAsync(Saldos + consulta);

            rechazo.StatusCode.ShouldBe(HttpStatusCode.BadRequest, $"{consulta}: {await Escenario.Detalle(rechazo)}");

            if (tipo is not null)
            {
                (await EscenaDeTransferencia.TipoDelProblemaAsync(rechazo)).ShouldBe(tipo);
            }
        }
    }

    /// <summary>
    /// Cada empresa lee solo sus pares, aunque nombre el almacén o el artículo de la otra (R8).
    /// </summary>
    [Fact]
    public async Task Cada_empresa_lee_solo_sus_saldos_aunque_nombre_los_de_la_otra()
    {
        EscenaDeTransferencia una = await MontarAsync(871, "SAF-D");
        EscenaDeTransferencia otra = await MontarAsync(872, "SAF-E");

        await una.EntrarAsync(postgres, una.AlmacenA, una.UbicacionA, 2m, 1m);
        await otra.EntrarAsync(postgres, otra.AlmacenB, otra.UbicacionB, 5m, 1m);

        Pares(await SaldosAsync(una.Cliente, "")).ShouldBe([(una.ArticuloId, una.AlmacenA)]);
        Pares(await SaldosAsync(otra.Cliente, "")).ShouldBe([(otra.ArticuloId, otra.AlmacenB)]);

        foreach (string consulta in new[] { $"?almacen={otra.AlmacenB}", $"?articulo={otra.ArticuloId}" })
        {
            (await SaldosAsync(una.Cliente, consulta)).Saldos.Total.ShouldBe(0, consulta);
        }
    }

    private static SortedSet<DateOnly> LosDiasQueDeciden(
        IReadOnlyList<MovimientoStock> libro, DateOnly delBorrador, DateOnly desde)
    {
        SortedSet<DateOnly> dias = [Hoy, Hoy.AddDays(-1)];

        foreach (DateOnly dia in libro.Select(fila => fila.FechaDeOperacion).Append(delBorrador))
        {
            dias.Add(dia.AddDays(-1));
            dias.Add(dia);
        }

        for (DateOnly mes = desde; mes <= Hoy; mes = mes.AddMonths(1))
        {
            dias.Add(mes);
            dias.Add(mes.AddMonths(1).AddDays(-1));
        }

        dias.RemoveWhere(dia => dia > Hoy);

        return dias;
    }

    private async Task<List<string>> CadaDiaContraElLibroAsync(
        EscenaDeTransferencia escena,
        IReadOnlyList<MovimientoStock> libro,
        SortedSet<DateOnly> dias,
        (Guid, Guid)[] pares)
    {
        List<Transferencia> transferencias;

        await using (InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId))
        {
            transferencias = await contexto.Transferencias.AsNoTracking().ToListAsync();
        }

        // EL BARRIDO SE AFIRMA ANTES DE MIRAR: los días, los dos años, y algún día con algo volando.
        dias.Count.ShouldBeGreaterThan(40);
        transferencias.Count.ShouldBe(5, "cuatro hacia B y el inverso de la anulada");

        List<string> distintos = [];
        int conTransito = 0;

        foreach (DateOnly dia in dias)
        {
            SaldosAUnaFechaDto leido = await SaldosAsync(escena.Cliente, $"?fecha={Dia(dia)}&size=200");

            leido.Fecha.ShouldBe(dia);
            leido.Saldos.Total.ShouldBe(pares.Length, Dia(dia));
            Pares(leido).ShouldBe(pares, ignoreOrder: true, customMessage: Dia(dia));

            foreach (SaldoDto saldo in leido.Saldos.Elementos)
            {
                SaldoDto debido = LoQueDiceElLibro(libro, transferencias, saldo.ArticuloId, saldo.AlmacenId, dia);

                if (saldo != debido)
                {
                    distintos.Add($"el {Dia(dia)}, {saldo}, y el libro dice {debido}");
                }

                conTransito += saldo.CantidadEnTransito == 0m ? 0 : 1;
            }
        }

        conTransito.ShouldBeGreaterThan(0, "sin un día con algo volando, el tránsito no se ha comparado");

        return distintos;
    }

    // EL LIBRO SUMADO HASTA EL DÍA, y lo que vuela hacia el almacén: lo que movieron hasta ese día las
    // transferencias hacia él y sus inversos, que comparten destino, con el signo cambiado.
    private static SaldoDto LoQueDiceElLibro(
        IReadOnlyList<MovimientoStock> libro,
        IReadOnlyList<Transferencia> transferencias,
        Guid articuloId,
        Guid almacenId,
        DateOnly dia)
    {
        HashSet<Guid> hacia =
            [.. transferencias.Where(fila => fila.AlmacenDestinoId == almacenId).Select(fila => fila.Id)];

        MovimientoStock[] hastaElDia =
            [.. libro.Where(fila => fila.ArticuloId == articuloId && fila.FechaDeOperacion <= dia)];
        MovimientoStock[] delPar = [.. hastaElDia.Where(fila => fila.AlmacenId == almacenId)];
        MovimientoStock[] delViaje = [.. hastaElDia.Where(fila => hacia.Contains(fila.DocumentoOrigenId))];

        return new SaldoDto(
            articuloId,
            almacenId,
            delPar.Sum(fila => fila.CantidadEnUnidadBase),
            delPar.Sum(fila => fila.Valor.Cantidad),
            -delViaje.Sum(fila => fila.CantidadEnUnidadBase),
            -delViaje.Sum(fila => fila.Valor.Cantidad),
            libro.First(fila => fila.ArticuloId == articuloId).Divisa);
    }

    private static async Task<SaldoDto> ElDeAsync(EscenaDeTransferencia escena, Guid almacenId, DateOnly dia) =>
        Ver(await SaldosAsync(escena.Cliente, $"?fecha={Dia(dia)}"), escena.ArticuloId, almacenId);

    private static async Task<SaldoDto> ElDelArticuloAsync(
        LosSaldosAUnaFecha saldos, EscenaDeTransferencia escena, DateOnly dia) =>
        (await saldos.LeerAsync(
            dia, escena.AlmacenA, escena.ArticuloId, new Paginacion(), CancellationToken.None))
        .Elementos.ShouldHaveSingleItem();

    private async Task<IReadOnlyList<MovimientoStock>> ElLibroAsync(Guid empresaId)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);

        return await contexto.Movimientos.AsNoTracking().ToListAsync();
    }

    private static async Task<SaldosAUnaFechaDto> SaldosAsync(HttpClient cliente, string consulta)
    {
        using HttpResponseMessage lectura = await cliente.GetAsync(Saldos + consulta);

        return await EscenaDeTransferencia.LeerAsync<SaldosAUnaFechaDto>(lectura);
    }

    private static SaldoDto Ver(SaldosAUnaFechaDto leido, Guid articuloId, Guid almacenId) =>
        leido.Saldos.Elementos.Single(saldo => saldo.ArticuloId == articuloId && saldo.AlmacenId == almacenId);

    private static List<(Guid, Guid)> Pares(SaldosAUnaFechaDto leido) =>
        [.. leido.Saldos.Elementos.Select(saldo => (saldo.ArticuloId, saldo.AlmacenId))];

    private static string Dia(DateOnly dia) => dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static async Task<EscenaDeTransferencia> ConOtroArticuloAsync(EscenaDeTransferencia escena, int maestro)
    {
        (Guid articuloId, Guid unidadId) = await LosMaestrosPorLaApi.CrearArticuloAsync(escena.Cliente, maestro);

        return escena with { ArticuloId = articuloId, UnidadId = unidadId };
    }

    private async Task<EscenaDeTransferencia> MontarAsync(int semilla, string codigo, int? anio = null)
    {
        EscenaDeTransferencia escena =
            await EscenaDeTransferencia.MontarAsync(_api, semilla, codigo, semilla, anio: anio);

        _clientes.Add(escena.Cliente);

        return escena;
    }
}
