namespace Bastion.Inventario.Domain.Reservas;

/// <summary>Por qué una reserva soltó lo que le quedaba (ADR-0059 §1, precisión 5).</summary>
public enum CausaDeLiberacion
{
    /// <summary>Alguien la liberó, con un motivo escrito.</summary>
    AMano = 1,

    /// <summary>
    /// Llegó su caducidad. La fecha de la liberación es la de la caducidad, no la de la escritura
    /// que la encontró (ADR-0059 §4).
    /// </summary>
    Caducidad = 2,
}
