using System.Reflection;
using Bastion.Api.FunctionalTests.Salud;
using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.Pruebas.Comun;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Listados;

/// <summary>
/// Cada exportación es hermana de un listado: su ruta más <c>/exportacion</c>, su mismo permiso,
/// sus mismos criterios menos la página, un CSV, y la foto de su propio módulo (ADR-0073).
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo que el contrato dice y el código no obliga.</b> La exportación llama al caso de uso del
/// listado, así que los filtros y lo bloqueado son los suyos por construcción. Lo que no sale solo
/// es lo de alrededor: un <c>[ExigePermiso]</c> copiado de otra acción dejaría descargar entero un
/// listado que no se puede ver, y un <c>[FromQuery]</c> de menos haría que un orden pedido saliera
/// en otro. Esto lo compara con el listado, que es la fuente.
/// </para>
/// <para>
/// <b>Las exportaciones son una lista cerrada</b>, escrita aquí, que crece con cada una. Una nueva
/// la pone roja hasta que se declara, y una que se pierde, también.
/// </para>
/// <para>
/// <b>Un listado sin paginar también tiene exportación</b>, como los códigos de barras de un
/// artículo: su hermano no es una página, y no lee la página de la URL. Esos son otra lista
/// cerrada, la de <see cref="s_sinPaginar"/>, para que un listado paginado no pase por uno sin
/// paginar quitándole la página, ni uno sin paginar se cuele sin decirlo.
/// </para>
/// <para>
/// <b>La foto se resuelve</b> desde un ámbito del host, que es la prueba de construcción que pide
/// el ADR-0073: sin el registro del módulo, la exportación no llega ni a abrirla, y eso solo se
/// vería pidiéndola por HTTP con una base detrás.
/// </para>
/// </remarks>
public sealed class LaExportacionEsHermanaDelListadoTests : IDisposable
{
    private const string TipoDeContenido = "text/csv";

    /// <summary>Lo que el listado lee de la URL y la exportación no, porque las páginas las pone ella.</summary>
    private static readonly string[] s_deLaPagina = ["page", "size"];

    /// <summary>Las exportaciones, con el verbo y la ruta.</summary>
    private static readonly SortedSet<string> s_exportaciones = new(StringComparer.Ordinal)
    {
        "GET /api/v1/catalogo/articulos/exportacion",
        "GET /api/v1/catalogo/articulos/{articuloId}/gtins/exportacion",
        "GET /api/v1/catalogo/categorias/exportacion",
        "GET /api/v1/catalogo/tarifas/exportacion",
        "GET /api/v1/organizacion/almacenes/exportacion",
        "GET /api/v1/organizacion/empresas/exportacion",
        "GET /api/v1/terceros/terceros/exportacion",
    };

    /// <summary>
    /// Los listados que no se paginan y tienen exportación: devuelven la lista entera.
    /// </summary>
    private static readonly SortedSet<string> s_sinPaginar = new(StringComparer.Ordinal)
    {
        "GET /api/v1/catalogo/articulos/{articuloId}/gtins",
    };

    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void Las_exportaciones_son_las_declaradas()
    {
        SortedSet<string> exportaciones = new(
            ExportacionesDeLaApi.De(_api.Services).Select(ListadosDeLaApi.Nombre), StringComparer.Ordinal);

        exportaciones.ShouldBe(
            s_exportaciones,
            customMessage: "las exportaciones no son las declaradas: " + string.Join(", ", exportaciones));
    }

    [Fact]
    public void Cada_exportacion_tiene_el_permiso_y_los_criterios_de_su_listado()
    {
        HashSet<string> paginados = new(
            ListadosDeLaApi.De(_api.Services).Where(ListadosDeLaApi.SeConsulta).Select(ListadosDeLaApi.Nombre),
            StringComparer.Ordinal);
        List<ApiDescription> consultas = [.. Consultas(_api.Services)];
        SortedSet<string> sinPaginar = new(StringComparer.Ordinal);
        List<string> fallos = [];
        int comparadas = 0;

        foreach (ApiDescription exportacion in ExportacionesDeLaApi.De(_api.Services))
        {
            string nombre = ListadosDeLaApi.Nombre(exportacion);
            string ruta = exportacion.RelativePath![..^ExportacionesDeLaApi.Segmento.Length];
            ApiDescription? listado = consultas.SingleOrDefault(
                candidato => string.Equals(candidato.RelativePath, ruta, StringComparison.Ordinal));

            if (listado is null)
            {
                fallos.Add($"{nombre}: no hay un listado en GET /{ruta}");

                continue;
            }

            string hermano = ListadosDeLaApi.Nombre(listado);
            bool paginado = paginados.Contains(hermano);

            if (!paginado && !s_sinPaginar.Contains(hermano))
            {
                fallos.Add($"{nombre}: {hermano} no se pagina, y no está entre los listados sin paginar");

                continue;
            }

            if (paginado && s_sinPaginar.Contains(hermano))
            {
                fallos.Add($"{nombre}: {hermano} está entre los listados sin paginar, y se pagina");
            }

            if (!paginado)
            {
                sinPaginar.Add(hermano);

                if (!DevuelveUnaLista(listado))
                {
                    fallos.Add($"{nombre}: {hermano} no devuelve una lista");
                }
            }

            Permiso? permiso = PermisoDe(exportacion);

            if (permiso is null || permiso != PermisoDe(listado))
            {
                fallos.Add($"{nombre}: exige «{permiso}», y su listado, «{PermisoDe(listado)}»");
            }

            string[] delListado = [.. ListadosDeLaApi.DeLaUrl(listado).Order(StringComparer.Ordinal)];
            string[] deLaExportacion = [.. ListadosDeLaApi.DeLaUrl(exportacion).Order(StringComparer.Ordinal)];

            // EL ARNÉS: que lo que se quita esté en el listado. Si un día la página dejara de
            // llamarse así, la resta no quitaría nada y la exportación tendría que leer `page`. Y su
            // pareja: el que no se pagina no la lee, así que no hay nada que quitarle.
            if (paginado && !s_deLaPagina.All(delListado.Contains))
            {
                fallos.Add($"{nombre}: su listado no lee {string.Join(" ni ", s_deLaPagina)} de la URL");
            }

            if (!paginado && s_deLaPagina.Any(delListado.Contains))
            {
                fallos.Add($"{nombre}: su listado no se pagina, y lee la página de la URL");
            }

            string[] esperados = [.. delListado.Except(s_deLaPagina, StringComparer.Ordinal)];

            if (!deLaExportacion.SequenceEqual(esperados, StringComparer.Ordinal))
            {
                fallos.Add(
                    $"{nombre}: lee [{string.Join(", ", deLaExportacion)}] de la URL, y su listado, " +
                    $"sin la página, [{string.Join(", ", esperados)}]");
            }

            bool esCsv = exportacion.SupportedResponseTypes.Any(
                respuesta => respuesta.StatusCode == 200
                    && respuesta.ApiResponseFormats.Any(
                        formato => string.Equals(formato.MediaType, TipoDeContenido, StringComparison.Ordinal)));

            if (!esCsv)
            {
                fallos.Add($"{nombre}: su 200 no publica {TipoDeContenido}");
            }

            comparadas++;
        }

        fallos.ShouldBeEmpty(string.Join(Environment.NewLine, fallos));
        comparadas.ShouldBe(s_exportaciones.Count, "el arnés: se han comparado todas las declaradas");
        sinPaginar.ShouldBe(s_sinPaginar, "los listados sin paginar declarados son los hermanos de una exportación");
    }

