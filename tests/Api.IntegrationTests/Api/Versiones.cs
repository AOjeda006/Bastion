using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Api;

/// <summary>
/// Escribir como escribe un cliente que respeta R11: leer el recurso, quedarse con su <c>ETag</c>
/// y devolverlo en el <c>If-Match</c> de la escritura.
/// </summary>
/// <remarks>
/// <para>
/// <b>La etiqueta se lee de la API, no se fabrica.</b> Un ayudante que se inventara un
/// <c>If-Match: *</c> —o que sacara la versión de la base de datos— dejaría estos tests verdes con
/// la cabecera puesta y sin haber comprobado nunca que el <c>ETag</c> que emite el <c>GET</c> es el
/// que el <c>PUT</c> acepta. Es justo el ida y vuelta que hay que ejercitar: si el <c>GET</c>
/// dejara de emitirlo, o emitiera uno con otra forma, esto se pone rojo en la primera línea.
/// </para>
/// <para>
/// <b>Y por eso el <c>ShouldNotBeNull</c> del principio no sobra.</b> Sin él, un <c>GET</c> que
/// dejara de poner la cabecera mandaría un <c>If-Match</c> vacío, el filtro contestaría
/// <c>428</c> y el rojo se leería como «esta acción exige una cabecera que le estoy mandando»,
/// que es un sitio malísimo por donde empezar a mirar.
/// </para>
/// </remarks>
public static class Versiones
{
    /// <summary>Lee el recurso y devuelve el <c>ETag</c> que emite.</summary>
    /// <param name="cliente">Cliente autenticado.</param>
    /// <param name="recurso">Ruta del recurso, la del <c>GET</c> por identificador.</param>
    public static async Task<string> EtiquetaDeAsync(this HttpClient cliente, string recurso)
    {
        ArgumentNullException.ThrowIfNull(cliente);

        using HttpResponseMessage lectura = await cliente.GetAsync(recurso).ConfigureAwait(false);

        lectura.StatusCode.ShouldBe(
            HttpStatusCode.OK, await Escenario.Detalle(lectura).ConfigureAwait(false));

        EntityTagHeaderValue? etiqueta = lectura.Headers.ETag;

        etiqueta.ShouldNotBeNull($"{recurso} no emite ETag, así que no hay versión que citar");

        return etiqueta.ToString();
    }

    /// <summary>Modifica el recurso citando la versión que acaba de leer.</summary>
    /// <typeparam name="T">Tipo del cuerpo.</typeparam>
    /// <param name="cliente">Cliente autenticado.</param>
    /// <param name="recurso">Ruta del recurso.</param>
    /// <param name="cuerpo">Lo que se manda.</param>
    public static async Task<HttpResponseMessage> ModificarAsync<T>(
        this HttpClient cliente, string recurso, T cuerpo)
    {
        ArgumentNullException.ThrowIfNull(cliente);

        string etiqueta = await cliente.EtiquetaDeAsync(recurso).ConfigureAwait(false);

        return await cliente.EnviarConVersionAsync(
            HttpMethod.Put, recurso, etiqueta, JsonContent.Create(cuerpo)).ConfigureAwait(false);
    }

    /// <summary>Suprime el recurso citando la versión que acaba de leer.</summary>
    /// <param name="cliente">Cliente autenticado.</param>
    /// <param name="recurso">Ruta del recurso.</param>
    public static async Task<HttpResponseMessage> SuprimirAsync(
        this HttpClient cliente, string recurso)
    {
        ArgumentNullException.ThrowIfNull(cliente);

        string etiqueta = await cliente.EtiquetaDeAsync(recurso).ConfigureAwait(false);

        return await cliente.EnviarConVersionAsync(HttpMethod.Delete, recurso, etiqueta)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Acciona una subruta del recurso —el bloqueo, el cierre— citando la versión del recurso.
    /// </summary>
    /// <remarks>
    /// La versión se lee del <b>recurso</b> y se manda contra la <b>subruta</b>, porque la subruta
    /// no tiene <c>GET</c> propio: no es otro recurso, es otra puerta al mismo. Ese detalle es el
    /// que hace que bloquear un almacén y modificarlo compitan por la misma versión, que es lo que
    /// se quiere.
    /// </remarks>
    /// <param name="cliente">Cliente autenticado.</param>
    /// <param name="recurso">Ruta del recurso, de donde sale la versión.</param>
    /// <param name="puerta">Ruta de la acción, a donde va la petición.</param>
    /// <param name="metodo">Método con el que se acciona.</param>
    public static async Task<HttpResponseMessage> AccionarAsync(
        this HttpClient cliente, string recurso, string puerta, HttpMethod metodo)
    {
        ArgumentNullException.ThrowIfNull(cliente);

        string etiqueta = await cliente.EtiquetaDeAsync(recurso).ConfigureAwait(false);

        return await cliente.EnviarConVersionAsync(metodo, puerta, etiqueta).ConfigureAwait(false);
    }

    /// <summary>Manda una petición con el <c>If-Match</c> que se le diga, sin leer nada antes.</summary>
    /// <param name="cliente">Cliente autenticado.</param>
    /// <param name="metodo">Método HTTP.</param>
    /// <param name="ruta">A dónde va.</param>
    /// <param name="etiqueta">El valor del <c>If-Match</c>. Nulo para no mandar la cabecera.</param>
    /// <param name="cuerpo">Cuerpo, si lleva.</param>
    public static Task<HttpResponseMessage> EnviarConVersionAsync(
        this HttpClient cliente,
        HttpMethod metodo,
        string ruta,
        string? etiqueta,
        HttpContent? cuerpo = null)
    {
        ArgumentNullException.ThrowIfNull(cliente);

        HttpRequestMessage peticion = new(metodo, ruta) { Content = cuerpo };

        if (etiqueta is not null)
        {
            peticion.Headers.TryAddWithoutValidation("If-Match", etiqueta);
        }

        return cliente.SendAsync(peticion);
    }
}
