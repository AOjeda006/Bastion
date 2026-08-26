using Bastion.BuildingBlocks.Domain.Autorizacion;
using Microsoft.AspNetCore.Authorization;

namespace Bastion.BuildingBlocks.Infrastructure.Autorizacion;

/// <summary>
/// Declara QUÉ permiso hace falta para ejecutar una acción.
/// </summary>
/// <remarks>
/// <para>
/// Es lo que sustituye a <c>[Authorize(Roles = "Admin")]</c> esparcido por los controladores. Con
/// roles en el atributo, cada endpoint decide por su cuenta quién entra, y añadir un rol nuevo
/// obliga a repasar el proyecto entero buscando cadenas de texto. Aquí el endpoint declara la
/// <b>facultad</b> que necesita —<c>organizacion.empresa.crear</c>— y quién la tiene lo dicen los
/// roles, que son datos.
/// </para>
/// <para>
/// El permiso viaja como nombre de política, y
/// <see cref="ProveedorDePoliticasDePermisos"/> la fabrica al vuelo. Así no hay que registrar
/// doscientas políticas a mano al arrancar ni acordarse de añadir la de cada permiso nuevo: un
/// permiso que exista y no tenga política registrada sería un endpoint que no deja pasar a nadie.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class ExigePermisoAttribute : AuthorizeAttribute
{
    /// <summary>Prefijo con el que se distingue una política de permiso de cualquier otra.</summary>
    public const string Prefijo = "permiso:";

    /// <summary>Exige un permiso concreto.</summary>
    /// <param name="permiso">Permiso en la forma <c>modulo.recurso.accion</c>.</param>
    /// <exception cref="ArgumentException">Si no tiene la forma de un permiso.</exception>
    public ExigePermisoAttribute(string permiso)
    {
        // Se valida AQUÍ, al construir el atributo, y no cuando llegue la primera petición. Un
        // permiso mal escrito en un endpoint es una puerta que no abre nunca, y descubrirlo en
        // producción cuesta mucho más que reventar al arrancar. `Permiso.De` lanza si no cuadra.
        Permiso = Permiso.De(permiso);
        Policy = Prefijo + Permiso.Valor;
    }

    /// <summary>El permiso exigido, ya validado.</summary>
    public Permiso Permiso { get; }
}
