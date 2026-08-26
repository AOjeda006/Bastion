using System.Security.Claims;
using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Bastion.BuildingBlocks.Infrastructure.Autorizacion;

/// <summary>Lo que una política de permiso exige: ese permiso, en el token.</summary>
/// <param name="Permiso">Permiso exigido.</param>
public sealed record RequisitoDePermiso(Permiso Permiso) : IAuthorizationRequirement;

/// <summary>
/// Fabrica al vuelo la política de cada <see cref="ExigePermisoAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// Un permiso por endpoint significaría, con dieciséis módulos, un par de centenares de políticas
/// registradas a mano en el arranque. La lista se olvidaría, y un permiso sin política registrada
/// no da error al compilar ni al arrancar: da un <c>500</c> la primera vez que alguien llama a ese
/// endpoint —o, según cómo se configure, un <c>403</c> a todo el mundo, que es peor porque parece
/// que el sistema funciona.
/// </para>
/// <para>
/// La política de RESPALDO —la que se aplica donde no hay atributo— es «hay que estar autenticado»,
/// y se configura en el <i>composition root</i>. Es la mitad de «denegar por defecto»; la otra
/// mitad es el test que comprueba que toda acción declara su permiso, porque estar autenticado no
/// es estar autorizado.
/// </para>
/// </remarks>
/// <param name="opciones">Opciones de autorización del host.</param>
public sealed class ProveedorDePoliticasDePermisos(IOptions<AuthorizationOptions> opciones)
    : DefaultAuthorizationPolicyProvider(opciones)
{
    /// <inheritdoc/>
    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        ArgumentNullException.ThrowIfNull(policyName);

        if (!policyName.StartsWith(ExigePermisoAttribute.Prefijo, StringComparison.Ordinal) ||
            !Permiso.Intentar(policyName[ExigePermisoAttribute.Prefijo.Length..], out Permiso? permiso))
        {
            return base.GetPolicyAsync(policyName);
        }

        return Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new RequisitoDePermiso(permiso!))
            .Build());
    }
}

/// <summary>
/// Decide si el token trae el permiso exigido.
/// </summary>
/// <remarks>
/// <para>
/// Compara valores enteros contra los <i>claims</i> <c>permiso</c>, sin comodines, sin prefijos y
/// sin «el que tiene <c>administrar</c> lo tiene todo». Cualquiera de esas comodidades convierte
/// la lista de permisos en un lenguaje con reglas propias, y las reglas propias se interpretan mal
/// justo el día que importa.
/// </para>
/// <para>
/// <c>StringComparison.Ordinal</c> a propósito: comparar permisos sin distinguir mayúsculas haría
/// que la cultura del servidor participara en una decisión de autorización — el problema clásico
/// de la «i» turca, que en una comparación insensible hace que dos cadenas distintas se consideren
/// iguales.
/// </para>
/// </remarks>
public sealed class ManejadorDePermisos : AuthorizationHandler<RequisitoDePermiso>
{
    // Los parámetros van en inglés —`context`, `requirement`— y no en castellano como el resto
    // del código: son los nombres del método base, y renombrarlos rompe a quien llame por nombre
    // de parámetro. Es la excepción que la propia regla CA1725 obliga a hacer, y solo alcanza a
    // las firmas heredadas del marco.
    /// <inheritdoc/>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RequisitoDePermiso requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        bool loTiene = context.User.Claims.Any(reclamacion =>
            string.Equals(reclamacion.Type, ClaimsDeBastion.Permiso, StringComparison.Ordinal)
            && string.Equals(reclamacion.Value, requirement.Permiso.Valor, StringComparison.Ordinal));

        if (loTiene)
        {
            context.Succeed(requirement);
        }

        // Si no lo tiene, no se llama a `Fail`: basta con no dar por cumplido el requisito. `Fail`
        // sería un veto que ningún otro manejador podría levantar, y eso cierra la puerta a
        // requisitos alternativos —«este permiso O el de administración»— que la fase 1 va a
        // necesitar.
        return Task.CompletedTask;
    }
}

/// <summary>
/// Quién opera, leído del token de la petición en curso.
/// </summary>
/// <remarks>
/// <para>
/// <b>La empresa sale de aquí y de ningún otro sitio</b> (R8). No hay ningún camino que la lea del
/// cuerpo, de la ruta o de la cadena de consulta, y por eso no puede haber un caso de uso que se
/// olvide de comprobarla: el dato no llega por ahí.
/// </para>
/// <para>
/// Los nombres de los <i>claims</i> salen de <see cref="ClaimsDeBastion"/>, las mismas constantes
/// que usa el emisor. Es lo que impide que quien escribe y quien lee dejen de entenderse sin que
/// nada falle: un token con <c>permiso</c> leído buscando <c>permisos</c> no da error, da una lista
/// vacía.
/// </para>
/// </remarks>
/// <param name="acceso">Acceso al contexto HTTP de la petición en curso.</param>
public sealed class UsuarioActual(IHttpContextAccessor acceso) : IUsuarioActual
{
    private ClaimsPrincipal? Principal => acceso.HttpContext?.User;

    /// <inheritdoc/>
    public bool EstaAutenticado => Principal?.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc/>
    public Guid UsuarioId => Leer(ClaimsDeBastion.Sujeto);

    /// <inheritdoc/>
    public Guid EmpresaId => Leer(ClaimsDeBastion.Empresa);

    /// <inheritdoc/>
    public bool Tiene(Permiso permiso)
    {
        ArgumentNullException.ThrowIfNull(permiso);

        return Principal?.Claims.Any(reclamacion =>
            string.Equals(reclamacion.Type, ClaimsDeBastion.Permiso, StringComparison.Ordinal)
            && string.Equals(reclamacion.Value, permiso.Valor, StringComparison.Ordinal)) ?? false;
    }

    // LANZA si el claim no está o no es un Guid, en vez de devolver `Guid.Empty`. Un
    // identificador vacío recorrería el sistema entero pareciendo un dato: consultaría por la
    // empresa 00000000-…, no encontraría nada y devolvería una lista vacía — «no tienes
    // almacenes» en lugar de «esta petición no debería haber llegado hasta aquí». Que reviente es
    // lo que convierte el fallo en un 500 con traza, que es lo que hay que poder arreglar.
    private Guid Leer(string tipo)
    {
        string? valor = Principal?.FindFirst(tipo)?.Value;

        return Guid.TryParse(valor, out Guid identificador)
            ? identificador
            : throw new InvalidOperationException(
                $"La petición ha llegado a un caso de uso sin el claim «{tipo}» en el token. " +
                "O el endpoint no exige autenticación y debería, o el emisor ha dejado de " +
                "escribir ese claim.");
    }
}
