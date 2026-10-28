using Bastion.BuildingBlocks.Application.Listados;

namespace Bastion.Inventario.Application;

/// <summary>
/// La foto <b>de este módulo</b>: la que abre la exportación de un listado de Inventario sobre su
/// contexto, el mismo con el que lee el listado (ADR-0073).
/// </summary>
/// <remarks>
/// Una por módulo por lo mismo que <see cref="IUnidadTrabajoDeInventario"/>. Aquí importa más que
/// en ningún otro: el saldo a una fecha se lee con SQL escrito sobre el contexto del módulo, y con
/// la foto de otro cada página se leería en su propia transacción. No daría error, y dos páginas
/// podrían sumar existencias de dos momentos distintos.
/// </remarks>
public interface IFotoDeInventario : IFotoDeLectura;
