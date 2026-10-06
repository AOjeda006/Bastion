using Bastion.BuildingBlocks.Domain.Eventos;

namespace Bastion.Inventario.Contracts.Recuentos;

/// <summary>Un recuento quedó anulado, y su ajuste, si lo tenía, con él (ADR-0055 §9).</summary>
/// <remarks>
/// Lo que compensa el libro lo cuenta el <c>AjusteAnulado</c> de su ajuste. Este cuenta la
/// transición del recuento.
/// </remarks>
/// <param name="RecuentoId">El documento anulado.</param>
/// <param name="EmpresaId">Empresa a la que pertenece (R8).</param>
/// <param name="AlmacenId">El almacén que se contó.</param>
public sealed record RecuentoAnulado(Guid RecuentoId, Guid EmpresaId, Guid AlmacenId) : EventoDeIntegracion
{
    /// <summary>Nombre con el que viaja por la bandeja de salida.</summary>
    public const string Nombre = "inventario.recuento-anulado";
}
