using Bastion.BuildingBlocks.Application;

namespace Bastion.Organizacion.Application;

/// <summary>
/// La unidad de trabajo <b>de este módulo</b>: confirma sobre el contexto de Organización y sobre
/// ningún otro.
/// </summary>
/// <remarks>
/// <para>
/// Existe porque el contenedor resuelve por tipo, y con un único <see cref="IUnidadTrabajo"/> para
/// todos los módulos la última inscripción gana: los casos de uso de Organización acabarían
/// confirmando sobre el contexto de Identidad. Y no daría error —<c>SaveChangesAsync</c> sobre un
/// contexto que no rastrea nada devuelve cero y calla—, así que el alta contestaría <c>201</c> y
/// la fila no existiría.
/// </para>
/// <para>
/// Con una interfaz por módulo, el contenedor no tiene nada que adivinar y equivocarse deja de ser
/// posible: pedir la de otro módulo no compila, porque un módulo no ve la capa de aplicación
/// ajena (§4).
/// </para>
/// </remarks>
public interface IUnidadTrabajoDeOrganizacion : IUnidadTrabajo;
