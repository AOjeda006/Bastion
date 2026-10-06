namespace Bastion.Inventario.Domain.Recuentos;

/// <summary>Una clave del almacén que el alta precarga, con la unidad en la que se contará.</summary>
/// <param name="Clave">La clave, con físico mayor que cero.</param>
/// <param name="UnidadBaseId">La unidad base de su artículo, que es la del ajuste (ADR-0055 §1.2).</param>
public sealed record LineaAPrecargar(ClaveDelRecuento Clave, Guid UnidadBaseId);
