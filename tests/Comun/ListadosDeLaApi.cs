using System.Reflection;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;

namespace Bastion.Pruebas.Comun;

/// <summary>
/// Los listados que publica la API, tal como los ve su explorador, y lo que cada uno lee de la URL.
/// </summary>
/// <remarks>
/// <para>
/// <b>De dónde salen.</b> Del explorador de API, que es la misma fuente de la que sale
/// <c>docs/api/openapi.json</c> y por tanto el cliente del frontal. No se reconstruye por
/// reflexión: un parámetro sin <c>[FromQuery]</c> en un <c>GET</c> también se enlaza desde la
/// URL, por convenio, y una reconstrucción que solo mirase el atributo lo pasaría por alto.
/// </para>
/// <para>
/// <b>Un listado se reconoce por lo que DEVUELVE y no por cómo se llama.</b> Una heurística por
/// nombre —los métodos que se llamen <c>Listar</c>— es la que el ítem 1.2 tumbó: se le escapa el
/// primero que se llame <c>Consultar</c>, y no se le escapa con un rojo, se le escapa en silencio.
/// El tipo de la respuesta es un hecho del contrato: si devuelve una página o un tramo, o los
/// lleva dentro, como el saldo con su fecha, es un listado.
/// </para>
/// <para>
/// <b>Compartido y enlazado</b>, como el censo: lo usan la regla de los criterios sensibles, en
/// el carril funcional, y la de quién recibe el texto, en los dos carriles (ítem 3.2). Una copia
/// por fichero se desvía en la primera que reconozca un listado más que la otra.
/// </para>
/// </remarks>
public static class ListadosDeLaApi
{
    /// <summary>Nombre externo del filtro de texto, el que solo publica quien lo mira.</summary>
    /// <remarks>
    /// Tecleado aquí y no tomado de <c>FiltroDeTexto</c> a propósito: es el nombre del contrato, y
    /// si alguien lo cambia en el código, lo que tiene que ponerse rojo es esto.
    /// </remarks>
    public const string Texto = "q";

    /// <summary>Los listados de la API que sirve <paramref name="servicios"/>.</summary>
    /// <param name="servicios">Los servicios de la aplicación, ya construida.</param>
    public static IEnumerable<ApiDescription> De(IServiceProvider servicios)
    {
        IApiDescriptionGroupCollectionProvider explorador =
            servicios.GetRequiredService<IApiDescriptionGroupCollectionProvider>();

        return from grupo in explorador.ApiDescriptionGroups.Items
               from descripcion in grupo.Items
               where descripcion.SupportedResponseTypes.Any(respuesta => EsColeccion(respuesta.Type))
               select descripcion;
    }

    /// <summary>Lo que un listado lee de la cadena de consulta, por su nombre externo.</summary>
    /// <param name="listado">El listado.</param>
    public static IEnumerable<string> DeLaUrl(ApiDescription listado)
    {
        ArgumentNullException.ThrowIfNull(listado);

        return listado.ParameterDescriptions
            .Where(parametro => parametro.Source == BindingSource.Query)
            .Select(parametro => parametro.Name);
    }

    /// <summary>Si el listado se pide con un <c>GET</c>, que es el que lee sus criterios de la URL.</summary>
    /// <remarks>
    /// Los otros son las búsquedas por <c>POST .../buscar</c>, que llevan el criterio en el cuerpo
    /// porque es sensible (ADR-0025) y van por cursor, sin página ni texto.
    /// </remarks>
    /// <param name="listado">El listado.</param>
    public static bool SeConsulta(ApiDescription listado)
    {
        ArgumentNullException.ThrowIfNull(listado);

        return string.Equals(listado.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Si el listado publica el filtro de texto.</summary>
    /// <param name="listado">El listado.</param>
    public static bool PublicaElTexto(ApiDescription listado) =>
        DeLaUrl(listado).Contains(Texto, StringComparer.Ordinal);

    /// <summary>Cómo se nombra un listado en un mensaje: el verbo y la ruta.</summary>
    /// <param name="listado">El listado.</param>
    public static string Nombre(ApiDescription listado)
    {
        ArgumentNullException.ThrowIfNull(listado);

        return (listado.HttpMethod ?? "?") + " /" + listado.RelativePath;
    }

    /// <summary>
    /// Si una respuesta es la de un listado: una página o un tramo, o algo que lleva uno dentro.
    /// </summary>
    /// <param name="tipo">El tipo de la respuesta.</param>
    public static bool EsColeccion(Type? tipo) =>
        EsPagina(tipo)
        || (tipo is not null
            && tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(propiedad => EsPagina(propiedad.PropertyType)));

    private static bool EsPagina(Type? tipo) =>
        tipo is { IsGenericType: true }
        && (tipo.GetGenericTypeDefinition() == typeof(PaginaDe<>)
            || tipo.GetGenericTypeDefinition() == typeof(TramoDe<>));
}
