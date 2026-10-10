using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Domain.Reservas;
using Bastion.Inventario.Domain.Valoraciones;

namespace Bastion.Inventario.Application.Reservas;

/// <summary>Suelta a mano lo que le queda a una reserva, con un motivo escrito.</summary>
public interface ILiberarReserva
{
    /// <summary>Ejecuta la liberación.</summary>
    /// <param name="peticion">De qué reserva, y por qué.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>La reserva liberada, o el motivo por el que no se libera.</returns>
    Task<Resultado<ReservaDto>> EjecutarAsync(LiberarReservaDto peticion, CancellationToken cancelacion);
}

/// <summary>
/// Liberar: la valoración de la clave con <c>FOR NO KEY UPDATE</c>, las caducadas, la reserva y su
/// liberación (ADR-0059 §2).
/// </summary>
/// <remarks>
/// <b>Toma el cerrojo aunque solo baje lo reservado</b>: es una escritura sobre las reservas de la
/// clave, y la regla del ADR-0059 §2 dice que toda escritura así lo tiene antes. Sin él, una
/// transferencia que leyera lo reservado con la valoración bloqueada podría verlo cambiar.
/// </remarks>
/// <param name="reservas">El cerrojo y la reserva.</param>
/// <param name="unidadTrabajo">La transacción.</param>
/// <param name="reloj">De dónde sale «ahora».</param>
internal sealed class LiberarReserva(
    IRepositorioDeReservas reservas,
    IUnidadTrabajoDeInventario unidadTrabajo,
    TimeProvider reloj) : ILiberarReserva
{
    /// <inheritdoc/>
    public Task<Resultado<ReservaDto>> EjecutarAsync(LiberarReservaDto peticion, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        return unidadTrabajo.EnTransaccionAsync(enCurso => LiberarAsync(peticion, enCurso), cancelacion);
    }

    private async Task<Resultado<ReservaDto>> LiberarAsync(LiberarReservaDto peticion, CancellationToken cancelacion)
    {
        DateTimeOffset ahora = reloj.GetUtcNow();

        if (!LoQueSeLePideALaReserva.EsUnOrigen(peticion.Origen))
        {
            return Resultado.Fallo<ReservaDto>(ErroresDeReserva.OrigenNoValido());
        }

        if (string.IsNullOrWhiteSpace(peticion.Motivo) || peticion.Motivo.Trim().Length > Reserva.LargoDelMotivo)
        {
            return Resultado.Fallo<ReservaDto>(ErroresDeReserva.MotivoNoValido());
        }

        OrigenDeLaReserva origen = peticion.Origen;

        ClaveDeValoracion? deLaReserva = await reservas
            .ClaveDelOrigenAsync(origen.Tipo, origen.Id, origen.Linea, cancelacion)
            .ConfigureAwait(false);

        if (deLaReserva is not { } clave)
        {
            return Resultado.Fallo<ReservaDto>(ErroresDeReserva.NoEncontrada(origen));
        }

        // LIBERAR SIEMPRE ENCUENTRA LA FILA: una reserva solo nace donde había físico, y una
        // valoración no se borra (ADR-0059 §2).
        _ = await reservas.BloquearLaValoracionAsync(clave, cancelacion).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"La clave de la reserva del origen {origen} no tiene valoración: la reserva nació con " +
                "físico en ella, así que la fila tenía que estar.");

        Reserva reserva = await reservas
            .ObtenerPorOrigenAsync(origen.Tipo, origen.Id, origen.Linea, cancelacion)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"La reserva del origen {origen} estaba antes del cerrojo y no está después: una " +
                "reserva no se borra, así que la lectura no ha visto lo que debía.");

        if (LoQueSeLePideALaReserva.SiNoEstaActiva(reserva, ahora) is { } noEstaActiva)
        {
            return Resultado.Fallo<ReservaDto>(noEstaActiva);
        }

        // LAS CADUCADAS DE LA CLAVE, ya sin ningún rechazo por delante (ADR-0059 §4).
        await LasCaducadasDeLaClave.LiberarAsync(reservas, clave, ahora, cancelacion).ConfigureAwait(false);

        reserva.Liberar(peticion.Motivo, ahora);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(reserva.ADto(ahora));
    }
}
