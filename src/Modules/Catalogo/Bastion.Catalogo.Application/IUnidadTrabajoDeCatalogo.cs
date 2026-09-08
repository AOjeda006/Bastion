using Bastion.BuildingBlocks.Application;

namespace Bastion.Catalogo.Application;

/// <summary>
/// La unidad de trabajo <b>de este módulo</b>: confirma sobre el contexto de Catálogo y sobre
/// ningún otro.
/// </summary>
/// <remarks>
/// Una interfaz por módulo por lo mismo que en los otros tres: el contenedor resuelve por tipo y,
/// con un único <see cref="IUnidadTrabajo"/> para todos, la última inscripción gana. El fallo no
/// sería ruidoso —<c>SaveChangesAsync</c> sobre un contexto que no rastrea nada devuelve cero y
/// calla—, así que el alta contestaría <c>201</c> y la fila no existiría.
/// </remarks>
public interface IUnidadTrabajoDeCatalogo : IUnidadTrabajo;
