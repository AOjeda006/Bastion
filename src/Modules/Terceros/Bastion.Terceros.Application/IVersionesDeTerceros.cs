using Bastion.BuildingBlocks.Application.Concurrencia;

namespace Bastion.Terceros.Application;

/// <summary>
/// Las versiones <b>de este módulo</b>: las lee del contexto de Terceros y de ningún otro.
/// </summary>
/// <remarks>
/// Una interfaz por módulo por lo mismo que <see cref="IUnidadTrabajoDeTerceros"/>: el contenedor
/// resuelve por tipo y la última inscripción gana. Con una compartida, pedir la versión de un
/// tercero se la pediría al contexto de otro módulo, que no lo rastrea.
/// </remarks>
public interface IVersionesDeTerceros : IVersiones;
