using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Application.Reservas;
using Bastion.Inventario.Contracts.Existencias;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Reservas;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Configuraciones;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>Una reserva tal como está guardada, leída con el filtro de la empresa puesto.</summary>
/// <param name="Estado">El estado guardado, que no es el que se lee (ADR-0059 §4).</param>
/// <param name="Causa">Por qué se liberó, si se liberó.</param>
/// <param name="LiberadaEl">Cuándo.</param>
/// <param name="Consumida">La suma de sus consumos.</param>
/// <param name="Consumos">Cuántos consumos tiene.</param>
internal sealed record ReservaGuardada(
    EstadoDeReserva Estado, CausaDeLiberacion? Causa, DateTimeOffset? LiberadaEl, decimal Consumida, int Consumos);

/// <summary>
/// Lo que repiten los casos de la reserva (ítem 2.13): pedir, consumir y liberar con el módulo
/// cableado a mano, leer el disponible, y mirar lo guardado con un contexto propio.
/// </summary>
/// <remarks>
/// <b>Lo guardado se lee con otro contexto</b>, y no con el del módulo: el del módulo recuerda lo que
/// el caso de uso cambió en memoria —una caducada liberada por un rechazo, que no se guarda—, y leer
/// ahí diría lo que pasó en memoria y no lo que llegó a la base.
/// </remarks>
internal static class LasReservas
{
    /// <summary>Un origen nuevo: la línea de un pedido de venta que todavía no existe.</summary>
    /// <param name="linea">La línea, desde uno.</param>
    /// <returns>El origen.</returns>
    internal static OrigenDeLaReserva OrigenNuevo(int linea = 1) =>
        new(TipoDeOrigenDeReserva.PedidoDeVenta, Guid.CreateVersion7(), linea);

    /// <summary>Pide una reserva del artículo de la escena, tal como salga.</summary>
    /// <param name="modulo">El módulo de la empresa de la escena.</param>
    /// <param name="escena">La escena.</param>
    /// <param name="origen">La línea que reserva.</param>
    /// <param name="cantidad">Cuánto, en unidad base.</param>
    /// <param name="almacenId">Dónde; el almacén A de la escena si no se dice.</param>
    /// <param name="caducaEl">Cuándo caduca, o nunca.</param>
    /// <returns>Lo que contestó el caso de uso.</returns>
    internal static Task<Resultado<ReservaDto>> ReservarAsync(
        ElModuloDeInventario modulo,
        EscenaDeTransferencia escena,
        OrigenDeLaReserva origen,
        decimal cantidad,
        Guid? almacenId = null,
        DateTimeOffset? caducaEl = null) =>
        modulo.Reserva.EjecutarAsync(
            new ReservarDto(origen, escena.ArticuloId, almacenId ?? escena.AlmacenA, cantidad, caducaEl),
            CancellationToken.None);

    /// <summary>Pide una reserva que tiene que salir bien.</summary>
    /// <param name="modulo">El módulo de la empresa de la escena.</param>
    /// <param name="escena">La escena.</param>
    /// <param name="origen">La línea que reserva.</param>
    /// <param name="cantidad">Cuánto, en unidad base.</param>
    /// <param name="caducaEl">Cuándo caduca, o nunca.</param>
    /// <returns>La reserva.</returns>
    internal static async Task<ReservaDto> ReservadaAsync(
        ElModuloDeInventario modulo,
        EscenaDeTransferencia escena,
        OrigenDeLaReserva origen,
        decimal cantidad,
        DateTimeOffset? caducaEl = null) =>
        Exigir(await ReservarAsync(modulo, escena, origen, cantidad, caducaEl: caducaEl));

    /// <summary>Consume una reserva con un albarán, tal como salga.</summary>
    /// <param name="modulo">El módulo de la empresa.</param>
    /// <param name="origen">La línea de la reserva.</param>
    /// <param name="albaranId">El albarán que sale.</param>
    /// <param name="fecha">El día de la salida.</param>
    /// <param name="lineas">Lo que saca.</param>
    /// <returns>Lo que contestó el caso de uso.</returns>
    internal static Task<Resultado<ReservaDto>> ConsumirAsync(
        ElModuloDeInventario modulo,
        OrigenDeLaReserva origen,
        Guid albaranId,
        DateOnly fecha,
        params LineaDeConsumoDto[] lineas) =>
        modulo.ConsumoDeReserva.EjecutarAsync(
            new ConsumirReservaDto(origen, TipoDeDocumentoOrigen.Albaran, albaranId, fecha, lineas),
            CancellationToken.None);

