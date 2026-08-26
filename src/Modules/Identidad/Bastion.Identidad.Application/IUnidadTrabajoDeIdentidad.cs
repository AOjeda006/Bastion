using Bastion.BuildingBlocks.Application;

namespace Bastion.Identidad.Application;

/// <summary>
/// La unidad de trabajo <b>de este módulo</b>: confirma sobre el contexto de Identidad y sobre
/// ningún otro.
/// </summary>
/// <remarks>
/// Misma razón que en Organización: el contenedor resuelve por tipo, y un único
/// <see cref="IUnidadTrabajo"/> compartido haría que ganara la última inscripción sin que nadie se
/// enterase. Aquí el precio sería peor que una fila que falta: un usuario creado que no se graba,
/// o un token de refresco revocado que se queda vivo.
/// </remarks>
public interface IUnidadTrabajoDeIdentidad : IUnidadTrabajo;
