using Bastion.BuildingBlocks.Application.Concurrencia;

namespace Bastion.Organizacion.Application;

/// <summary>
/// Las versiones <b>de este módulo</b>: las lee del contexto de Organización y de ningún otro.
/// </summary>
/// <remarks>
/// Una interfaz por módulo por lo mismo que <see cref="IUnidadTrabajoDeOrganizacion"/>: el
/// contenedor resuelve por tipo y la última inscripción gana. Con una compartida, pedir la
/// versión de un almacén se la pediría al contexto de Identidad, que no lo rastrea — y ahí el
/// fallo sí sería ruidoso, pero por el sitio equivocado y en tiempo de ejecución.
/// </remarks>
public interface IVersionesDeOrganizacion : IVersiones;
