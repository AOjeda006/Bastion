using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Domain.Reservas;

namespace Bastion.Inventario.Application.Reservas;

/// <summary>Las dos preguntas que hacen los tres casos de uso antes de tocar la reserva.</summary>
internal static class LoQueSeLePideALaReserva
{
    /// <summary>
    /// Si el origen dice qué línea pide: un tipo conocido, su documento y una línea desde uno. Es lo
    /// que el alta lanzaría, contestado antes como un <c>400</c>.
    /// </summary>
    /// <param name="origen">El origen de la petición.</param>
    /// <returns><c>true</c> si vale.</returns>
    internal static bool EsUnOrigen(OrigenDeLaReserva? origen) =>
        origen is not null && Enum.IsDefined(origen.Tipo) && origen.Id != Guid.Empty && origen.Linea > 0;

    /// <summary>
    /// El <c>409</c> de una reserva que no está activa ahora: el de la caducada si caducó, y el otro
    /// si se consumió entera o se liberó a mano (ADR-0059 §6).
    /// </summary>
    /// <param name="reserva">La reserva, leída con su clave bloqueada.</param>
    /// <param name="ahora">El instante, el del <c>TimeProvider</c>.</param>
    /// <returns>El error, o <see langword="null"/> si está activa.</returns>
    internal static ErrorDeOperacion? SiNoEstaActiva(Reserva reserva, DateTimeOffset ahora)
    {
        EstadoDeReserva estado = reserva.EstadoEn(ahora);

        if (estado == EstadoDeReserva.Activa)
        {
            return null;
        }

        return reserva.HaCaducadoEn(ahora)
            ? ErroresDeReserva.Caducada(reserva.Id, reserva.CaducaEl)
            : ErroresDeReserva.NoEstaActiva(reserva.Id, estado);
    }
}