    /// <summary>Libera una reserva a mano, tal como salga.</summary>
    /// <param name="modulo">El módulo de la empresa.</param>
    /// <param name="origen">La línea de la reserva.</param>
    /// <param name="motivo">Por qué.</param>
    /// <returns>Lo que contestó el caso de uso.</returns>
    internal static Task<Resultado<ReservaDto>> LiberarAsync(
        ElModuloDeInventario modulo, OrigenDeLaReserva origen, string motivo = "El cliente anuló el pedido") =>
        modulo.LiberacionDeReserva.EjecutarAsync(new LiberarReservaDto(origen, motivo), CancellationToken.None);

    /// <summary>El disponible del artículo de la escena en un almacén, leído por el puerto.</summary>
    /// <param name="modulo">El módulo de la empresa.</param>
    /// <param name="escena">La escena.</param>
    /// <param name="almacenId">El almacén; el A de la escena si no se dice.</param>
    /// <returns>Las tres cifras.</returns>
    internal static async Task<DisponibleDeUnArticulo> DisponibleAsync(
        ElModuloDeInventario modulo, EscenaDeTransferencia escena, Guid? almacenId = null)
    {
        IReadOnlyDictionary<Guid, DisponibleDeUnArticulo> leido = await modulo.Disponible.DisponibleDeAsync(
            almacenId ?? escena.AlmacenA, [escena.ArticuloId], CancellationToken.None);

        leido.Keys.ShouldBe([escena.ArticuloId], "el puerto contesta por cada artículo pedido, y solo por él");

        return leido[escena.ArticuloId];
    }

    /// <summary>La reserva tal como está guardada.</summary>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="empresaId">La empresa.</param>
    /// <param name="reservaId">La reserva.</param>
    /// <returns>Lo guardado.</returns>
    internal static async Task<ReservaGuardada> LaGuardadaAsync(
        PostgresConTodosLosModulos postgres, Guid empresaId, Guid reservaId)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);

        Reserva reserva = await contexto.Reservas.AsNoTracking().SingleAsync(fila => fila.Id == reservaId);

        EstadoDeReserva estado = await contexto.Reservas
            .Where(fila => fila.Id == reservaId)
            .Select(fila => EF.Property<EstadoDeReserva>(fila, ConfiguracionDeReserva.Estado))
            .SingleAsync();

        return new ReservaGuardada(estado, reserva.Causa, reserva.LiberadaEl, reserva.Consumida, reserva.Consumos.Count);
    }

    /// <summary>Cuántas reservas tiene la empresa guardadas.</summary>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="empresaId">La empresa.</param>
    /// <returns>El número.</returns>
    internal static async Task<int> CuantasAsync(PostgresConTodosLosModulos postgres, Guid empresaId)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);

        return await contexto.Reservas.CountAsync();
    }

    /// <summary>Cuántas filas tiene el libro de la empresa.</summary>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="empresaId">La empresa.</param>
    /// <returns>El número.</returns>
    internal static async Task<int> FilasDelLibroAsync(PostgresConTodosLosModulos postgres, Guid empresaId)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);

        return await contexto.Movimientos.CountAsync();
    }

    /// <summary>Exige que el caso de uso haya salido bien, y devuelve lo que contestó.</summary>
    /// <param name="resultado">Lo que contestó.</param>
    /// <returns>La reserva.</returns>
    internal static ReservaDto Exigir(Resultado<ReservaDto> resultado)
    {
        resultado.EsCorrecto.ShouldBeTrue($"«{resultado.Error?.Codigo}»: {resultado.Error?.Mensaje}");

        return resultado.Valor;
    }

    /// <summary>Exige un rechazo con su código y su clase.</summary>
    /// <param name="resultado">Lo que contestó el caso de uso.</param>
    /// <param name="codigo">El código que tenía que contestar.</param>
    /// <param name="tipo">La clase del error, que decide el estado HTTP.</param>
    /// <param name="porque">Qué se estaba probando, para el mensaje.</param>
    internal static void ExigirElRechazo(
        Resultado<ReservaDto> resultado, string codigo, TipoDeError tipo, string porque = "")
    {
        resultado.EsCorrecto.ShouldBeFalse(porque);
        resultado.Error!.Codigo.ShouldBe(codigo, porque);
        resultado.Error!.Tipo.ShouldBe(tipo, porque);
    }
}
