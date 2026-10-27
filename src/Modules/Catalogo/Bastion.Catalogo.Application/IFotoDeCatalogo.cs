using Bastion.BuildingBlocks.Application.Listados;

namespace Bastion.Catalogo.Application;

/// <summary>
/// La foto <b>de este módulo</b>: la que abre la exportación de un listado de Catálogo sobre su
/// contexto, el mismo con el que lee el listado (ADR-0073).
/// </summary>
/// <remarks>
/// Una por módulo por lo mismo que <see cref="IUnidadTrabajoDeCatalogo"/>: con una sola
/// <see cref="IFotoDeLectura"/> para todos, la última inscripción ganaría, y la exportación de
/// artículos abriría su foto sobre el contexto de otro módulo. No daría error: cada página se
/// leería en su propia transacción, fuera de la foto.
/// </remarks>
public interface IFotoDeCatalogo : IFotoDeLectura;
