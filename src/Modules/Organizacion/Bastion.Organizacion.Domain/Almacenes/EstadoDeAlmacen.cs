namespace Bastion.Organizacion.Domain.Almacenes;

/// <summary>Estado de un almacén.</summary>
/// <remarks>
/// Lleva <c>Bloqueado</c>, pero por un motivo distinto al de la empresa: cada movimiento de
/// existencias apunta a su almacén para siempre, así que borrarlo rompería el histórico de
/// valoración, que es irreparable. La forma es la misma a propósito.
/// </remarks>
public enum EstadoDeAlmacen
{
    /// <summary>Admite movimientos.</summary>
    Activo,

    /// <summary>No admite movimientos nuevos; su histórico se conserva.</summary>
    Bloqueado,
}
