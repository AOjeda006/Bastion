namespace Bastion.BuildingBlocks.Infrastructure.Auditoria;

/// <summary>Qué le pasó a la fila.</summary>
/// <remarks>
/// Tres y no cuatro: no hay «lectura». Auditar accesos a datos es otra cosa, con otro volumen y
/// otra tabla, y no es de este ítem. Lo que sí deja traza aquí es el acceso al sistema, porque
/// entrar cambia una fila (<c>Usuario.UltimoAccesoEn</c>) y ese cambio es una modificación normal.
/// </remarks>
public enum TipoDeCambio
{
    /// <summary>La fila no existía.</summary>
    Alta = 1,

    /// <summary>La fila existía y alguna propiedad auditada cambió de valor.</summary>
    Modificacion,

    /// <summary>
    /// La fila se ha borrado de verdad. Debería ser raro: R16 dice que suprimir es bloquear, y un
    /// bloqueo es una modificación. Un borrado que aparezca aquí es una pregunta, no un trámite.
    /// </summary>
    Baja,
}