    /// <summary>
    /// La foto que pide cada exportación es la de su módulo, declarada en su aplicación, y el host
    /// la construye con una implementación de su infraestructura.
    /// </summary>
    [Fact]
    public void Cada_exportacion_pide_la_foto_de_su_modulo_y_el_host_la_construye()
    {
        using IServiceScope ambito = _api.Services.CreateScope();
        List<string> fallos = [];
        int resueltas = 0;

        foreach (ApiDescription exportacion in ExportacionesDeLaApi.De(_api.Services))
        {
            string nombre = ListadosDeLaApi.Nombre(exportacion);
            ControllerActionDescriptor accion = exportacion.ActionDescriptor.ShouldBeOfType<ControllerActionDescriptor>();
            string modulo = Modulo(accion.ControllerTypeInfo.Assembly, ".Endpoints");
            Type[] fotos = ExportacionesDeLaApi.Fotos(exportacion);

            if (fotos is not [Type foto])
            {
                fallos.Add($"{nombre}: pide {fotos.Length} fotos, y es una");

                continue;
            }

            if (foto == typeof(IFotoDeLectura) || !foto.IsInterface
                || !string.Equals(foto.Assembly.GetName().Name, $"{modulo}.Application", StringComparison.Ordinal))
            {
                fallos.Add($"{nombre}: pide {foto.FullName}, que no es la foto que declara {modulo}.Application");

                continue;
            }

            object construida = ambito.ServiceProvider.GetRequiredService(foto);
            string? deDonde = construida.GetType().Assembly.GetName().Name;

            if (!string.Equals(deDonde, $"{modulo}.Infrastructure", StringComparison.Ordinal))
            {
                fallos.Add($"{nombre}: su foto la implementa {deDonde}, y no {modulo}.Infrastructure");
            }

            resueltas++;
        }

        fallos.ShouldBeEmpty(string.Join(Environment.NewLine, fallos));
        resueltas.ShouldBe(s_exportaciones.Count, "el arnés: se han resuelto las fotos de todas las declaradas");
    }

    /// <summary>
    /// Todas las consultas de la API, y no solo los listados paginados: el hermano de una
    /// exportación puede devolver la lista entera.
    /// </summary>
    private static IEnumerable<ApiDescription> Consultas(IServiceProvider servicios) =>
        from grupo in servicios.GetRequiredService<IApiDescriptionGroupCollectionProvider>().ApiDescriptionGroups.Items
        from descripcion in grupo.Items
        where ListadosDeLaApi.SeConsulta(descripcion)
        select descripcion;

    /// <summary>
    /// Si el <c>200</c> de una consulta es una lista: una colección genérica, que no es una página.
    /// </summary>
    private static bool DevuelveUnaLista(ApiDescription consulta) =>
        consulta.SupportedResponseTypes.Any(
            respuesta => respuesta.StatusCode == 200
                && respuesta.Type is { IsGenericType: true } tipo
                && typeof(System.Collections.IEnumerable).IsAssignableFrom(tipo)
                && !ListadosDeLaApi.EsColeccion(tipo));

    private static Permiso? PermisoDe(ApiDescription accion) =>
        accion.ActionDescriptor.EndpointMetadata.OfType<ExigePermisoAttribute>().FirstOrDefault()?.Permiso;

    /// <summary>El módulo de un ensamblado, como <c>Bastion.Organizacion</c>, sin su capa.</summary>
    private static string Modulo(Assembly ensamblado, string capa)
    {
        string nombre = ensamblado.GetName().Name!;

        nombre.ShouldEndWith(capa, customMessage: $"el arnés: {nombre} no es una capa {capa}");

        return nombre[..^capa.Length];
    }
}
