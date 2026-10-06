using Bastion.BuildingBlocks.Domain.Eventos;

namespace Bastion.Inventario.Contracts.Recuentos;

/// <summary>Un recuento se confirmó, y su ajuste, si lo hay, ya movió el libro.</summary>
/// <remarks>
/// <b>No lleva importes, a propósito</b> (ADR-0055 §1). El asiento de regularización del §8.3 sale
/// del <c>AjusteConfirmado</c> del ajuste que genera, como el de cualquier otro ajuste. Este evento
/// cuenta la transición del recuento, que la R1 no deja hacer sin evento, y nadie puede asentar con
/// él.
/// </remarks>
/// <param name="RecuentoId">El documento que se confirmó.</param>
/// <param name="EmpresaId">Empresa a la que pertenece (R8).</param>
/// <param name="AlmacenId">El almacén que se contó.</param>
/// <param name="FechaDeConfirmacion">El día de la confirmación, que es el de su ajuste.</param>
/// <param name="Lineas">Cuántas claves llevaba.</param>
/// <param name="AjusteId">El ajuste que movió la diferencia, o <c>null</c> si todo cuadraba.</param>
public sealed record RecuentoConfirmado(
    Guid RecuentoId,
    Guid EmpresaId,
    Guid AlmacenId,
    DateOnly FechaDeConfirmacion,
    int Lineas,
    Guid? AjusteId) : EventoDeIntegracion
{
    /// <summary>Nombre con el que viaja por la bandeja de salida.</summary>
    public const string Nombre = "inventario.recuento-confirmado";
}
