using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Application.Reservas;
using Bastion.Inventario.Contracts.Existencias;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Domain.Transferencias;
using Microsoft.EntityFrameworkCore.Storage;
using Shouldly;
using static Bastion.Api.IntegrationTests.Inventario.LasReservas;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Las dos carreras de la reserva, con dos transacciones de verdad cada una: dos reservas de la
/// misma clave, y una reserva contra un envío que sale de esa clave (ADR-0059 §2 y §9).
/// </summary>
/// <remarks>
/// <para>
/// <b>Cada caso para a la primera transacción con su reserva escrita y sin confirmar</b>, la
/// segunda sale lanzada, <c>LaEspera</c> comprueba que está parada detrás de la primera y no en otra
/// parte, y solo entonces la primera confirma.
/// </para>
/// <para>
/// <b>La primera ha escrito, porque la carrera es sobre lo que escribe</b>: sin la reserva apartada,
/// la segunda no tendría nada que ver. <c>AGENTS.md</c> lo admite desde el epílogo del 2.11 con dos
/// condiciones, y los dos casos las cumplen. El código distingue la causa: sin cerrojo, la segunda
/// leería lo confirmado, sin la reserva de la primera, y <b>saldría bien</b>; con él, es el
/// <c>422</c> del disponible. Y la mutación que quita el cerrojo de la reserva, o que adelanta la
/// lectura de lo reservado del envío a antes del suyo, pone el caso rojo ella sola.
/// </para>
/// <para>
/// <b>Semillas: el 850 y el 851</b>, empresas y maestros con el mismo número, del bloque del 2.13
/// que cuenta la cabecera de <c>LasReservasTests</c>.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LasCarrerasDeLaReservaTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private static readonly TimeSpan s_plazo = TimeSpan.FromSeconds(30);

    private readonly ApiDeVerdad _api = new(postgres);

    /// <inheritdoc/>
    public void Dispose() => _api.Dispose();

    /// <summary>
    /// Dos reservas de 6 sobre un físico de 10, a la vez: la segunda espera en la valoración de la
    /// clave, y al entrar ve los 6 de la primera y no pasa del disponible.
    /// </summary>
    [Fact]
    public async Task Dos_reservas_a_la_vez_la_segunda_espera_y_ve_lo_que_aparto_la_primera()
    {
        EscenaDeTransferencia escena = await MontarAsync(850, "RCR-A");
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 10m, 2m);

        await using ElModuloDeInventario unos = new(postgres, escena.EmpresaId);
        await using ElModuloDeInventario otros = new(postgres, escena.EmpresaId);

        (Resultado<ReservaDto> primera, IDbContextTransaction enVuelo) =
            await unos.ReservarYQuedarseDentroAsync(Peticion(escena, 6m));

        await using (enVuelo)
        {
            Exigir(primera);

            Task<Resultado<ReservaDto>> laOtra = otros.ReservarAsync(Peticion(escena, 6m));

            await LaEspera.AQueLaFreneAsync(
                postgres.CadenaDeConexion,
                unos.ProcesoDeLaBase,
                laOtra,
                "la reserva en vuelo",
                "ha reservado sin esperar a la reserva de la misma clave que estaba apartando");

            await enVuelo.CommitAsync();

            ExigirElRechazo(
                await laOtra.WaitAsync(s_plazo),
                "reserva-por-encima-del-disponible",
                TipoDeError.ReglaDeNegocio,
                "la segunda lee lo reservado después del cerrojo, con la primera ya confirmada");
        }

        await using ElModuloDeInventario despues = new(postgres, escena.EmpresaId);

        (await DisponibleAsync(despues, escena)).ShouldBe(new DisponibleDeUnArticulo(10m, 6m, 4m));
        (await CuantasAsync(postgres, escena.EmpresaId)).ShouldBe(1);
    }

    /// <summary>
    /// Una reserva de 6 y el envío de 6 sobre un físico de 10, a la vez: el envío espera en la
    /// valoración del origen, y al entrar ve la reserva y no se lleva lo apartado.
    /// </summary>
    [Fact]
    public async Task Una_reserva_y_un_envio_a_la_vez_el_envio_espera_y_no_se_lleva_lo_apartado()
    {
        EscenaDeTransferencia escena = await MontarAsync(851, "RCR-B");
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 10m, 2m);

        Guid borrador = await escena.AbrirAsync(postgres, 6m);

        await using ElModuloDeInventario unos = new(postgres, escena.EmpresaId);
        await using ElModuloDeInventario otros = new(postgres, escena.EmpresaId);

        (Resultado<ReservaDto> reserva, IDbContextTransaction enVuelo) =
            await unos.ReservarYQuedarseDentroAsync(Peticion(escena, 6m));

        await using (enVuelo)
        {
            Exigir(reserva);

            Task<Resultado<TransferenciaDto>> elEnvio = otros.EnviarAsync(borrador);

            await LaEspera.AQueLaFreneAsync(
                postgres.CadenaDeConexion,
                unos.ProcesoDeLaBase,
                elEnvio,
                "la reserva en vuelo",
                "ha enviado sin esperar a la reserva que estaba apartando en el origen");

            await enVuelo.CommitAsync();

            Resultado<TransferenciaDto> envio = await elEnvio.WaitAsync(s_plazo);

            envio.EsCorrecto.ShouldBeFalse("el envío lee lo reservado después de su cerrojo, con la reserva ya dentro");
            envio.Error!.Codigo.ShouldBe("transferencia-por-encima-del-disponible");
        }

        (await escena.LaTransferenciaAsync(postgres, borrador)).Estado.ShouldBe(EstadoDeTransferencia.Borrador);

        await using ElModuloDeInventario despues = new(postgres, escena.EmpresaId);

        (await DisponibleAsync(despues, escena)).ShouldBe(new DisponibleDeUnArticulo(10m, 6m, 4m));
    }

    private static ReservarDto Peticion(EscenaDeTransferencia escena, decimal cantidad) =>
        new(OrigenNuevo(), escena.ArticuloId, escena.AlmacenA, cantidad, null);

    private Task<EscenaDeTransferencia> MontarAsync(int semilla, string codigo) =>
        EscenaDeTransferencia.MontarAsync(_api, semilla, codigo, semilla);
}
