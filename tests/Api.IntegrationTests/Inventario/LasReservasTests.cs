using System.Net;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Fechas;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Application.Reservas;
using Bastion.Inventario.Contracts.Existencias;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Domain.Reservas;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Organizacion.Contracts.Empresas;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using static Bastion.Api.IntegrationTests.Inventario.LasReservas;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Reservar, liberar y leer el disponible (ítem 2.13, ADR-0059): lo que se aparta sale del
/// disponible, no pasa de él, se suelta a mano o al caducar, y cada empresa lee el suyo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por el caso de uso cableado a mano</b>, porque la reserva no tiene borde: la llama Ventas en
/// proceso desde la fase 4 (ADR-0059 §12). Los rechazos son resultados, y cada caso mira el código y
/// la clase, que es lo que decidirá el estado HTTP el día que haya quien lo traduzca.
/// </para>
/// <para>
/// <b>Cada guarda lleva su otra mitad</b>: lo que la guarda para, ya en regla, pasa. Una guarda que
/// contestara siempre que no daría el mismo rojo que la buena.
/// </para>
/// <para>
/// <b>Semillas: del 830 al 839</b>, empresas y maestros con el mismo número; el 838 es solo una
/// empresa, la que pregunta por lo ajeno. <b>El bloque del 830 al 869 es del 2.13</b>: del 830 al 899
/// no había ninguna, ni por literal ni por cálculo —las del generador de la propiedad acaban en el
/// 595, y las de la importación llevan delante 33 000 000—. Del resto del bloque, del 840 al 846 son de
/// <c>ElConsumoDeLaReservaTests</c>, del 847 al 849 de <c>LaTransferenciaFrenteAlDisponibleTests</c>,
/// el 850 y el 851 de <c>LasCarrerasDeLaReservaTests</c>, y del 852 al 869 quedan libres.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LasReservasTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private readonly ApiDeVerdad _api = new(postgres);

    /// <inheritdoc/>
    public void Dispose() => _api.Dispose();

    /// <summary>
    /// Lo reservado sale del disponible, y una reserva no pasa de él: justo lo que queda entra, y una
    /// millonésima más ya no.
    /// </summary>
    [Fact]
    public async Task Reservar_aparta_del_disponible_y_no_pasa_de_el()
    {
        EscenaDeTransferencia escena = await MontarAsync(830, "RSV-A");
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 10m, 2m);

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        (await DisponibleAsync(modulo, escena)).ShouldBe(new DisponibleDeUnArticulo(10m, 0m, 10m));

        OrigenDeLaReserva primera = OrigenNuevo();
        ReservaDto reservada = await ReservadaAsync(modulo, escena, primera, 4m);

        reservada.Origen.ShouldBe(primera);
        reservada.Cantidad.ShouldBe(4m);
        reservada.Consumida.ShouldBe(0m);
        reservada.Pendiente.ShouldBe(4m);
        reservada.Estado.ShouldBe(EstadoDeReserva.Activa);
        reservada.Causa.ShouldBeNull();
        reservada.UnidadBaseId.ShouldBe(escena.UnidadId);

        (await DisponibleAsync(modulo, escena)).ShouldBe(new DisponibleDeUnArticulo(10m, 4m, 6m));

        ExigirElRechazo(
            await ReservarAsync(modulo, escena, OrigenNuevo(), 6.000001m),
            "reserva-por-encima-del-disponible",
            TipoDeError.ReglaDeNegocio,
            "una millonésima por encima de lo que queda");

        _ = await ReservadaAsync(modulo, escena, OrigenNuevo(), 6m);

        (await DisponibleAsync(modulo, escena)).ShouldBe(new DisponibleDeUnArticulo(10m, 10m, 0m));

        ExigirElRechazo(
            await ReservarAsync(modulo, escena, OrigenNuevo(), 0.000001m),
            "reserva-por-encima-del-disponible",
            TipoDeError.ReglaDeNegocio,
            "con todo reservado, no queda ni una millonésima");

        (await CuantasAsync(postgres, escena.EmpresaId)).ShouldBe(2, "los rechazos no dejan reserva");
    }

    /// <summary>
    /// El mismo origen con la misma petición devuelve su reserva, esté como esté; con otra cosa, es
    /// un conflicto, y va antes que el disponible y que los maestros.
    /// </summary>
    /// <remarks>
    /// <b>La otra línea del mismo pedido es otro origen</b>, y reserva: la línea es parte de la clave
    /// del origen. Y el conflicto con otro almacén sale aunque ese almacén no tenga nada: el origen se
    /// mira antes que el disponible (ADR-0059 §5).
    /// </remarks>
    [Fact]
    public async Task El_mismo_origen_devuelve_su_reserva_y_otra_peticion_es_un_conflicto()
    {
        EscenaDeTransferencia escena = await MontarAsync(831, "RSV-B");
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 10m, 2m);

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        OrigenDeLaReserva origen = OrigenNuevo();
        ReservaDto primera = await ReservadaAsync(modulo, escena, origen, 3m);
        ReservaDto otraVez = await ReservadaAsync(modulo, escena, origen, 3m);

        otraVez.Id.ShouldBe(primera.Id);
        (await DisponibleAsync(modulo, escena)).Reservado.ShouldBe(3m, "repetir la petición no aparta dos veces");

        // Una a una: el módulo tiene un solo contexto, y no admite dos operaciones a la vez.
        foreach ((string porque, Func<Task<Resultado<ReservaDto>>> peticion) in new (string, Func<Task<Resultado<ReservaDto>>>)[]
        {
            ("otra cantidad", () => ReservarAsync(modulo, escena, origen, 4m)),
            ("otro almacén, vacío", () => ReservarAsync(modulo, escena, origen, 3m, escena.AlmacenB)),
            ("con caducidad", () => ReservarAsync(modulo, escena, origen, 3m, caducaEl: DateTimeOffset.UtcNow.AddDays(1))),
        })
        {
            ExigirElRechazo(await peticion(), "reserva-origen-con-otra-reserva", TipoDeError.Conflicto, porque);
        }

        ReservaDto otraLinea = await ReservadaAsync(modulo, escena, origen with { Linea = 2 }, 3m);

        otraLinea.Id.ShouldNotBe(primera.Id);

        Exigir(await LiberarAsync(modulo, origen));

        ReservaDto liberada = await ReservadaAsync(modulo, escena, origen, 3m);

        liberada.Id.ShouldBe(primera.Id, "la misma petición devuelve su reserva, esté como esté");
        liberada.Estado.ShouldBe(EstadoDeReserva.Liberada);
        (await CuantasAsync(postgres, escena.EmpresaId)).ShouldBe(2);
    }

    /// <summary>
    /// Sin físico no hay nada que reservar, y el cerrojo de la reserva no crea la valoración que no
    /// encuentra.
    /// </summary>
    [Fact]
    public async Task Sin_fisico_no_hay_nada_que_reservar_y_el_cerrojo_no_crea_la_fila()
    {
        EscenaDeTransferencia escena = await MontarAsync(832, "RSV-C");

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        (await DisponibleAsync(modulo, escena)).ShouldBe(new DisponibleDeUnArticulo(0m, 0m, 0m));

        ExigirElRechazo(
            await ReservarAsync(modulo, escena, OrigenNuevo(), 1m),
            "reserva-por-encima-del-disponible",
            TipoDeError.ReglaDeNegocio,
            "sin una sola entrada");

        await using (InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId))
        {
            (await contexto.Valoraciones.CountAsync()).ShouldBe(0, "reservar no crea la valoración");
        }

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 1m, 2m);

        _ = await ReservadaAsync(modulo, escena, OrigenNuevo(), 1m);
    }

    /// <summary>
    /// Los maestros de la reserva: el almacén existe y admite lo nuevo, y el artículo existe y se
    /// almacena. Cada uno con su código, y la reserva buena al final.
    /// </summary>
    [Fact]
    public async Task Los_maestros_de_la_reserva_contestan_cada_uno_con_su_codigo()
    {
        EscenaDeTransferencia escena = await MontarAsync(833, "RSV-D");

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        using (HttpResponseMessage servicio = await escena.CambiarElArticuloAsync("Servicio", "Ninguna"))
        {
            servicio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(servicio));
        }

        ExigirElRechazo(
            await ReservarAsync(modulo, escena, OrigenNuevo(), 1m),
            "reserva-articulo-no-se-almacena",
            TipoDeError.Conflicto,
            "un servicio");

        using (HttpResponseMessage almacenable = await escena.CambiarElArticuloAsync("Bien", "Ninguna"))
        {
            almacenable.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(almacenable));
        }

        ExigirElRechazo(
            await modulo.Reserva.EjecutarAsync(
                new ReservarDto(OrigenNuevo(), Guid.CreateVersion7(), escena.AlmacenA, 1m), CancellationToken.None),
            "reserva-articulo-no-encontrado",
            TipoDeError.Validacion,
            "un artículo que no existe");

        ExigirElRechazo(
            await ReservarAsync(modulo, escena, OrigenNuevo(), 1m, Guid.CreateVersion7()),
            "reserva-almacen-no-encontrado",
            TipoDeError.Validacion,
            "un almacén que no existe");

        using (HttpResponseMessage bloqueo = await escena.Cliente.SuprimirAsync(
            $"{LosMaestrosPorLaApi.Almacenes}/{escena.AlmacenC}"))
        {
            bloqueo.IsSuccessStatusCode.ShouldBeTrue(await Escenario.Detalle(bloqueo));
        }

        ExigirElRechazo(
            await ReservarAsync(modulo, escena, OrigenNuevo(), 1m, escena.AlmacenC),
            "reserva-almacen-bloqueado",
            TipoDeError.Conflicto,
            "un almacén bloqueado");

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 1m, 2m);

        _ = await ReservadaAsync(modulo, escena, OrigenNuevo(), 1m);
        (await CuantasAsync(postgres, escena.EmpresaId)).ShouldBe(1);
    }

    /// <summary>
    /// Lo que la reserva lanzaría se contesta como un <c>400</c>, antes de mirar nada: el origen, la
    /// cantidad y la caducidad, con la frontera de cada una.
    /// </summary>
    /// <remarks>
    /// <b>Con el reloj parado</b>, para que «ahora» sea un instante que el caso conoce: la caducidad
    /// igual a ahora no vale, y un minuto después sí.
    /// </remarks>
    [Fact]
    public async Task La_peticion_sin_forma_es_un_400_con_la_frontera_de_cada_campo()
    {
        EscenaDeTransferencia escena = await MontarAsync(834, "RSV-E");
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 10m, 2m);

        var ahora = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId, new RelojParado(ahora));

        OrigenDeLaReserva bueno = OrigenNuevo();

        foreach ((string porque, OrigenDeLaReserva? origen) in new (string, OrigenDeLaReserva?)[]
        {
            ("sin origen", null),
            ("un tipo que no existe", bueno with { Tipo = 0 }),
            ("sin identificador", bueno with { Id = Guid.Empty }),
            ("la línea cero", bueno with { Linea = 0 }),
            ("una línea negativa", bueno with { Linea = -1 }),
        })
        {
            ExigirElRechazo(
                await ReservarAsync(modulo, escena, origen!, 1m), "reserva-origen-no-valido", TipoDeError.Validacion, porque);
        }

        foreach (decimal cantidad in new[] { 0m, -1m, 1.0000001m })
        {
            ExigirElRechazo(
                await ReservarAsync(modulo, escena, bueno, cantidad),
                "reserva-cantidad-no-valida",
                TipoDeError.Validacion,
                $"la cantidad {cantidad}");
        }

        foreach (DateTimeOffset caducaEl in new[] { ahora, ahora.AddTicks(9), ahora.AddSeconds(-1) })
        {
            ExigirElRechazo(
                await ReservarAsync(modulo, escena, bueno, 1m, caducaEl: caducaEl),
                "reserva-caducidad-no-valida",
                TipoDeError.Validacion,
                $"caduca {(caducaEl - ahora).Ticks} ticks después de ahora");
        }

        (await CuantasAsync(postgres, escena.EmpresaId)).ShouldBe(0);

        ReservaDto reservada = await ReservadaAsync(modulo, escena, bueno, 1.000001m, ahora.AddMinutes(1));

        reservada.CaducaEl.ShouldBe(ahora.AddMinutes(1));
    }

    /// <summary>
    /// Liberar a mano suelta lo que queda, con su motivo y su fecha, y no se libera dos veces; el
    /// motivo tiene que estar y caber.
    /// </summary>
    [Fact]
    public async Task Liberar_suelta_lo_que_queda_con_su_motivo()
    {
        EscenaDeTransferencia escena = await MontarAsync(835, "RSV-F");
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 10m, 2m);

        var ahora = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId, new RelojParado(ahora));

        OrigenDeLaReserva origen = OrigenNuevo();
        ReservaDto reservada = await ReservadaAsync(modulo, escena, origen, 4m);

        foreach (string motivo in new[] { string.Empty, "   ", new string('x', Reserva.LargoDelMotivo + 1) })
        {
            ExigirElRechazo(
                await LiberarAsync(modulo, origen, motivo),
                "reserva-motivo-no-valido",
                TipoDeError.Validacion,
                $"con un motivo de {motivo.Length} caracteres");
        }

        (await LaGuardadaAsync(postgres, escena.EmpresaId, reservada.Id)).Estado.ShouldBe(EstadoDeReserva.Activa);

        ExigirElRechazo(
            await LiberarAsync(modulo, OrigenNuevo()),
            "reserva-no-encontrada",
            TipoDeError.NoEncontrado,
            "un origen sin reserva");

        string motivoJusto = new('x', Reserva.LargoDelMotivo);
        ReservaDto liberada = Exigir(await LiberarAsync(modulo, origen, motivoJusto));

        liberada.Estado.ShouldBe(EstadoDeReserva.Liberada);
        liberada.Causa.ShouldBe(CausaDeLiberacion.AMano);
        liberada.Motivo.ShouldBe(motivoJusto);
        liberada.LiberadaEl.ShouldBe(ahora);
        liberada.Pendiente.ShouldBe(4m, "liberar no consume: lo pendiente deja de apartar, pero no sale");

        (await DisponibleAsync(modulo, escena)).ShouldBe(new DisponibleDeUnArticulo(10m, 0m, 10m));
        (await LaGuardadaAsync(postgres, escena.EmpresaId, reservada.Id))
            .ShouldBe(new ReservaGuardada(EstadoDeReserva.Liberada, CausaDeLiberacion.AMano, ahora, 0m, 0));

        ExigirElRechazo(
            await LiberarAsync(modulo, origen),
            "reserva-no-esta-activa",
            TipoDeError.Conflicto,
            "liberada dos veces");
    }

    /// <summary>
    /// Una reserva que caduca deja de apartar en el instante de su caducidad, sin que nadie la
    /// escriba; la escribe, liberada por caducidad y con esa fecha, la siguiente escritura de su clave
    /// que sale bien, y un rechazo no la guarda.
    /// </summary>
    /// <remarks>
    /// <b>La frontera es la del dominio</b>: en el instante exacto de la caducidad ya no aparta, y un
    /// microsegundo antes, sí. El SQL del disponible y <c>HaCaducadoEn</c> tienen que decir lo mismo,
    /// porque uno decide lo que se lee y el otro lo que se escribe (ADR-0059 §4).
    /// </remarks>
    [Fact]
    public async Task La_caducidad_suelta_sola_y_la_escribe_la_siguiente_escritura_que_sale()
    {
        EscenaDeTransferencia escena = await MontarAsync(836, "RSV-G");
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 10m, 2m);

        var ahora = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        DateTimeOffset caducaEl = ahora.AddHours(1);

        OrigenDeLaReserva origen = OrigenNuevo();
        ReservaDto reservada;

        await using (ElModuloDeInventario antes = new(postgres, escena.EmpresaId, new RelojParado(ahora)))
        {
            reservada = await ReservadaAsync(antes, escena, origen, 4m, caducaEl);
        }

        await using (ElModuloDeInventario justoAntes = new(
            postgres, escena.EmpresaId, new RelojParado(caducaEl.AddTicks(-TimeSpan.TicksPerMicrosecond))))
        {
            (await DisponibleAsync(justoAntes, escena)).ShouldBe(new DisponibleDeUnArticulo(10m, 4m, 6m));
        }

        await using ElModuloDeInventario despues = new(postgres, escena.EmpresaId, new RelojParado(caducaEl));

        (await DisponibleAsync(despues, escena)).ShouldBe(new DisponibleDeUnArticulo(10m, 0m, 10m));

        ExigirElRechazo(
            await LiberarAsync(despues, origen),
            "reserva-caducada",
            TipoDeError.Conflicto,
            "liberar a mano una que ya caducó");

        ExigirElRechazo(
            await ConsumirAsync(
                despues, origen, Guid.CreateVersion7(), EscenaDeTransferencia.Hoy, new LineaDeConsumoDto(escena.UbicacionA, 1m)),
            "reserva-caducada",
            TipoDeError.Conflicto,
            "consumir una que ya caducó");

        (await LaGuardadaAsync(postgres, escena.EmpresaId, reservada.Id)).Estado
            .ShouldBe(EstadoDeReserva.Activa, "un rechazo no guarda la caducada");

        _ = await ReservadaAsync(despues, escena, OrigenNuevo(), 10m);

        (await LaGuardadaAsync(postgres, escena.EmpresaId, reservada.Id))
            .ShouldBe(new ReservaGuardada(EstadoDeReserva.Liberada, CausaDeLiberacion.Caducidad, caducaEl, 0m, 0));

        ReservaDto otraVez = await ReservadaAsync(despues, escena, origen, 4m, caducaEl);

        otraVez.Estado.ShouldBe(EstadoDeReserva.Liberada);
        otraVez.Causa.ShouldBe(CausaDeLiberacion.Caducidad);
        otraVez.LiberadaEl.ShouldBe(caducaEl);
    }

    /// <summary>
    /// El disponible es de la empresa que pregunta: con los identificadores de otra, contesta ceros. Y
    /// contesta por cada artículo pedido una vez, exista o no, y nada si no se pide nada.
    /// </summary>
    /// <remarks>
    /// <b>La sentencia es SQL crudo</b>, que no pasa por el filtro global: la empresa la compara ella,
    /// en la valoración y en las reservas (R8). Sin esas dos comparaciones, la empresa ajena leería las
    /// cifras de esta.
    /// </remarks>
    [Fact]
    public async Task El_disponible_es_de_la_empresa_que_pregunta_y_de_cada_articulo_pedido()
    {
        EscenaDeTransferencia escena = await MontarAsync(837, "RSV-H");
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 10m, 2m);

        await using (ElModuloDeInventario modulo = new(postgres, escena.EmpresaId))
        {
            _ = await ReservadaAsync(modulo, escena, OrigenNuevo(), 4m);

            var otro = Guid.CreateVersion7();
            IReadOnlyDictionary<Guid, DisponibleDeUnArticulo> leido = await modulo.Disponible.DisponibleDeAsync(
                escena.AlmacenA, [escena.ArticuloId, otro, escena.ArticuloId], CancellationToken.None);

            leido.Count.ShouldBe(2);
            leido[escena.ArticuloId].ShouldBe(new DisponibleDeUnArticulo(10m, 4m, 6m));
            leido[otro].ShouldBe(new DisponibleDeUnArticulo(0m, 0m, 0m));

            (await modulo.Disponible.DisponibleDeAsync(escena.AlmacenA, [], CancellationToken.None)).ShouldBeEmpty();
        }

        (HttpClient cliente, EmpresaDto ajena) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(838));

        using (cliente)
        {
            await using ElModuloDeInventario deLaAjena = new(postgres, ajena.Id);

            (await DisponibleAsync(deLaAjena, escena)).ShouldBe(new DisponibleDeUnArticulo(0m, 0m, 0m));
        }
    }

    /// <summary>
    /// El disponible no cuenta lo que vuela: lo que sale de A deja de estar en A al enviarse, y no
    /// está en B hasta que se recibe. Lo que viene en camino no se puede reservar.
    /// </summary>
    [Fact]
    public async Task El_disponible_no_cuenta_lo_que_vuela()
    {
        EscenaDeTransferencia escena = await MontarAsync(839, "RSV-I");
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 10m, 2m);

        TransferenciaDto enviada = await escena.EnviarAsync(postgres, 4m);

        await using (ElModuloDeInventario modulo = new(postgres, escena.EmpresaId))
        {
            (await DisponibleAsync(modulo, escena)).ShouldBe(new DisponibleDeUnArticulo(6m, 0m, 6m));
            (await DisponibleAsync(modulo, escena, escena.AlmacenB)).ShouldBe(new DisponibleDeUnArticulo(0m, 0m, 0m));

            ExigirElRechazo(
                await ReservarAsync(modulo, escena, OrigenNuevo(), 1m, escena.AlmacenB),
                "reserva-por-encima-del-disponible",
                TipoDeError.ReglaDeNegocio,
                "lo que viene en camino");
        }

        using (HttpResponseMessage recibida = await escena.RecibirPorLaApiAsync(enviada.Id, EscenaDeTransferencia.Hoy))
        {
            recibida.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(recibida));
        }

        await using ElModuloDeInventario despues = new(postgres, escena.EmpresaId);

        (await DisponibleAsync(despues, escena, escena.AlmacenB)).ShouldBe(new DisponibleDeUnArticulo(4m, 0m, 4m));

        _ = Exigir(await ReservarAsync(despues, escena, OrigenNuevo(), 4m, escena.AlmacenB));
    }

    private Task<EscenaDeTransferencia> MontarAsync(int semilla, string codigo) =>
        EscenaDeTransferencia.MontarAsync(_api, semilla, codigo, semilla);
}
