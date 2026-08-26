using Bastion.BuildingBlocks.Domain.Autorizacion;

namespace Bastion.BuildingBlocks.Application.Autorizacion;

/// <summary>
/// Quién está pidiendo la operación y en qué empresa. Es la única fuente de esos dos datos para
/// un caso de uso.
/// </summary>
/// <remarks>
/// <para>
/// <b>R8, y por qué el puerto no tiene un <c>EmpresaId</c> de entrada.</b> El identificador de
/// empresa sale del <i>claim</i> del usuario, <b>jamás</b> del cuerpo de la petición ni de la
/// cadena de consulta. Un caso de uso que lo recibiera como parámetro dejaría escrito el camino
/// por el que un cliente elige empresa: bastaría con mandar otro identificador. Aquí no hay ese
/// parámetro, así que ese camino no se puede escribir por descuido —solo cambiando este puerto,
/// que es un cambio que se ve en la revisión—.
/// </para>
/// <para>
/// La empresa que se devuelve es la <b>activa</b>, una sola, no la lista de aquellas a las que
/// el usuario pertenece: la lista viaja en la respuesta del login, la activa viaja en el token
/// (§9 del plan maestro). Cambiar de empresa reemite el token; no es un parámetro de petición.
/// </para>
/// <para>
/// Lo implementa Infrastructure sobre el <c>ClaimsPrincipal</c> de la petición en curso. Los
/// casos de uso no conocen ASP.NET Core.
/// </para>
/// </remarks>
public interface IUsuarioActual
{
    /// <summary>Si la petición trae un token válido.</summary>
    /// <remarks>
    /// Lo normal es no tener que preguntarlo: la tubería devuelve <c>401</c> antes de llegar al
    /// caso de uso. Está para los sitios que admiten anónimo a propósito.
    /// </remarks>
    bool EstaAutenticado { get; }

    /// <summary>Identificador del usuario que pide la operación.</summary>
    /// <exception cref="InvalidOperationException">Si la petición no está autenticada.</exception>
    Guid UsuarioId { get; }

    /// <summary>Empresa activa, del <i>claim</i> (R8).</summary>
    /// <exception cref="InvalidOperationException">
    /// Si la petición no está autenticada, o si el token no lleva empresa activa. Lanza en vez de
    /// devolver <see cref="Guid.Empty"/> a propósito: un <c>Guid.Empty</c> silencioso acabaría
    /// escrito en la columna <c>empresa_id</c> de una fila real.
    /// </exception>
    Guid EmpresaId { get; }

    /// <summary>Si el usuario tiene concedido ese permiso en la empresa activa.</summary>
    /// <param name="permiso">Permiso que se comprueba.</param>
    bool Tiene(Permiso permiso);
}
