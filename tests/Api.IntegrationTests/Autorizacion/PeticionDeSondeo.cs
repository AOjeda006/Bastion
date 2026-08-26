using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Bastion.Api.IntegrationTests.Autorizacion;

/// <summary>
/// Convierte una acción de la tabla de rutas en una petición HTTP de verdad.
/// </summary>
/// <remarks>
/// <para>
/// Es lo que permite barrer <b>todas</b> las acciones en vez de las que a uno se le ocurran. Un
/// endpoint nuevo entra en el barrido el día que se escribe, sin que nadie se acuerde de añadirlo
/// a una lista: es la diferencia entre una regla probada y una regla que se probó una vez.
/// </para>
/// <para>
/// Los identificadores de la ruta se rellenan con GUID inventados a propósito. La autorización
/// ocurre <b>antes</b> de que la acción busque nada, así que un 401 o un 403 llegan igual con un
/// identificador que no existe; y si en vez de eso llega un 404, lo que se ha descubierto es que
/// la puerta está abierta.
/// </para>
/// </remarks>
internal static partial class PeticionDeSondeo
{
    /// <summary>Compone la petición que ejercita una acción.</summary>
    /// <param name="accion">Acción de la tabla de rutas.</param>
    /// <param name="token">Token de acceso, o nulo para llamar sin credenciales.</param>
    public static HttpRequestMessage De(ActionDescriptor accion, string? token)
    {
        ArgumentNullException.ThrowIfNull(accion);

        HttpRequestMessage peticion = new(
            new HttpMethod(Verbo(accion)),
            "/" + Ruta(accion.AttributeRouteInfo?.Template ?? string.Empty));

        // Un cuerpo vacío pero bien formado: lo que se prueba es la puerta, y la puerta está
        // antes del enlace de modelo. Con un cuerpo ausente, un 415 taparía el 401 que se busca.
        if (!string.Equals(peticion.Method.Method, "GET", StringComparison.Ordinal))
        {
            peticion.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        if (token is not null)
        {
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return peticion;
    }

    /// <summary>Nombre legible de la acción, para los mensajes de fallo.</summary>
    /// <param name="accion">Acción de la tabla de rutas.</param>
    public static string Nombre(ActionDescriptor accion) => accion is ControllerActionDescriptor controlador
        ? $"{controlador.ControllerTypeInfo.Name}.{controlador.ActionName}"
        : accion.DisplayName ?? "(sin nombre)";

    /// <summary>El permiso que exige la acción, o nulo si no exige ninguno.</summary>
    /// <param name="accion">Acción de la tabla de rutas.</param>
    public static Permiso? PermisoDe(ActionDescriptor accion)
    {
        ArgumentNullException.ThrowIfNull(accion);

        return accion.EndpointMetadata.OfType<ExigePermisoAttribute>().FirstOrDefault()?.Permiso;
    }

    private static string Verbo(ActionDescriptor accion) =>
        accion.ActionConstraints?
            .OfType<HttpMethodActionConstraint>()
            .SelectMany(restriccion => restriccion.HttpMethods)
            .FirstOrDefault() ?? "GET";

    private static string Ruta(string plantilla) =>
        Parametro().Replace(plantilla, _ => Guid.CreateVersion7().ToString());

    [GeneratedRegex(@"\{[^}]+\}")]
    private static partial Regex Parametro();
}
