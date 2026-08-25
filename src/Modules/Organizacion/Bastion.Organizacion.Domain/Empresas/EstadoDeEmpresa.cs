namespace Bastion.Organizacion.Domain.Empresas;

/// <summary>Estado de la ficha de una empresa (R16).</summary>
/// <remarks>
/// Dos estados, no tres: no hay «borrada». Suprimir es bloquear, y el registro sigue existiendo
/// hasta que vence el plazo de prescripción y un proceso de destrucción lo retira.
/// </remarks>
public enum EstadoDeEmpresa
{
    /// <summary>Opera con normalidad.</summary>
    Activa,

    /// <summary>
    /// Datos identificados y reservados (art. 32 LOPDGDD): no se tratan —ni se visualizan—
    /// salvo para jueces, Fiscalía y Administraciones competentes.
    /// </summary>
    Bloqueada,
}
