namespace Bastion.BuildingBlocks.Infrastructure.Auditoria;

/// <summary>
/// Qué se hace con una entidad, o con una propiedad, cuando cambia.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es una lista de permitidos, no de prohibidos.</b> Lo que no está clasificado no se audita, y
/// además pone en rojo el barrido: una lista de prohibidos se olvida de la propiedad que alguien
/// añada el año que viene, y ese olvido es silencioso y permanente. Con esta forma, la propiedad
/// nueva no llega a la traza <b>y</b> alguien se entera en la misma ejecución de la CI.
/// </para>
/// <para>
/// <see cref="SinClasificar"/> no es un estado válido en reposo: existe para poder nombrarlo en el
/// mensaje del barrido que lo encuentra.
/// </para>
/// </remarks>
public enum ClasificacionDeAuditoria
{
    /// <summary>Nadie ha dicho qué hacer con esto. El barrido lo pone en rojo.</summary>
    SinClasificar = 0,

    /// <summary>Su valor viejo y su valor nuevo van a la traza.</summary>
    Auditada,

    /// <summary>Queda fuera de la traza a propósito, con su motivo escrito.</summary>
    NoAuditada,

    /// <summary>
    /// Queda fuera de la traza <b>y</b> no puede acabar en ella por ningún camino: es un secreto.
    /// </summary>
    Secreta,
}
