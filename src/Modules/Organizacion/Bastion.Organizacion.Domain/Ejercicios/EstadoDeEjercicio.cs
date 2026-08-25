namespace Bastion.Organizacion.Domain.Ejercicios;

/// <summary>Estado de un ejercicio contable (R9).</summary>
/// <remarks>
/// NO lleva «bloqueado»: el estado del art. 32 de la LOPDGDD es para datos personales, y un
/// ejercicio es un intervalo de fechas. Mezclar las dos máquinas de estados haría que «cerrar el
/// ejercicio» y «bloquear por derecho de supresión» compartieran columna.
/// </remarks>
public enum EstadoDeEjercicio
{
    /// <summary>Admite registro de operaciones.</summary>
    Abierto,

    /// <summary>Cerrado: no se registra nada en él (R9).</summary>
    Cerrado,
}
