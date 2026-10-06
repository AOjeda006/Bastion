using Bastion.BuildingBlocks.Domain.Eventos;

namespace Bastion.Inventario.Contracts.Recuentos;

/// <summary>
/// Un recuento en curso se descartó: no movió nada, no se numeró, y su almacén queda libre para otro
/// (ADR-0055 §1.6).
/// </summary>
/// <param name="RecuentoId">El documento descartado.</param>
/// <param name="EmpresaId">Empresa a la que pertenece (R8).</param>
/// <param name="AlmacenId">El almacén que queda libre.</param>
public sealed record RecuentoDescartado(Guid RecuentoId, Guid EmpresaId, Guid AlmacenId) : EventoDeIntegracion
{
    /// <summary>Nombre con el que viaja por la bandeja de salida.</summary>
    public const string Nombre = "inventario.recuento-descartado";
}
