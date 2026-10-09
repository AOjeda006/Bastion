namespace Bastion.Inventario.Domain.Reservas;

/// <summary>Los tres estados de una reserva (ADR-0059 §10).</summary>
/// <remarks>
/// <b>Nadie lee el estado guardado a secas</b>: se lee con <see cref="Reserva.EstadoEn"/>, porque
/// una reserva guardada <see cref="Activa"/> que ya caducó se lee <see cref="Liberada"/> (ADR-0059
/// §4). Por eso la caducidad no es un estado: es una causa de <see cref="Liberada"/>.
/// </remarks>
public enum EstadoDeReserva
{
    /// <summary>Aparta lo que le queda por servir, mientras no caduque.</summary>
    Activa = 1,

    /// <summary>Lo que apartaba ya salió entero por el libro, con uno o varios albaranes.</summary>
    Consumida = 2,

    /// <summary>
    /// Soltó lo que le quedaba, a mano o al caducar (<see cref="CausaDeLiberacion"/>). Lo que había
    /// consumido antes sigue siendo suyo.
    /// </summary>
    Liberada = 3,
}
