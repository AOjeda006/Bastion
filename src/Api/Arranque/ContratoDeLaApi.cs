using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Bastion.Api.Arranque;

/// <summary>
/// El documento OpenAPI: lo que se genera al compilar y de lo que sale el cliente del frontal.
/// </summary>
/// <remarks>
/// <para>
/// Casi todo el contenido lo pone ASP.NET Core solo, leyendo los controladores, los DTO y sus
/// comentarios de documentación. Aquí solo se corrige lo que sale mal o no sale: la portada, el
/// identificador de cada operación, el esquema de autenticación y dos nombres.
/// </para>
/// <para>
/// <b>Nada de esto se sirve por HTTP.</b> El documento se escribe en <c>docs/api/openapi.json</c>
/// al compilar y se versiona; la CI lo regenera y falla si difiere. Ver <c>Bastion.Api.csproj</c>.
/// </para>
/// </remarks>
internal static class ContratoDeLaApi
{
    private const string EsquemaDelTestigo = "testigoDeAcceso";

    /// <summary>Registra el generador del documento con sus arreglos.</summary>
    /// <param name="servicios">Contenedor del host.</param>
    internal static IServiceCollection AgregarContratoDeLaApi(this IServiceCollection servicios) =>
        servicios.AddOpenApi(opciones =>
        {
            opciones.CreateSchemaReferenceId = NombreDelEsquema;
            opciones.AddDocumentTransformer(PonerPortadaYEsquemaDeAcceso);
            opciones.AddOperationTransformer(PonerIdentificadorYQuitarLaDescripcionPrestada);
        });

    /// <summary>El nombre con el que un tipo aparece en <c>components/schemas</c>.</summary>
    /// <remarks>
    /// Existe por los genéricos. <c>PaginaDe&lt;AlmacenDto&gt;</c> sale por omisión como
    /// <c>PaginaDeOfAlmacenDto</c>: ese <c>Of</c> es la manera que tiene el generador de escribir
    /// «de», y el resultado es un nombre en dos idiomas que acaba tal cual en el TypeScript del
    /// frontal. Se le quita, y queda <c>PaginaDeAlmacenDto</c>.
    /// <para>
    /// Solo se toca a los tipos genéricos, que en esta solución son los dos <c>PaginaDe&lt;T&gt;</c>
    /// —uno por módulo, porque <c>Contracts</c> no referencia nada—. A un tipo corriente no se le
    /// mira el nombre.
    /// </para>
    /// </remarks>
    /// <param name="tipo">El tipo tal como lo ve el serializador.</param>
    private static string? NombreDelEsquema(JsonTypeInfo tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        string? nombre = OpenApiOptions.CreateDefaultSchemaReferenceId(tipo);

        if (nombre is null || !tipo.Type.IsGenericType)
        {
            return nombre;
        }

        int separador = nombre.IndexOf("Of", StringComparison.Ordinal);

        return separador < 0 ? nombre : nombre.Remove(separador, 2);
    }

    /// <summary>Portada del documento y el esquema de autenticación que usa toda la API.</summary>
    /// <remarks>
    /// El título por omisión es el nombre del ensamblado con la versión pegada
    /// —«Bastion.Api | v1»—: el nombre de un proyecto de C# asomando en el contrato público, que es
    /// justo lo que las URL en minúsculas evitan en las rutas.
    /// </remarks>
    private static Task PonerPortadaYEsquemaDeAcceso(
        OpenApiDocument documento,
        OpenApiDocumentTransformerContext contexto,
        CancellationToken cancelacion)
    {
        documento.Info = new OpenApiInfo
        {
            Title = "Bastion",
            Version = "v1",
            Description =
                "ERP para pyme española. Contrato de la versión 1 de la API, bajo " +
                "/api/v1/{modulo}/{recurso}. Se genera al compilar desde los controladores y los " +
                "DTO: no se escribe a mano, y el cliente del frontal se genera de él.",
        };

        documento.Components ??= new OpenApiComponents();
        documento.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(
            StringComparer.Ordinal);

        documento.Components.SecuritySchemes[EsquemaDelTestigo] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description =
                "Testigo de acceso de quince minutos, en la cabecera Authorization. Lo emite " +
                "POST /api/v1/identidad/sesiones y lo renueva POST .../sesiones/renovacion, que " +
                "no lo lee de aquí sino de una cookie HttpOnly. El testigo de refresco no aparece " +
                "en este contrato a propósito: el navegador no tiene que poder leerlo.",
        };

        return Task.CompletedTask;
    }

    /// <summary>Identificador estable de cada operación, y la descripción prestada fuera.</summary>
    /// <remarks>
    /// <para>
    /// <b>El identificador.</b> Sin él, cada generador de clientes se inventa el nombre del método
    /// a partir de la ruta, y dos generadores se inventan dos nombres distintos. Sale de
    /// controlador y acción, que es el par que ya es único en MVC.
    /// </para>
    /// <para>
    /// <b>La descripción prestada.</b> El cuerpo de toda petición salía descrito como «Cancelación
    /// de la petición en curso»: la herramienta reparte los <c>&lt;param&gt;</c> del comentario
    /// entre los parámetros que ve, y el <c>CancellationToken</c> —que no es ninguno de ellos— se
    /// lleva el último sitio. Una descripción equivocada es peor que ninguna, y el esquema al que
    /// apunta el cuerpo sí lleva la suya. Los parámetros de ruta no están afectados: los suyos
    /// llegan bien.
    /// </para>
    /// </remarks>
    private static Task PonerIdentificadorYQuitarLaDescripcionPrestada(
        OpenApiOperation operacion,
        OpenApiOperationTransformerContext contexto,
        CancellationToken cancelacion)
    {
        if (contexto.Description.ActionDescriptor is ControllerActionDescriptor accion)
        {
            operacion.OperationId = $"{accion.ControllerName}_{accion.ActionName}";
        }

        operacion.RequestBody?.Description = null;

        // Lo anónimo se declara anónimo. Son tres acciones en todo el sistema —abrir, renovar y
        // cerrar sesión— y el contrato tiene que decirlo, porque un cliente generado que mande el
        // testigo al iniciar sesión no falla: simplemente hace algo que nadie ha pensado.
        bool anonima = contexto.Description.ActionDescriptor.EndpointMetadata
            .OfType<IAllowAnonymous>()
            .Any();

        operacion.Security = anonima ? [] : [Exige()];

        return Task.CompletedTask;
    }

    private static OpenApiSecurityRequirement Exige() => new()
    {
        [new OpenApiSecuritySchemeReference(EsquemaDelTestigo)] = [],
    };
}
