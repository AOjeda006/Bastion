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
            opciones.AddDocumentTransformer(QuitarLosRetornosDeCarroDeLosEsquemas);
        });

    /// <summary>El mismo texto se genere donde se genere: sin retornos de carro.</summary>
    /// <remarks>
    /// <para>
    /// Las descripciones salen de los comentarios de documentación, y el compilador escribe el
    /// fichero XML con el salto de línea de la PLATAFORMA —no con el del fichero fuente, que en
    /// este repositorio es LF en todas partes—. Un mismo comentario de tres líneas entra en el
    /// documento como <c>\r\n</c> generado en Windows y como <c>\n</c> generado en Linux.
    /// </para>
    /// <para>
    /// Eso hace que el documento no sea reproducible, y un artefacto versionado que la CI vuelve a
    /// generar TIENE que serlo: si no, el paso de comprobación se pone rojo por el sistema
    /// operativo de quien lo generó y no por un cambio de contrato. Se normaliza aquí, en el punto
    /// donde se produce, y no en el fichero después: el que arregla la salida en vez de la causa
    /// deja el problema esperando al siguiente campo que se añada.
    /// </para>
    /// </remarks>
    /// <param name="texto">Descripción tal como llega del fichero XML, o <c>null</c>.</param>
    private static string? SinRetornosDeCarro(string? texto) =>
        texto?.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

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

        operacion.Summary = SinRetornosDeCarro(operacion.Summary);
        operacion.Description = SinRetornosDeCarro(operacion.Description);

        foreach (IOpenApiParameter parametro in operacion.Parameters ?? [])
        {
            if (parametro is OpenApiParameter propio)
            {
                propio.Description = SinRetornosDeCarro(propio.Description);
            }
        }

        foreach (IOpenApiResponse respuesta in
            operacion.Responses?.Values ?? Enumerable.Empty<IOpenApiResponse>())
        {
            if (respuesta is OpenApiResponse propia)
            {
                propia.Description = SinRetornosDeCarro(propia.Description);
            }
        }

        // Lo anónimo se declara anónimo. Son tres acciones en todo el sistema —abrir, renovar y
        // cerrar sesión— y el contrato tiene que decirlo, porque un cliente generado que mande el
        // testigo al iniciar sesión no falla: simplemente hace algo que nadie ha pensado.
        bool anonima = contexto.Description.ActionDescriptor.EndpointMetadata
            .OfType<IAllowAnonymous>()
            .Any();

        operacion.Security = anonima ? [] : [Exige()];

        return Task.CompletedTask;
    }

    /// <summary>Lo mismo para los esquemas, que traen la descripción de cada DTO y de cada campo.</summary>
    /// <remarks>
    /// <para>
    /// Va como transformador de DOCUMENTO y no de esquema, aunque lo que toque sean esquemas. Un
    /// transformador de esquema ve el esquema del TIPO mientras se construye, y ahí la propiedad
    /// que estorba todavía no tiene la forma final: probado, deja pasar el caso. Al documento se
    /// llega con todo montado.
    /// </para>
    /// <para>
    /// Hay que bajar hasta las propiedades —eso lo enseñó el guardián de
    /// <c>scripts/generar-openapi.sh</c>, que dejó pasar catorce sitios y paró en el decimoquinto—
    /// y también por los <c>oneOf</c>: una propiedad opcional que apunta a otro DTO sale como
    /// «nulo o esta referencia», y la descripción se queda colgada de la referencia, que no es un
    /// <see cref="OpenApiSchema"/>.
    /// </para>
    /// <para>
    /// Solo se escribe donde hay algo que arreglar. Leer la descripción de una referencia puede
    /// devolver la del tipo al que apunta; asignarla sin mirar la clavaría como descripción propia
    /// en cada sitio donde ese tipo se usa, y eso sí cambiaría el documento.
    /// </para>
    /// </remarks>
    /// <param name="documento">Documento ya montado.</param>
    /// <param name="contexto">De dónde salió; no se usa.</param>
    /// <param name="cancelacion">Cancelación de la generación.</param>
    private static Task QuitarLosRetornosDeCarroDeLosEsquemas(
        OpenApiDocument documento,
        OpenApiDocumentTransformerContext contexto,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(documento);

        foreach (IOpenApiSchema esquema in documento.Components?.Schemas?.Values ??
            Enumerable.Empty<IOpenApiSchema>())
        {
            Normalizar(esquema);
        }

        return Task.CompletedTask;

        static void Normalizar(IOpenApiSchema nodo)
        {
            if (nodo.Description is string descripcion &&
                descripcion.Contains('\r', StringComparison.Ordinal))
            {
                switch (nodo)
                {
                    case OpenApiSchema propio:
                        propio.Description = SinRetornosDeCarro(descripcion);
                        break;

                    case OpenApiSchemaReference referencia:
                        referencia.Description = SinRetornosDeCarro(descripcion);
                        break;

                    default:
                        break;
                }
            }

            // Por debajo de una referencia no se sigue: lo que hay al otro lado es un esquema del
            // catálogo, y a ese lo visita el transformador por su cuenta. Es también lo que impide
            // que un DTO que se contiene a sí mismo mande el recorrido a dar vueltas.
            if (nodo is not OpenApiSchema rama)
            {
                return;
            }

            IEnumerable<IOpenApiSchema> hijos =
            [
                .. rama.Properties?.Values ?? Enumerable.Empty<IOpenApiSchema>(),
                .. rama.OneOf ?? Enumerable.Empty<IOpenApiSchema>(),
                .. rama.AnyOf ?? Enumerable.Empty<IOpenApiSchema>(),
                .. rama.AllOf ?? Enumerable.Empty<IOpenApiSchema>(),
                .. rama.Items is null ? [] : new[] { rama.Items },
            ];

            foreach (IOpenApiSchema hijo in hijos)
            {
                Normalizar(hijo);
            }
        }
    }

    private static OpenApiSecurityRequirement Exige() => new()
    {
        [new OpenApiSecuritySchemeReference(EsquemaDelTestigo)] = [],
    };
}
