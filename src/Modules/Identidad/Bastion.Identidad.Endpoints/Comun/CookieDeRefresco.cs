using Microsoft.AspNetCore.Http;

namespace Bastion.Identidad.Endpoints.Comun;

/// <summary>
/// Dónde vive el token de refresco: en una cookie que el JavaScript del navegador no puede leer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nunca en <c>localStorage</c>.</b> Todo lo que hay en <c>localStorage</c> lo puede leer
/// cualquier script que se ejecute en la página, así que un único XSS —una biblioteca comprometida,
/// un campo que se pinta sin escapar— se lleva sesiones que duran catorce días. Con
/// <c>HttpOnly</c>, ese mismo XSS puede hacer peticiones en nombre del usuario mientras la página
/// está abierta, pero no se lleva nada que siga sirviendo después.
/// </para>
/// <para>
/// Las cuatro banderas hacen falta y cada una cierra una cosa distinta: <c>HttpOnly</c> la quita
/// del alcance del script; <c>Secure</c> impide que viaje en claro; <c>SameSite=Lax</c> impide que
/// la mande un sitio ajeno —que es la falsificación de petición entre sitios—; y <c>Path</c> la
/// limita a las rutas que la usan, de modo que no se adjunte a cada petición de la API.
/// </para>
/// </remarks>
public static class CookieDeRefresco
{
    /// <summary>Nombre de la cookie.</summary>
    /// <remarks>
    /// El prefijo <c>__Host-</c> no es decorativo: el navegador solo acepta guardarla si viene por
    /// HTTPS, sin <c>Domain</c> y con <c>Path=/</c>. Es la única forma de que un subdominio
    /// comprometido —o alguien que consiga servir HTTP en el mismo nombre— no pueda plantar una
    /// cookie con este nombre.
    /// </remarks>
    public const string Nombre = "__Host-bastion-refresco";

    /// <summary>Escribe la cookie con la emisión nueva.</summary>
    /// <param name="respuesta">Respuesta en curso.</param>
    /// <param name="valor">Token de refresco recién emitido.</param>
    /// <param name="expiraEn">Cuándo deja de valer.</param>
    public static void Escribir(HttpResponse respuesta, string valor, DateTimeOffset expiraEn)
    {
        ArgumentNullException.ThrowIfNull(respuesta);

        respuesta.Cookies.Append(Nombre, valor, Opciones(expiraEn));
    }

    /// <summary>Borra la cookie.</summary>
    /// <remarks>
    /// Con LAS MISMAS opciones con las que se escribió. Un borrado con banderas distintas no borra
    /// nada —el navegador no las considera la misma cookie— y el cierre de sesión dejaría el token
    /// donde estaba, aunque el servidor ya lo hubiera revocado.
    /// </remarks>
    /// <param name="respuesta">Respuesta en curso.</param>
    public static void Borrar(HttpResponse respuesta)
    {
        ArgumentNullException.ThrowIfNull(respuesta);

        respuesta.Cookies.Delete(Nombre, Opciones(DateTimeOffset.UnixEpoch));
    }

    /// <summary>Lee la cookie de la petición, o nulo si no viene.</summary>
    /// <param name="peticion">Petición en curso.</param>
    public static string? Leer(HttpRequest peticion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        return peticion.Cookies.TryGetValue(Nombre, out string? valor) ? valor : null;
    }

    private static CookieOptions Opciones(DateTimeOffset expiraEn) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Expires = expiraEn,
        IsEssential = true,
    };
}
