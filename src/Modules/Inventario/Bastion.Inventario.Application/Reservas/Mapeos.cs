using Bastion.Inventario.Domain.Reservas;

namespace Bastion.Inventario.Application.Reservas;

/// <summary>Del dominio a lo que se enseña.</summary>
internal static class Mapeos
{
    /// <summary>La reserva como se lee en un instante (ADR-0059 §4).</summary>
    /// <remarks>
    /// <b>Una caducada que nadie ha escrito se enseña como si ya lo estuviera</b>: con la causa
    /// <c>Caducidad</c> y la fecha de su caducidad, que es lo que dejará la escritura que la
    /// encuentre. Así la respuesta no depende de si alguien pasó antes por su clave.
    /// </remarks>
    /// <param name="reserva">La reserva.</param>
    /// <param name="ahora">El instante, el del <c>TimeProvider</c>.</param>
    /// <returns>Lo que se enseña.</returns>
    internal static ReservaDto ADto(this Reserva reserva, DateTimeOffset ahora)
    {
        bool caducadaSinEscribir = reserva.HaCaducadoEn(ahora) && reserva.Causa is null;

        return new ReservaDto(
            reserva.Id,
            new OrigenDeLaReserva(reserva.OrigenTipo, reserva.OrigenId, reserva.OrigenLinea),
            reserva.ArticuloId,
            reserva.AlmacenId,
            reserva.Cantidad,
            reserva.Consumida,
            reserva.Pendiente,
            reserva.UnidadBaseId,
            reserva.CaducaEl,
            reserva.EstadoEn(ahora),
            caducadaSinEscribir ? CausaDeLiberacion.Caducidad : reserva.Causa,
            reserva.Motivo,
            caducadaSinEscribir ? reserva.CaducaEl : reserva.LiberadaEl);
    }
}
