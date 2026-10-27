using Bastion.BuildingBlocks.Application.Listados;

namespace Bastion.Organizacion.Application;

/// <summary>
/// La foto <b>de este módulo</b>: la que abre la exportación de un listado de Organización sobre su
/// contexto, el mismo con el que lee el listado (ADR-0073).
/// </summary>
/// <remarks>
/// Una por módulo por lo mismo que <see cref="IUnidadTrabajoDeOrganizacion"/>: con una sola
/// <see cref="IFotoDeLectura"/> para todos, la última inscripción ganaría, y la exportación de
/// almacenes abriría su foto sobre el contexto de otro módulo. No daría error: cada página se
/// leería en su propia transacción, fuera de la foto, y un alta a mitad podría repetir una fila o
/// saltarse otra sin que nada lo dijera.
/// </remarks>
public interface IFotoDeOrganizacion : IFotoDeLectura;
