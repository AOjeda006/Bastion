using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Inventario;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Inventario.Application.Reservas;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Contracts.Lineas;
using Bastion.Inventario.Contracts.Recuentos;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Organizacion.Contracts.Divisas;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Shouldly;
using static Bastion.Api.IntegrationTests.Inventario.LasReservas;

namespace Bastion.Api.IntegrationTests.Retiradas;

/// <summary>
/// Lo nuevo no elige un artículo retirado, y lo viejo sigue su camino: el ajuste, la transferencia,
/// la reserva y la línea de tarifa lo rechazan al darse de alta; el recuento lo cuenta (ADR-0082).
/// </summary>
/// <remarks>
/// <para>
/// <b>Cada consumidor, con las dos mitades del ADR-0023 en el mismo caso.</b> La que rechaza lo
/// nuevo, con su <c>409</c> y su <c>type</c>; y la que deja seguir lo viejo: el borrador que ya lo
/// llevaba se confirma o se envía, la reserva que ya lo apartaba se consume, y la línea de tarifa
/// que ya lo nombraba sigue dando su precio. Sin la segunda, un consumidor que rechazara el artículo
/// en todas sus operaciones pasaría la primera, y dejaría sin salida lo que ya estaba en curso.
/// </para>
/// <para>
/// <b>Solo pregunta el alta</b>, y por eso la mitad vieja no necesita ninguna excepción escrita:
/// confirmar, enviar y consumir no preguntan por la aptitud. Lo que estos casos fijan es que siga
/// siendo así.
/// </para>
/// <para>
/// <b>Semillas: del 982 al 986</b>, en el bloque del 3.6, que va del 970 al 998. El 979, el 980 y
/// el 981 se saltan a propósito: aparecen en <c>LaDivisaDeLaTarifaSeTraduceAlMigrarTests</c> como
/// números de mutación, y una búsqueda por literal los daría por cogidos.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LoNuevoNoEligeUnArticuloRetiradoTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Tarifas = "/api/v1/catalogo/tarifas";
    private const string Divisas = "/api/v1/organizacion/divisas";

    private readonly ApiDeVerdad _api = new(postgres);
    private readonly List<HttpClient> _clientes = [];

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
    /// Un ajuste nuevo lo rechaza con la línea que lo nombra, y el borrador que ya lo llevaba se
    /// confirma.
    /// </summary>
    [Fact]
    public async Task El_ajuste_nuevo_lo_rechaza_y_el_borrador_que_ya_lo_llevaba_se_confirma()
    {
        EscenaDeTransferencia escena = await MontarAsync(982, "RTA");

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 5m, 2m);

        Guid borrador;

        await using (ElModuloDeInventario antes = new(postgres, escena.EmpresaId))
        {
            borrador = await escena.AbrirUnAjusteAsync(antes, escena.AlmacenA, Salida(escena, 2m));
        }

        await LosMaestrosPorLaApi.RetirarArticuloAsync(escena.Cliente, escena.ArticuloId);

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        Resultado<AjusteDto> nuevo = await modulo.Alta.EjecutarAsync(
            new AbrirAjusteDto(
                escena.SerieDeAjustes.Id,
                escena.AlmacenA,
                EscenaDeTransferencia.Hoy,
                "Regularización de un recuento",
                [Salida(escena, 1m)]),
            CancellationToken.None);

        nuevo.EsCorrecto.ShouldBeFalse("un ajuste nuevo ha elegido un artículo retirado");
        nuevo.Error!.Codigo.ShouldBe("ajuste-articulo-retirado");
        nuevo.Error!.Tipo.ShouldBe(TipoDeError.Conflicto);
        nuevo.Error!.Actual.ShouldBe(new LineaQueFallaDto(1), "la línea que lo nombra, desde uno (ADR-0065 §3)");

        using HttpResponseMessage confirmacion = await escena.ConfirmarElAjustePorLaApiAsync(borrador);

        confirmacion.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            "el borrador de antes de retirarlo tiene que poder confirmarse: solo pregunta el alta. " +
            await Escenario.Detalle(confirmacion));
    }

    /// <summary>
    /// Una transferencia nueva lo rechaza con la línea que lo nombra, y la que ya lo llevaba se
    /// envía.
    /// </summary>
    [Fact]
    public async Task La_transferencia_nueva_lo_rechaza_y_la_que_ya_lo_llevaba_se_envia()
    {
        EscenaDeTransferencia escena = await MontarAsync(983, "RTT");

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 5m, 2m);

        Guid borrador = await escena.AbrirAsync(postgres, 2m);

        await LosMaestrosPorLaApi.RetirarArticuloAsync(escena.Cliente, escena.ArticuloId);

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        Resultado<TransferenciaDto> nueva = await escena.IntentarAbrirAsync(
            modulo,
            escena.AlmacenA,
            escena.AlmacenB,
            [escena.Linea(escena.UbicacionA, escena.UbicacionB, 1m)]);

        nueva.EsCorrecto.ShouldBeFalse("una transferencia nueva ha elegido un artículo retirado");
        nueva.Error!.Codigo.ShouldBe("transferencia-articulo-retirado");
        nueva.Error!.Tipo.ShouldBe(TipoDeError.Conflicto);
        nueva.Error!.Actual.ShouldBe(new LineaQueFallaDto(1), "la línea que lo nombra, desde uno (ADR-0068 §6)");

        using HttpResponseMessage envio = await escena.EnviarPorLaApiAsync(borrador);

        (await EscenaDeTransferencia.LeerAsync<TransferenciaDto>(envio)).Id.ShouldBe(
            borrador, "la transferencia de antes de retirarlo tiene que poder enviarse: solo pregunta el alta");
    }

    /// <summary>
    /// Una reserva nueva lo rechaza, y la que ya lo apartaba se consume con su albarán.
    /// </summary>
    [Fact]
    public async Task La_reserva_nueva_lo_rechaza_y_la_que_ya_lo_apartaba_se_consume()
    {
        EscenaDeTransferencia escena = await MontarAsync(984, "RTR");

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 5m, 2m);

        OrigenDeLaReserva origen = OrigenNuevo();

        await using (ElModuloDeInventario antes = new(postgres, escena.EmpresaId))
        {
            _ = await ReservadaAsync(antes, escena, origen, 2m);
        }

        await LosMaestrosPorLaApi.RetirarArticuloAsync(escena.Cliente, escena.ArticuloId);

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        ExigirElRechazo(
            await ReservarAsync(modulo, escena, OrigenNuevo(), 1m),
            "reserva-articulo-retirado",
            TipoDeError.Conflicto,
            "una reserva nueva ha elegido un artículo retirado");

        ReservaDto consumida = Exigir(await ConsumirAsync(
            modulo, origen, Guid.CreateVersion7(), EscenaDeTransferencia.Hoy, new LineaDeConsumoDto(escena.UbicacionA, 2m)));

        consumida.Consumida.ShouldBe(2m, "la reserva de antes de retirarlo tiene que poder consumirse");
    }

    /// <summary>
    /// El recuento lo cuenta: su foto lo trae, se le añade una clave a mano, y el ajuste que genera
    /// saca lo que le quedaba.
    /// </summary>
    /// <remarks>
    /// <b>Es la salida de las existencias de un retirado</b>, y por eso el único consumidor que lo
    /// admite. Con el artículo retirado ANTES de abrir el recuento, para que la foto también pase por
    /// el puerto con el artículo ya retirado.
    /// </remarks>
    [Fact]
    public async Task El_recuento_lo_cuenta_y_su_ajuste_saca_lo_que_le_quedaba()
    {
        EscenaDeRecuento escena = await EscenaDeRecuento.MontarAsync(_api, 985, "RTC");
        EscenaDeTransferencia de = escena.Escena;

        _clientes.Add(escena.Cliente);

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 5m);

        UbicacionDto otroHueco = await LosMaestrosPorLaApi.CrearUbicacionAsync(
            escena.Cliente, de.AlmacenA, "RTC-A2");

        await LosMaestrosPorLaApi.RetirarArticuloAsync(escena.Cliente, de.ArticuloId);

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenA);

        LineaDeRecuentoDto deLaFoto = (await escena.TodasLasLineasAsync(abierto.Id)).ShouldHaveSingleItem(
            "la foto trae el artículo retirado con sus existencias");
        deLaFoto.ArticuloId.ShouldBe(de.ArticuloId);

        using (HttpResponseMessage anadida = await EscenaDeRecuento.AnadirAsync(
            escena.Cliente, abierto.Id, new AnadirLineaDeRecuentoDto(otroHueco.Id, de.ArticuloId, null, null, null)))
        {
            anadida.StatusCode.ShouldBe(
                HttpStatusCode.Created,
                "el recuento admite una clave de un artículo retirado: es la salida de lo que le " +
                "quede. " + await Escenario.Detalle(anadida));
        }

        foreach (LineaDeRecuentoDto linea in await escena.TodasLasLineasAsync(abierto.Id))
        {
            await escena.ContarConSuVersionAsync(abierto.Id, linea.Id, 0m);
        }

        RecuentoDto confirmado = await escena.ConfirmarConLaFichaDeAhoraAsync(abierto.Id);

        confirmado.AjusteId.ShouldNotBeNull("con cinco unidades contadas como cero, hay ajuste");

        await using ElModuloDeInventario modulo = new(postgres, de.EmpresaId);

        (await DisponibleAsync(modulo, de)).Fisico.ShouldBe(
            0m, "el ajuste del recuento saca las existencias del retirado, que ya no tenían otra salida");
    }

    /// <summary>
    /// La tarifa no le añade líneas, la que ya tenía sigue dando su precio, y reincorporado vuelve a
    /// admitirlas.
    /// </summary>
    [Fact]
    public async Task La_tarifa_no_le_anade_lineas_y_la_que_tenia_sigue_dando_su_precio()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(986));
        _clientes.Add(cliente);

        (Guid articuloId, _) = await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, 986);
        TarifaDto tarifa = await TarifaAsync(cliente, "TR986");

        (await LineaAsync(cliente, tarifa.Id, articuloId, 0m)).ShouldBe(
            HttpStatusCode.Created, "antes de retirarlo admite su línea, que es lo que da sentido al rechazo");

        await LosMaestrosPorLaApi.RetirarArticuloAsync(cliente, articuloId);

        using (HttpResponseMessage rechazo = await cliente.PostAsJsonAsync(
            $"{Tarifas}/{tarifa.Id}/lineas", Linea(articuloId, 10m)))
        {
            rechazo.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(rechazo));
            (await EscenaDeTransferencia.TipoDelProblemaAsync(rechazo)).ShouldBe("/errors/tarifa-articulo-retirado");
        }

        using (HttpResponseMessage precio = await cliente.GetAsync(
            $"{Tarifas}/TR986/precio?articulo={articuloId}&cantidad=1"))
        {
            (await EscenaDeTransferencia.LeerAsync<PrecioResueltoDto>(precio)).Precio.ShouldBe(
                7.5m, "la línea que ya lo nombraba sigue dando su precio: lo viejo se sigue resolviendo");
        }

        await LosMaestrosPorLaApi.ReincorporarArticuloAsync(cliente, articuloId);

        (await LineaAsync(cliente, tarifa.Id, articuloId, 10m)).ShouldBe(
            HttpStatusCode.Created, "reincorporado, vuelve a admitir líneas nuevas");
    }

    private static LineaDeAjusteDto Salida(EscenaDeTransferencia escena, decimal cantidad) =>
        new(escena.UbicacionA, escena.ArticuloId, -cantidad, escena.UnidadId, null);

    private static CrearLineaTarifaDto Linea(Guid articuloId, decimal desde) => new()
    {
        ArticuloId = articuloId,
        CantidadDesde = desde,
        Precio = 7.5m,
    };

    private static async Task<HttpStatusCode> LineaAsync(
        HttpClient cliente, Guid tarifaId, Guid articuloId, decimal desde)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            $"{Tarifas}/{tarifaId}/lineas", Linea(articuloId, desde));

        return alta.StatusCode;
    }

    /// <summary>Una tarifa de venta en euros, vigente desde el año 2000 y sin final.</summary>
    /// <remarks>
    /// La divisa es de instalación: si otro caso ya la dio de alta, el <c>409</c> es lo esperable.
    /// </remarks>
    private static async Task<TarifaDto> TarifaAsync(HttpClient cliente, string codigo)
    {
        using (HttpResponseMessage divisa = await cliente.PostAsJsonAsync(
            Divisas, new CrearDivisaDto { Codigo = "EUR", Nombre = "Euro" }))
        {
            divisa.StatusCode.ShouldBeOneOf(HttpStatusCode.Created, HttpStatusCode.Conflict);
        }

        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Tarifas,
            new CrearTarifaDto
            {
                Codigo = codigo,
                Nombre = "Tarifa " + codigo,
                Tipo = "Venta",
                Divisa = "EUR",
                VigenteDesde = new DateOnly(2000, 1, 1),
                VigenteHasta = null,
            });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<TarifaDto>())!;
    }

    private async Task<EscenaDeTransferencia> MontarAsync(int semilla, string codigo)
    {
        EscenaDeTransferencia escena = await EscenaDeTransferencia.MontarAsync(
            _api, semilla, codigo, semilla);

        _clientes.Add(escena.Cliente);

        return escena;
    }
}
