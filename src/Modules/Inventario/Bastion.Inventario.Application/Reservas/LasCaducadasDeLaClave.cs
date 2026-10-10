using Bastion.Inventario.Domain.Reservas;
using Bastion.Inventario.Domain.Valoraciones;

namespace Bastion.Inventario.Application.Reservas;

/// <summary>
/// La escritura de la caducidad al pasar: reservar, consumir y liberar, con la valoración de la
/// clave ya bloqueada, dejan liberadas las reservas de la clave que caducaron (ADR-0059 §4).
/// </summary>
/// <remarks>
/// <b>Se guardan con lo demás, o no se guardan</b>: esto solo las cambia en el rastreador, y las
/// escribe el <c>ConfirmarAsync</c> del caso de uso. Un camino de rechazo no lo llama, así que las
/// deja como estaban, y no importa: el estado y el disponible ya no las cuentan.
/// </remarks>
internal static class LasCaducadasDeLaClave
{
    /// <summary>Carga las caducadas de la clave y las deja liberadas, con la fecha de su caducidad.</summary>
    /// <param name="reservas">El repositorio.</param>
    /// <param name="clave">La clave, ya bloqueada.</param>
    /// <param name="ahora">El instante, el del <c>TimeProvider</c>.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>Una tarea que acaba cuando están liberadas en el rastreador.</returns>
    internal static async Task LiberarAsync(
        IRepositorioDeReservas reservas,
        ClaveDeValoracion clave,
        DateTimeOffset ahora,
        CancellationToken cancelacion)
    {
        IReadOnlyList<Reserva> caducadas = await reservas
            .CaducadasDeLaClaveAsync(clave, ahora, cancelacion)
            .ConfigureAwait(false);

        foreach (Reserva caducada in caducadas)
        {
            caducada.LiberarSiHaCaducado(ahora);
        }
    }
}
