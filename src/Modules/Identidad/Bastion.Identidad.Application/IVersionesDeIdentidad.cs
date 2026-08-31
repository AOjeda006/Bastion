using Bastion.BuildingBlocks.Application.Concurrencia;

namespace Bastion.Identidad.Application;

/// <summary>
/// Las versiones <b>de este módulo</b>: las lee del contexto de Identidad y de ningún otro.
/// </summary>
/// <remarks>
/// Una interfaz por módulo por lo mismo que <see cref="IUnidadTrabajoDeIdentidad"/>: el contenedor
/// resuelve por tipo y la última inscripción gana.
/// </remarks>
public interface IVersionesDeIdentidad : IVersiones;
