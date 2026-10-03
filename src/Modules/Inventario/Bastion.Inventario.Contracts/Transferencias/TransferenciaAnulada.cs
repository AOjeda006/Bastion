using Bastion.BuildingBlocks.Domain.Eventos;

namespace Bastion.Inventario.Contracts.Transferencias;

/// <summary>Una transferencia quedó anulada: un inverso ya la compensa (R2).</summary>
/// <param name="TransferenciaId">El documento anulado.</param>
/// <param name="EmpresaId">Empresa a la que pertenece (R8).</param>
public sealed record TransferenciaAnulada(Guid TransferenciaId, Guid EmpresaId) : EventoDeIntegracion
{
    /// <summary>Nombre con el que viaja por la bandeja de salida.</summary>
    public const string Nombre = "inventario.transferencia-anulada";
}
