using System.Net;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Fechas;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.Inventario.Application.Reservas;
using Bastion.Inventario.Contracts.Existencias;
using Bastion.Inventario.Domain.Reservas;
using Bastion.Inventario.Domain.Transferencias;
using Shouldly;
using static Bastion.Api.IntegrationTests.Inventario.LasReservas;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// La transferencia respeta el disponible de su origen (ítem 2.13, ADR-0059 §9): el envío no se
/// lleva lo que otra línea tiene apartado.
/// </summary>
/// <remarks>
/// <para>
/// <b>El envío va por la API</b>, porque el <c>422</c> es del borde y porque el filtro de
/// idempotencia deshace lo que no es un 2xx: el número que el envío rechazado tomó vuelve a la serie.
/// Las reservas van por el caso de uso cableado a mano, que no tiene borde (ADR-0059 §12).
/// </para>
/// <para>
/// <b>Semillas: del 847 al 849</b>, empresas y maestros con el mismo número, del bloque del 2.13 que
/// cuenta la cabecera de <c>LasReservasTests</c>.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaTransferenciaFrenteAlDisponibleTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string PorEncimaDelDisponible = "/errors/transferencia-por-encima-del-disponible";

    private readonly ApiDeVerdad _api = new(postgres);

    /// <inheritdoc/>
    public void Dispose() => _api.Dispose();

    /// <summary>
    /// Con 10 de físico y 4 reservados, enviar 7 es un <c>422</c> que no escribe nada ni gasta
    /// número, y enviar justo los 6 que quedan sale.
    /// </summary>
    [Fact]
    public async Task El_envio_no_se_lleva_lo_reservado_y_lo_disponible_sale()
    {
        EscenaDeTransferencia escena = await MontarAsync(847, "RTD-A");
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 10m, 2m);

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);
        _ = await ReservadaAsync(modulo, escena, OrigenNuevo(), 4m);

        Guid demasiado = await escena.AbrirAsync(postgres, 7m);

        using (HttpResponseMessage rechazo = await escena.EnviarPorLaApiAsync(demasiado))
        {
            rechazo.StatusCode.ShouldBe(HttpStatusCode.UnprocessableContent, await Escenario.Detalle(rechazo));
            (await EscenaDeTransferencia.TipoDelProblemaAsync(rechazo)).ShouldBe(PorEncimaDelDisponible);
        }

        (await escena.LaTransferenciaAsync(postgres, demasiado)).Estado.ShouldBe(EstadoDeTransferencia.Borrador);
        (await escena.FilasDelLibroAsync(postgres, demasiado)).ShouldBeEmpty("el envío rechazado no deja filas");
        (await escena.ContadorAsync()).ShouldBe(0, "ni deja un número gastado");
        (await DisponibleAsync(modulo, escena)).ShouldBe(new DisponibleDeUnArticulo(10m, 4m, 6m));

        Guid justo = await escena.AbrirAsync(postgres, 6m);

        using (HttpResponseMessage envio = await escena.EnviarPorLaApiAsync(justo))
        {
            envio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(envio));
        }

        (await DisponibleAsync(modulo, escena)).ShouldBe(new DisponibleDeUnArticulo(4m, 4m, 0m));
    }

    /// <summary>
    /// Entre el disponible y el físico contesta el disponible, también con el físico entero; por
    /// encima del físico contesta el hueco, con el <c>stock-insuficiente</c> de siempre.
    /// </summary>
    [Fact]
    public async Task Hasta_el_fisico_contesta_el_disponible_y_por_encima_el_hueco()
    {
        EscenaDeTransferencia escena = await MontarAsync(848, "RTD-B");
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 10m, 2m);

        await using (ElModuloDeInventario modulo = new(postgres, escena.EmpresaId))
        {
            _ = await ReservadaAsync(modulo, escena, OrigenNuevo(), 4m);
        }

        foreach ((decimal cantidad, string tipo) in new[]
        {
            (10m, PorEncimaDelDisponible),
            (11m, "/errors/stock-insuficiente"),
        })
        {
            Guid borrador = await escena.AbrirAsync(postgres, cantidad);

            using HttpResponseMessage rechazo = await escena.EnviarPorLaApiAsync(borrador);

            rechazo.StatusCode.ShouldBe(HttpStatusCode.UnprocessableContent, await Escenario.Detalle(rechazo));
            (await EscenaDeTransferencia.TipoDelProblemaAsync(rechazo)).ShouldBe(tipo, $"enviando {cantidad}");
        }

        (await escena.ContadorAsync()).ShouldBe(0);
    }

    /// <summary>
    /// Una reserva caducada ya no aparta (ADR-0059 §4): el envío de 7 sale, y la reserva sigue
    /// guardada como estaba, porque el envío no es una escritura de reservas.
    /// </summary>
    /// <remarks>
    /// <b>La reserva se pide con el reloj de ayer</b>, para que caduque hace casi un día sin esperar:
    /// el envío mira con el reloj de la API, el de verdad.
    /// </remarks>
    [Fact]
    public async Task Una_reserva_caducada_no_frena_el_envio()
    {
        EscenaDeTransferencia escena = await MontarAsync(849, "RTD-C");
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 10m, 2m);

        DateTimeOffset ayer = DateTimeOffset.UtcNow.AddDays(-1);
        ReservaDto reservada;

        await using (ElModuloDeInventario antes = new(postgres, escena.EmpresaId, new RelojParado(ayer)))
        {
            reservada = await ReservadaAsync(antes, escena, OrigenNuevo(), 4m, ayer.AddHours(1));
        }

        Guid borrador = await escena.AbrirAsync(postgres, 7m);

        using (HttpResponseMessage envio = await escena.EnviarPorLaApiAsync(borrador))
        {
            envio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(envio));
        }

        (await LaGuardadaAsync(postgres, escena.EmpresaId, reservada.Id)).Estado.ShouldBe(EstadoDeReserva.Activa);
    }

    private Task<EscenaDeTransferencia> MontarAsync(int semilla, string codigo) =>
        EscenaDeTransferencia.MontarAsync(_api, semilla, codigo, semilla);
}
