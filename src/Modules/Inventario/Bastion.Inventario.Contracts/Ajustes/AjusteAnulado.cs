using Bastion.BuildingBlocks.Domain.Eventos;

namespace Bastion.Inventario.Contracts.Ajustes;

/// <summary>Un ajuste confirmado quedó anulado por un inverso (R2).</summary>
/// <remarks>
/// <para>
/// <b>Anulado no es borrado</b>, y el evento se llama así a propósito: las filas que el ajuste
/// escribió en el libro <b>siguen ahí</b>, porque el libro es de solo añadido. Lo que compensa el
/// efecto es otro documento con el signo contrario, y desde el ítem 2.5 ese documento existe: el
/// original no llega a este evento sin que su inverso esté confirmado y apuntándole.
/// </para>
/// <para>
/// <b>Y el evento no lo nombra</b>, aunque exista. Quien se suscriba a esto quiere saber que un
/// documento dejó de valer; el par se recorre por <c>AnulaAId</c>, que es el único sitio donde
/// está escrito. Meter aquí el identificador del inverso sería el segundo sitio, y el segundo
/// sitio es el que acaba discrepando — el mismo motivo por el que la columna es una sola.
/// </para>
/// </remarks>
/// <param name="AjusteId">El documento que quedó anulado.</param>
/// <param name="EmpresaId">Empresa a la que pertenece (R8).</param>
public sealed record AjusteAnulado(Guid AjusteId, Guid EmpresaId) : EventoDeIntegracion
{
    /// <summary>Nombre con el que viaja por la bandeja de salida.</summary>
    public const string Nombre = "inventario.ajuste-anulado";
}
