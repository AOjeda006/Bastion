using Bastion.BuildingBlocks.Application.Concurrencia;

namespace Bastion.Inventario.Application;

/// <summary>
/// Las versiones <b>de este módulo</b>: las lee del contexto de Inventario y de ningún otro.
/// </summary>
/// <remarks>
/// <para>
/// <b>Entra en el ítem 2.12, con la primera ficha del módulo</b>: el recuento publica su
/// <c>ETag</c>, y cada línea el suyo (ADR-0055 §4). Hasta entonces ningún documento de Inventario
/// se leía por la API.
/// </para>
/// <para>
/// Una interfaz por módulo por lo mismo que <see cref="IUnidadTrabajoDeInventario"/>: el contenedor
/// resuelve por tipo y la última inscripción gana. Con una compartida, pedir la versión de un
/// recuento se la pediría al contexto de otro módulo, que no lo rastrea.
/// </para>
/// </remarks>
public interface IVersionesDeInventario : IVersiones;
