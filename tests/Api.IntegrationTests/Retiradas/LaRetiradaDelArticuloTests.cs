using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Exportacion;
using Bastion.Api.IntegrationTests.Inventario;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Catalogo.Contracts;
using Bastion.Catalogo.Contracts.Catalogo;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Retiradas;

/// <summary>
/// El artículo se retira por HTTP como los cuatro maestros de instalación: dos puertas con su
/// versión y su permiso, el <c>GET</c> que lo sigue devolviendo, y un listado que no lo trae salvo
/// que se le pida (ADR-0082).
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo que aquí se prueba es la ficha y su listado, no lo que deja de admitir.</b> Qué rechaza
/// un artículo retirado lo dice cada operación nueva con su <c>409</c>, y se prueba con cada una.
/// </para>
/// <para>
/// <b>Semillas: del 970 al 976</b>, el bloque del 3.6, que va del 970 al 978. Son empresas y, con
/// el mismo número, la unidad y el tramo de impuesto del artículo, que son de instalación.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaRetiradaDelArticuloTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Articulos = "/api/v1/catalogo/articulos";

    private readonly ApiDeVerdad _api = new(postgres);

    /// <inheritdoc/>
    public void Dispose() => _api.Dispose();

    /// <summary>
    /// Retirado, el <c>GET</c> lo sigue devolviendo y lo dice; reincorporado, vuelve a decir que no.
    /// </summary>
    [Fact]
    public async Task El_GET_sigue_devolviendo_el_articulo_retirado_y_lo_dice()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(970));
        using HttpClient suyo = cliente;

        (Guid id, _) = await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, 970);

        (await LeerAsync(cliente, id)).Retirada.ShouldBeFalse(
            "recién dado de alta se ofrece: sin esta mitad, un `Retirada` cableado a `true` " +
            "pasaría la de abajo");

        await PuertaAsync(cliente, id, HttpMethod.Post);

        ArticuloDto retirado = await LeerAsync(cliente, id);

        retirado.Retirada.ShouldBeTrue(
            "y lo dice: sin el campo, quien lo lee no distingue un artículo retirado de uno que " +
            "se ofrece");
        retirado.Codigo.ShouldBe("APT-970", "es la misma ficha, y retirar no le toca nada más");
        retirado.Descripcion.ShouldBe("Artículo 970");

        await PuertaAsync(cliente, id, HttpMethod.Delete);

        (await LeerAsync(cliente, id)).Retirada.ShouldBeFalse(
            "reincorporar lo vuelve a ofrecer, sobre la misma fila");
    }

    /// <summary>
    /// El listado no trae al retirado por omisión, y lo trae con <c>?retiradas=true</c>; la
    /// exportación sigue al listado, con la columna que lo distingue.
    /// </summary>
    /// <remarks>
    /// Las dos mitades en el mismo caso y sobre la misma fila: un listado que devolviera siempre
    /// todo pasaría la segunda, y uno que no devolviera nunca lo retirado, la primera.
    /// </remarks>
    [Fact]
    public async Task El_listado_y_la_exportacion_lo_dejan_fuera_salvo_que_se_pida()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(971));
        using HttpClient suyo = cliente;

        (Guid queda, _) = await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, 971);
        (Guid retirado, _) = await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, 972);

        PaginaDe<ArticuloDto> antes = await ListarAsync(cliente, string.Empty);
        antes.Elementos.Select(articulo => articulo.Id).ShouldBe(
            [queda, retirado], "antes de retirarlo sale, que es lo que da sentido a su ausencia");

        await PuertaAsync(cliente, retirado, HttpMethod.Post);

        PaginaDe<ArticuloDto> sinPedirlo = await ListarAsync(cliente, string.Empty);
        sinPedirlo.Elementos.Select(articulo => articulo.Id).ShouldBe(
            [queda], "por omisión el listado no ofrece lo retirado: es la lista con la que se elige");
        sinPedirlo.Total.ShouldBe(1, "y el total es el de lo que se ofrece, contado después del filtro");

        PaginaDe<ArticuloDto> pidiendolo = await ListarAsync(cliente, "?retiradas=true");
        pidiendolo.Elementos.Select(articulo => (articulo.Id, articulo.Retirada)).ShouldBe(
            [(queda, false), (retirado, true)], "pedido, sale, y con su marca");

        const string Cabecera =
            "codigo;descripcion;tipo;trazabilidad;retirada;unidad_base_codigo;unidad_base_nombre;" +
            "impuesto_por_defecto_codigo;impuesto_por_defecto_nombre;categoria_codigo;categoria_nombre\r\n";
        const string LineaDelQueQueda =
            "APT-971;Artículo 971;Bien;Ninguna;no;W971;Unidad 971;ART971;Tramo ART971;;\r\n";
        const string LineaDelRetirado =
            "APT-972;Artículo 972;Bien;Ninguna;sí;W972;Unidad 972;ART972;Tramo ART972;;\r\n";

        (await Exportaciones.ExportarAsync(cliente, Articulos, "articulos", string.Empty)).ShouldBe(
            Cabecera + LineaDelQueQueda, "la exportación es el listado, también en esto");
        (await Exportaciones.ExportarAsync(cliente, Articulos, "articulos", "?retiradas=true")).ShouldBe(
            Cabecera + LineaDelQueQueda + LineaDelRetirado);
    }

    /// <summary>Las dos puertas exigen la versión, y la retirada la cambia.</summary>
    [Fact]
    public async Task Las_dos_puertas_exigen_la_version_y_retirar_la_cambia()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(973));
        using HttpClient suyo = cliente;

        (Guid id, _) = await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, 973);
        string puerta = $"{Articulos}/{id}/retirada";

        using HttpResponseMessage sinVersion = await cliente.EnviarConVersionAsync(
            HttpMethod.Post, puerta, etiqueta: null);

        sinVersion.StatusCode.ShouldBe(
            HttpStatusCode.PreconditionRequired, await Escenario.Detalle(sinVersion));

        string vieja = await cliente.EtiquetaDeAsync($"{Articulos}/{id}");

        using HttpResponseMessage retirada = await cliente.EnviarConVersionAsync(
            HttpMethod.Post, puerta, vieja);

        retirada.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(retirada));

        using HttpResponseMessage conLaVieja = await cliente.EnviarConVersionAsync(
            HttpMethod.Delete, puerta, vieja);

        conLaVieja.StatusCode.ShouldBe(
            HttpStatusCode.PreconditionFailed,
            "la retirada es una escritura sobre la ficha y cambia su versión: quien reincorpora " +
            "con la que leyó antes no ha visto la retirada. " + await Escenario.Detalle(conLaVieja));

        (await LeerAsync(cliente, id)).Retirada.ShouldBeTrue("el 412 no ha tocado nada");
    }

    /// <summary>Cada puerta tiene su permiso, y modificar la ficha no abre ninguna.</summary>
    [Fact]
    public async Task Cada_puerta_tiene_su_permiso()
    {
        // En la empresa de la semilla, que es en la que ConPermisosAsync da los permisos.
        using HttpClient administrador = await _api.ComoAdministradorAsync();

        (Guid id, _) = await LosMaestrosPorLaApi.CrearArticuloAsync(administrador, 974);

        using HttpClient quienModifica = await _api.ConPermisosAsync(
            PermisosDeCatalogo.ArticuloVer, PermisosDeCatalogo.ArticuloModificar);
        using HttpClient quienRetira = await _api.ConPermisosAsync(
            PermisosDeCatalogo.ArticuloVer, PermisosDeCatalogo.ArticuloRetirar);
        using HttpClient quienReincorpora = await _api.ConPermisosAsync(
            PermisosDeCatalogo.ArticuloVer, PermisosDeCatalogo.ArticuloReincorporar);

        (await IntentarAsync(quienModifica, id, HttpMethod.Post)).ShouldBe(
            HttpStatusCode.Forbidden, "corregir la ficha no es decidir que deja de ofrecerse");
        (await IntentarAsync(quienReincorpora, id, HttpMethod.Post)).ShouldBe(
            HttpStatusCode.Forbidden, "deshacer una retirada no es hacerla");
        (await IntentarAsync(quienRetira, id, HttpMethod.Post)).ShouldBe(HttpStatusCode.NoContent);

        (await IntentarAsync(quienRetira, id, HttpMethod.Delete)).ShouldBe(
            HttpStatusCode.Forbidden, "y hacerla no es deshacerla");
        (await IntentarAsync(quienReincorpora, id, HttpMethod.Delete)).ShouldBe(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// El artículo de otra empresa no se retira desde esta: contesta lo mismo que uno que no
    /// existe (R8).
    /// </summary>
    [Fact]
    public async Task El_articulo_de_otra_empresa_contesta_como_uno_que_no_existe()
    {
        (HttpClient suya, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(975));
        using HttpClient deLaSuya = suya;
        (HttpClient otra, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(976));
        using HttpClient deLaOtra = otra;

        (Guid id, _) = await LosMaestrosPorLaApi.CrearArticuloAsync(suya, 975);
        string etiqueta = await suya.EtiquetaDeAsync($"{Articulos}/{id}");

        using HttpResponseMessage ajena = await otra.EnviarConVersionAsync(
            HttpMethod.Post, $"{Articulos}/{id}/retirada", etiqueta);

        ajena.StatusCode.ShouldBe(
            HttpStatusCode.NotFound,
            "el filtro de la empresa lo deja fuera antes de mirar nada, y con la versión buena: " +
            "un 412 diría que existe. " + await Escenario.Detalle(ajena));

        using HttpResponseMessage inexistente = await suya.EnviarConVersionAsync(
            HttpMethod.Post, $"{Articulos}/{Guid.NewGuid()}/retirada", etiqueta);

        inexistente.StatusCode.ShouldBe(HttpStatusCode.NotFound, await Escenario.Detalle(inexistente));

        (await LeerAsync(suya, id)).Retirada.ShouldBeFalse("y en la suya sigue ofreciéndose");
    }

    private static async Task<ArticuloDto> LeerAsync(HttpClient cliente, Guid id)
    {
        using HttpResponseMessage lectura = await cliente.GetAsync($"{Articulos}/{id}");

        lectura.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(lectura));

        return (await lectura.Content.ReadFromJsonAsync<ArticuloDto>())!;
    }

    private static async Task<PaginaDe<ArticuloDto>> ListarAsync(HttpClient cliente, string consulta)
    {
        using HttpResponseMessage respuesta = await cliente.GetAsync(Articulos + consulta);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(respuesta));

        return (await respuesta.Content.ReadFromJsonAsync<PaginaDe<ArticuloDto>>())!;
    }

    private static async Task PuertaAsync(HttpClient cliente, Guid id, HttpMethod metodo)
    {
        using HttpResponseMessage respuesta = await cliente.AccionarAsync(
            $"{Articulos}/{id}", $"{Articulos}/{id}/retirada", metodo);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(respuesta));
    }

    private static async Task<HttpStatusCode> IntentarAsync(HttpClient cliente, Guid id, HttpMethod metodo)
    {
        using HttpResponseMessage respuesta = await cliente.AccionarAsync(
            $"{Articulos}/{id}", $"{Articulos}/{id}/retirada", metodo);

        return respuesta.StatusCode;
    }
}
