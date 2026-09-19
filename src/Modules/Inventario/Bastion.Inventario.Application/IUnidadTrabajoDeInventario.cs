using Bastion.BuildingBlocks.Application;

namespace Bastion.Inventario.Application;

/// <summary>La unidad de trabajo del módulo Inventario.</summary>
/// <remarks>
/// Es propia del módulo, como en los demás: un <c>IUnidadTrabajo</c> compartido haría que
/// confirmar un ajuste guardara de paso lo que otro módulo tuviera a medias en la misma petición.
/// </remarks>
public interface IUnidadTrabajoDeInventario : IUnidadTrabajo;
