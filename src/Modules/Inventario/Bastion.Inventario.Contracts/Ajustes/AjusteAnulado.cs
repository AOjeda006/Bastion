using Bastion.BuildingBlocks.Domain.Eventos;

namespace Bastion.Inventario.Contracts.Ajustes;

/// <summary>Un ajuste confirmado quedó anulado por un inverso (R2).</summary>
/// <remarks>
/// <b>Anulado no es borrado</b>, y el evento se llama así a propósito: las filas que el ajuste
/// escribió en el libro <b>siguen ahí</b>, porque el libro es de solo añadido. Lo que compensa el
/// efecto es otro documento con el signo contrario, y quien lo crea es el ítem 2.5; hasta
/// entonces este evento cuenta el cambio de estado del original y nada más.
/// </remarks>
/// <param name="AjusteId">El documento que quedó anulado.</param>
/// <param name="EmpresaId">Empresa a la que pertenece (R8).</param>
public sealed record AjusteAnulado(Guid AjusteId, Guid EmpresaId) : EventoDeIntegracion
{
    /// <summary>Nombre con el que viaja por la bandeja de salida.</summary>
    public const string Nombre = "inventario.ajuste-anulado";
}
