using System.Net;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Fechas;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Application.Reservas;
using Bastion.Inventario.Contracts.Existencias;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Reservas;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Organizacion.Contracts.Empresas;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using static Bastion.Api.IntegrationTests.Inventario.LasReservas;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Consumir una reserva (ítem 2.13, ADR-0059 §6): la salida del albarán va por el libro, y la
/// reserva anota lo que sale en el mismo <c>COMMIT</c>; lo que no se puede sacar se contesta antes
/// de escribir nada.
/// </summary>
/// <remarks>
/// <para>
/// <b>Las guardas del libro son las de cualquier salida</b>: la fecha, el ejercicio, la marca, el
/// físico de cada hueco y la valoración. Las de la reserva van delante: que este albarán no la haya
/// consumido ya, que esté activa y que no se saque más de lo que le queda.
/// </para>
/// <para>
/// <b>Semillas: del 840 al 846</b>, del bloque del 2.13 que reparte la cabecera de
/// <c>LasReservasTests</c>.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElConsumoDeLaReservaTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string RutaDeEmpresas = "/api/v1/organizacion/empresas";

    private readonly ApiDeVerdad _api = new(postgres);

    /// <inheritdoc/>
    public void Dispose() => _api.Dispose();

    /// <summary>
    /// Consumir saca por el libro, al precio medio, y anota el consumo en la reserva: lo que sale
    /// deja de estar reservado y de estar en el almacén a la vez. Consumida entera, ya no se consume
    /// ni se libera; liberada a medias, lo consumido se queda.
    /// </summary>
    [Fact]
    public async Task Consumir_saca_por_el_libro_y_lo_anota_en_la_reserva()
    {
        EscenaDeTransferencia escena = await MontarAsync(840, "RCS-A");
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 10m, 2m);

        var ahora = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId, new RelojParado(ahora));

        OrigenDeLaReserva origen = OrigenNuevo();
        ReservaDto reservada = await ReservadaAsync(modulo, escena, origen, 5m);

        var primerAlbaran = Guid.CreateVersion7();
        ReservaDto aMedias = Exigir(await ConsumirAsync(
            modulo, origen, primerAlbaran, EscenaDeTransferencia.Hoy, new LineaDeConsumoDto(escena.UbicacionA, 2m)));

        aMedias.Consumida.ShouldBe(2m);
        aMedias.Pendiente.ShouldBe(3m);
        aMedias.Estado.ShouldBe(EstadoDeReserva.Activa);

        MovimientoStock salida = (await escena.FilasDelLibroAsync(postgres, primerAlbaran)).ShouldHaveSingleItem();

        salida.DocumentoOrigenTipo.ShouldBe(TipoDeDocumentoOrigen.Albaran);
        salida.AlmacenId.ShouldBe(escena.AlmacenA);
        salida.UbicacionId.ShouldBe(escena.UbicacionA);
        salida.CantidadEnUnidadBase.ShouldBe(-2m);
        salida.Valor.ShouldBe(Importe.De(-4m, "EUR"), "2 al precio medio, que es 2,00");

        (await escena.LaValoracionDeAsync(postgres, escena.AlmacenA)).Cantidad.ShouldBe(8m);
        (await DisponibleAsync(modulo, escena)).ShouldBe(
            new DisponibleDeUnArticulo(8m, 3m, 5m), "lo que sale deja de estar reservado y en el almacén a la vez");

        ReservaDto entera = Exigir(await ConsumirAsync(
            modulo, origen, Guid.CreateVersion7(), EscenaDeTransferencia.Hoy, new LineaDeConsumoDto(escena.UbicacionA, 3m)));

        entera.Estado.ShouldBe(EstadoDeReserva.Consumida);
        entera.Pendiente.ShouldBe(0m);
        (await DisponibleAsync(modulo, escena)).ShouldBe(new DisponibleDeUnArticulo(5m, 0m, 5m));

        ExigirElRechazo(
            await ConsumirAsync(
                modulo, origen, Guid.CreateVersion7(), EscenaDeTransferencia.Hoy, new LineaDeConsumoDto(escena.UbicacionA, 1m)),
            "reserva-no-esta-activa",
            TipoDeError.Conflicto,
            "consumida entera, no se consume más");

        ExigirElRechazo(
            await LiberarAsync(modulo, origen), "reserva-no-esta-activa", TipoDeError.Conflicto, "ni se libera");

        (await LaGuardadaAsync(postgres, escena.EmpresaId, reservada.Id))
            .ShouldBe(new ReservaGuardada(EstadoDeReserva.Consumida, null, null, 5m, 2));

        OrigenDeLaReserva otro = OrigenNuevo();
        ReservaDto otra = await ReservadaAsync(modulo, escena, otro, 4m);

        _ = Exigir(await ConsumirAsync(
            modulo, otro, Guid.CreateVersion7(), EscenaDeTransferencia.Hoy, new LineaDeConsumoDto(escena.UbicacionA, 1m)));

        ReservaDto liberada = Exigir(await LiberarAsync(modulo, otro));

        liberada.Estado.ShouldBe(EstadoDeReserva.Liberada);
        liberada.Consumida.ShouldBe(1m, "liberar suelta lo pendiente, y lo que salió ya salió");
        (await DisponibleAsync(modulo, escena)).ShouldBe(new DisponibleDeUnArticulo(4m, 0m, 4m));
        (await LaGuardadaAsync(postgres, escena.EmpresaId, otra.Id))
            .ShouldBe(new ReservaGuardada(EstadoDeReserva.Liberada, CausaDeLiberacion.AMano, ahora, 1m, 1));
    }

    /// <summary>
    /// Las guardas de la reserva y las del físico contestan antes de escribir: el mismo albarán dos
    /// veces, más de lo pendiente, y más de lo que hay en un hueco, sumando las líneas que lo
    /// comparten. Justo lo pendiente, repartido en dos huecos que lo tienen, sale; y si ese albarán
    /// vuelve, oye que ya la consumió, aunque la haya dejado consumida.
    /// </summary>
    [Fact]
    public async Task Las_guardas_del_consumo_contestan_antes_de_escribir()
    {
        EscenaDeTransferencia escena = await MontarAsync(841, "RCS-B");
        Guid otroHueco = (await LosMaestrosPorLaApi.CrearUbicacionAsync(escena.Cliente, escena.AlmacenA, "RCS-B-A2")).Id;

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 5m, 2m);
        await escena.EntrarAsync(postgres, escena.AlmacenA, otroHueco, 1m, 2m);

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        OrigenDeLaReserva origen = OrigenNuevo();
        ReservaDto reservada = await ReservadaAsync(modulo, escena, origen, 6m);

        var primerAlbaran = Guid.CreateVersion7();
        _ = Exigir(await ConsumirAsync(
            modulo, origen, primerAlbaran, EscenaDeTransferencia.Hoy, new LineaDeConsumoDto(escena.UbicacionA, 1m)));

        int filasAntes = await FilasDelLibroAsync(postgres, escena.EmpresaId);

        ExigirElRechazo(
            await ConsumirAsync(
                modulo, origen, primerAlbaran, EscenaDeTransferencia.Hoy, new LineaDeConsumoDto(escena.UbicacionA, 1m)),
            "reserva-documento-ya-la-consumio",
            TipoDeError.Conflicto,
            "el mismo albarán, otra vez");

        var segundo = Guid.CreateVersion7();

        ExigirElRechazo(
            await ConsumirAsync(
                modulo,
                origen,
                segundo,
                EscenaDeTransferencia.Hoy,
                new LineaDeConsumoDto(escena.UbicacionA, 4m),
                new LineaDeConsumoDto(otroHueco, 1.000001m)),
            "reserva-consumo-por-encima-de-lo-pendiente",
            TipoDeError.ReglaDeNegocio,
            "una millonésima más de lo que le queda");

        foreach ((string porque, LineaDeConsumoDto[] lineas) in new (string, LineaDeConsumoDto[])[]
        {
            ("más de lo que hay en el segundo hueco", [new(otroHueco, 2m)]),
            ("dos líneas que juntas pasan del hueco", [new(escena.UbicacionA, 3m), new(escena.UbicacionA, 1.000001m)]),
            ("un hueco de otro almacén", [new(escena.UbicacionB, 1m)]),
            ("un hueco que no existe", [new(Guid.Empty, 1m)]),
        })
        {
            ExigirElRechazo(
                await ConsumirAsync(modulo, origen, segundo, EscenaDeTransferencia.Hoy, lineas),
                "reserva-consumo-sin-stock",
                TipoDeError.ReglaDeNegocio,
                porque);
        }

        (await FilasDelLibroAsync(postgres, escena.EmpresaId)).ShouldBe(filasAntes, "ningún rechazo escribe en el libro");

        ReservaDto consumida = Exigir(await ConsumirAsync(
            modulo,
            origen,
            segundo,
            EscenaDeTransferencia.Hoy,
            new LineaDeConsumoDto(escena.UbicacionA, 4m),
            new LineaDeConsumoDto(otroHueco, 1m)));

        consumida.Estado.ShouldBe(EstadoDeReserva.Consumida);
        (await FilasDelLibroAsync(postgres, escena.EmpresaId)).ShouldBe(filasAntes + 2);
        (await LaGuardadaAsync(postgres, escena.EmpresaId, reservada.Id)).Consumos.ShouldBe(2);

        ExigirElRechazo(
            await ConsumirAsync(
                modulo, origen, segundo, EscenaDeTransferencia.Hoy, new LineaDeConsumoDto(escena.UbicacionA, 1m)),
            "reserva-documento-ya-la-consumio",
            TipoDeError.Conflicto,
            "el reintento del albarán que la dejó consumida oye que ya salió, y no que no está activa");
    }

    /// <summary>
    /// La petición sin forma es un <c>400</c>, antes de buscar la reserva: el origen, el documento,
    /// las líneas y sus cantidades. Y un origen sin reserva es un <c>404</c>.
    /// </summary>
    [Fact]
    public async Task La_peticion_sin_forma_es_un_400_y_un_origen_sin_reserva_un_404()
    {
        EscenaDeTransferencia escena = await MontarAsync(846, "RCS-G");

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        OrigenDeLaReserva origen = OrigenNuevo();
        LineaDeConsumoDto una = new(escena.UbicacionA, 1m);
        ConsumirReservaDto buena = new(
            origen, TipoDeDocumentoOrigen.Albaran, Guid.CreateVersion7(), EscenaDeTransferencia.Hoy, [una]);

        foreach ((string porque, ConsumirReservaDto peticion, string codigo) in new (string, ConsumirReservaDto, string)[]
        {
            ("sin origen", buena with { Origen = null! }, "reserva-origen-no-valido"),
            ("la línea cero", buena with { Origen = origen with { Linea = 0 } }, "reserva-origen-no-valido"),
            ("un ajuste", buena with { DocumentoTipo = TipoDeDocumentoOrigen.Ajuste }, "reserva-documento-no-valido"),
            ("una transferencia", buena with { DocumentoTipo = TipoDeDocumentoOrigen.Transferencia }, "reserva-documento-no-valido"),
            ("sin albarán", buena with { DocumentoId = Guid.Empty }, "reserva-documento-no-valido"),
            ("sin líneas", buena with { Lineas = [] }, "reserva-sin-lineas"),
            ("una cantidad cero", buena with { Lineas = [una with { Cantidad = 0m }] }, "reserva-cantidad-no-valida"),
            ("una cantidad negativa", buena with { Lineas = [una, una with { Cantidad = -1m }] }, "reserva-cantidad-no-valida"),
            ("siete decimales", buena with { Lineas = [una with { Cantidad = 1.0000001m }] }, "reserva-cantidad-no-valida"),
        })
        {
            ExigirElRechazo(
                await modulo.ConsumoDeReserva.EjecutarAsync(peticion, CancellationToken.None),
                codigo,
                TipoDeError.Validacion,
                porque);
        }

        ExigirElRechazo(
            await modulo.ConsumoDeReserva.EjecutarAsync(buena, CancellationToken.None),
            "reserva-no-encontrada",
            TipoDeError.NoEncontrado,
            "la petición buena, sin reserva detrás");
    }

    /// <summary>
    /// La fecha del albarán pasa las guardas de cualquier salida: no es futura, cae en un ejercicio
    /// abierto y no va por detrás del último movimiento de la clave. El mismo día que el último, sí.
    /// </summary>
    [Fact]
    public async Task La_fecha_del_albaran_pasa_las_guardas_de_cualquier_salida()
    {
        int anio = EscenaDeTransferencia.Hoy.Year - 1;
        EscenaDeTransferencia escena = await MontarAsync(842, "RCS-C", anio: anio);
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 10m, 2m, new DateOnly(anio, 6, 15));

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        OrigenDeLaReserva origen = OrigenNuevo();
        _ = await ReservadaAsync(modulo, escena, origen, 5m);

        LineaDeConsumoDto una = new(escena.UbicacionA, 1m);
        var albaran = Guid.CreateVersion7();

        foreach ((DateOnly fecha, string codigo, TipoDeError tipo) in new (DateOnly, string, TipoDeError)[]
        {
            (EscenaDeTransferencia.Hoy.AddDays(1), "reserva-con-fecha-futura", TipoDeError.ReglaDeNegocio),
            (new DateOnly(anio - 1, 6, 15), "reserva-sin-ejercicio", TipoDeError.Conflicto),
            (new DateOnly(anio, 6, 14), "reserva-fecha-anterior-al-ultimo-movimiento", TipoDeError.ReglaDeNegocio),
        })
        {
            ExigirElRechazo(await ConsumirAsync(modulo, origen, albaran, fecha, una), codigo, tipo, $"el {fecha:O}");
        }

        _ = Exigir(await ConsumirAsync(modulo, origen, albaran, new DateOnly(anio, 6, 15), una));

        using (HttpResponseMessage cierre = await escena.CerrarElEjercicioAsync())
        {
            cierre.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(cierre));
        }

        ExigirElRechazo(
            await ConsumirAsync(modulo, origen, Guid.CreateVersion7(), new DateOnly(anio, 6, 16), una),
            "reserva-en-ejercicio-cerrado",
            TipoDeError.Conflicto,
            "con el ejercicio cerrado");
    }

    /// <summary>
    /// Un artículo por lote saca con su lote: sin él, o con un número de serie que no pide, no casa;
    /// con uno mal escrito es un <c>400</c>; con uno que no existe no hay de dónde sacar, y no se crea.
    /// El lote se lee normalizado, como al entrar.
    /// </summary>
    [Fact]
    public async Task Por_lote_saca_con_su_lote_y_no_crea_el_que_no_existe()
    {
        EscenaDeTransferencia escena = await MontarAsync(843, "RCS-D", "PorLote");
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 3m, 2m, lote: "L1");
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 2m, 2m, lote: "L2");

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        OrigenDeLaReserva origen = OrigenNuevo();
        _ = await ReservadaAsync(modulo, escena, origen, 5m);

        var albaran = Guid.CreateVersion7();

        foreach ((string porque, LineaDeConsumoDto linea, string codigo, TipoDeError tipo) in
            new (string, LineaDeConsumoDto, string, TipoDeError)[]
        {
            ("sin lote", new(escena.UbicacionA, 1m), "reserva-trazabilidad-no-casa", TipoDeError.Conflicto),
            ("con un número de serie", new(escena.UbicacionA, 1m, "L1", "S1"), "reserva-trazabilidad-no-casa", TipoDeError.Conflicto),
            ("un lote con un espacio dentro", new(escena.UbicacionA, 1m, "L 1"), "reserva-lote-no-valido", TipoDeError.Validacion),
            ("un lote que no existe", new(escena.UbicacionA, 1m, "L9"), "reserva-consumo-sin-stock", TipoDeError.ReglaDeNegocio),
            ("más de lo que tiene el lote", new(escena.UbicacionA, 2.000001m, "L2"), "reserva-consumo-sin-stock", TipoDeError.ReglaDeNegocio),
        })
        {
            ExigirElRechazo(await ConsumirAsync(modulo, origen, albaran, EscenaDeTransferencia.Hoy, linea), codigo, tipo, porque);
        }

        await using (InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId))
        {
            (await contexto.Lotes.CountAsync()).ShouldBe(2, "buscar el lote no lo crea");
        }

        _ = Exigir(await ConsumirAsync(
            modulo,
            origen,
            albaran,
            EscenaDeTransferencia.Hoy,
            new LineaDeConsumoDto(escena.UbicacionA, 3m, " L1 "),
            new LineaDeConsumoDto(escena.UbicacionA, 2m, "L2")));

        IReadOnlyList<MovimientoStock> filas = await escena.FilasDelLibroAsync(postgres, albaran);

        await using InventarioDbContext despues = postgres.AbrirInventario(escena.EmpresaId);
        Dictionary<Guid, string> lotes = await despues.Lotes.ToDictionaryAsync(lote => lote.Id, lote => lote.Codigo);

        filas.Select(fila => (lotes[fila.LoteId!.Value], fila.CantidadEnUnidadBase))
            .ShouldBe([("L1", -3m), ("L2", -2m)]);
    }

    /// <summary>
    /// Con la empresa en otra divisa que la de la valoración, la salida no se valora: un <c>422</c>,
    /// sin tocar el libro ni la reserva.
    /// </summary>
    [Fact]
    public async Task Una_valoracion_en_otra_divisa_para_el_consumo()
    {
        EscenaDeTransferencia escena = await MontarAsync(844, "RCS-E");
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 3m, 2m);

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        OrigenDeLaReserva origen = OrigenNuevo();
        ReservaDto reservada = await ReservadaAsync(modulo, escena, origen, 2m);

        using (HttpResponseMessage cambio = await escena.Cliente.ModificarAsync(
            $"{RutaDeEmpresas}/{escena.EmpresaId}",
            new ModificarEmpresaDto
            {
                RazonSocial = "Empresa que pasa a llevar las cuentas en dólares",
                DomicilioFiscal = Escenario.Domicilio(),
                DivisaBase = "USD",
                RegimenDeIva = "General",
            }))
        {
            cambio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(cambio));
        }

        int filasAntes = await FilasDelLibroAsync(postgres, escena.EmpresaId);

        ExigirElRechazo(
            await ConsumirAsync(
                modulo, origen, Guid.CreateVersion7(), EscenaDeTransferencia.Hoy, new LineaDeConsumoDto(escena.UbicacionA, 1m)),
            "reserva-valoracion-en-otra-divisa",
            TipoDeError.ReglaDeNegocio,
            "la valoración en euros y la empresa en dólares");

        (await FilasDelLibroAsync(postgres, escena.EmpresaId)).ShouldBe(filasAntes);
        (await LaGuardadaAsync(postgres, escena.EmpresaId, reservada.Id))
            .ShouldBe(new ReservaGuardada(EstadoDeReserva.Activa, null, null, 0m, 0));

        Valoracion enEuros = await escena.LaValoracionDeAsync(postgres, escena.AlmacenA);

        enEuros.Cantidad.ShouldBe(3m);
        enEuros.Divisa.ShouldBe("EUR");
    }

    /// <summary>
    /// Un artículo por número de serie saca pieza a pieza: una unidad por línea, cada número una vez
    /// por albarán, y solo los que están en el hueco. El que ya salió no vuelve a salir.
    /// </summary>
    [Fact]
    public async Task Por_numero_de_serie_saca_pieza_a_pieza()
    {
        EscenaDeTransferencia escena = await MontarAsync(845, "RCS-F", "PorNumeroSerie");

        foreach (string serie in new[] { "S1", "S2", "S3" })
        {
            await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 1m, 2m, serie: serie);
        }

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        OrigenDeLaReserva origen = OrigenNuevo();
        _ = await ReservadaAsync(modulo, escena, origen, 2m);

        var albaran = Guid.CreateVersion7();

        foreach ((string porque, LineaDeConsumoDto[] lineas, string codigo, TipoDeError tipo) in
            new (string, LineaDeConsumoDto[], string, TipoDeError)[]
        {
            ("dos unidades con un número", [new(escena.UbicacionA, 2m, null, "S1")], "reserva-serie-no-unitaria", TipoDeError.Validacion),
            (
                "el mismo número dos veces",
                [new(escena.UbicacionA, 1m, null, "S1"), new(escena.UbicacionA, 1m, null, "S1")],
                "reserva-serie-repetida",
                TipoDeError.Validacion),
            ("un número con un espacio dentro", [new(escena.UbicacionA, 1m, null, "S 1")], "reserva-numero-de-serie-no-valido", TipoDeError.Validacion),
            ("un número que no existe", [new(escena.UbicacionA, 1m, null, "S9")], "reserva-consumo-sin-stock", TipoDeError.ReglaDeNegocio),
        })
        {
            ExigirElRechazo(
                await ConsumirAsync(modulo, origen, albaran, EscenaDeTransferencia.Hoy, lineas), codigo, tipo, porque);
        }

        await using (InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId))
        {
            (await contexto.NumerosDeSerie.CountAsync()).ShouldBe(3, "buscar el número no lo crea");
        }

        ReservaDto consumida = Exigir(await ConsumirAsync(
            modulo,
            origen,
            albaran,
            EscenaDeTransferencia.Hoy,
            new LineaDeConsumoDto(escena.UbicacionA, 1m, null, "S1"),
            new LineaDeConsumoDto(escena.UbicacionA, 1m, null, "S2")));

        consumida.Estado.ShouldBe(EstadoDeReserva.Consumida);

        OrigenDeLaReserva otro = OrigenNuevo();
        _ = await ReservadaAsync(modulo, escena, otro, 1m);

        ExigirElRechazo(
            await ConsumirAsync(
                modulo, otro, Guid.CreateVersion7(), EscenaDeTransferencia.Hoy, new LineaDeConsumoDto(escena.UbicacionA, 1m, null, "S1")),
            "reserva-consumo-sin-stock",
            TipoDeError.ReglaDeNegocio,
            "una pieza que ya salió");
    }

    private Task<EscenaDeTransferencia> MontarAsync(
        int semilla, string codigo, string trazabilidad = "Ninguna", int? anio = null) =>
        EscenaDeTransferencia.MontarAsync(_api, semilla, codigo, semilla, trazabilidad, anio);
}
