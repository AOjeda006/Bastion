using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Impuestos;
using Bastion.Organizacion.Contracts.Unidades;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Contrato;

/// <summary>
/// Los códigos de barras del artículo por la API y contra PostgreSQL (ítem 2.10, ADR-0051).
/// </summary>
/// <remarks>
/// <para>
/// <b>Aquí está lo que solo se ve con el motor delante.</b> Que el GTIN se guarde en catorce y la
/// consulta compare esa forma, así que un GTIN-12 y su GTIN-13 chocan. Que el filtro de empresa y el
/// índice por <c>(empresa_id, gtin)</c> dejen a otra llevar el mismo número. Que la baja exija su
/// versión y borre de verdad, y que después el número vuelva a estar libre. Y que el reintento con
/// la misma clave no se convierta en un <c>409</c> de sí mismo.
/// </para>
/// <para>
/// <b>Ningún caso de aquí choca contra el índice único.</b> Las altas van de una en una, así que el
/// <c>409</c> lo da siempre la comprobación previa del caso de uso. El índice y la traducción de su
/// nombre solo se alcanzan con dos altas a la vez, y eso es un caso de carrera, no de contrato.
/// </para>
/// <para>
/// <b>Lo que decide el caso de uso sin conexión</b> —el orden de las preguntas, el nivel por nombre,
/// las unidades que se suponen— está en <c>CodigosBarrasDelArticuloTests</c>. Aquí cada motivo se
/// recorre una vez para ver que su <c>type</c> llega al cuerpo, no para volver a probar el dominio.
/// </para>
/// <para>
/// <b>Las semillas son el bloque 630-639</b>: las empresas y, con el mismo número, la unidad y el
/// tramo de impuesto de cada artículo, que son maestros de instalación. Del 624 al 699 no había
/// ninguna, ni por literal ni por cálculo: la más alta calculada es la de
/// <c>ElSaldoEsLaSumaDelLibroPorPropiedadTests</c>, <c>465 + 130 = 595</c>. El prefijo del tramo es
/// <c>GTN</c>, que no usa ningún otro fichero.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ContratoDelCodigoDeBarrasTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Articulos = "/api/v1/catalogo/articulos";
    private const string Gtins = Articulos + "/gtins";
    private const string Unidades = "/api/v1/organizacion/unidades-de-medida";
    private const string Impuestos = "/api/v1/organizacion/impuestos";

    private const string Duplicado = "/errors/codigo-barras-duplicado";

    private static readonly DateOnly s_desde = new(2000, 1, 1);

    private readonly ApiDeVerdad _api = new(postgres);
    private readonly List<HttpClient> _clientes = [];

    public void Dispose()
    {
        foreach (HttpClient cliente in _clientes)
        {
            cliente.Dispose();
        }

        _api.Dispose();
    }

    /// <summary>
    /// El alta contesta 201 con el GTIN en catorce, su <c>Location</c> lleva a él con su
    /// <c>ETag</c>, y el listado del artículo trae las bases antes que las cajas.
    /// </summary>
    /// <remarks>
    /// Dos de cada nivel, que es lo que contestó la puerta del 2.10 (ADR-0051 §3): ni el nivel ni las
    /// unidades son únicos por artículo. Dentro de un nivel, el orden es el del GTIN.
    /// </remarks>
    [Fact]
    public async Task Un_gtin_se_da_de_alta_con_201_en_catorce_cifras_y_el_listado_lo_trae()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(630);
        ArticuloDto articulo = await ArticuloAsync(cliente, 630, "A");

        using HttpResponseMessage caja = await AgregarAsync(cliente, articulo.Id, "10012345678902", "Caja", 12);
        caja.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(caja));

        using HttpResponseMessage alta = await AgregarAsync(cliente, articulo.Id, "4006381333931", "Base");
        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        CodigoBarrasDto codigo = (await alta.Content.ReadFromJsonAsync<CodigoBarrasDto>())!;

        codigo.Gtin.ShouldBe("04006381333931", "se guarda y sale en su forma de catorce");
        codigo.Nivel.ShouldBe("Base");
        codigo.Unidades.ShouldBe(1, "sin unidades, la base lleva la suya");
        codigo.ArticuloId.ShouldBe(articulo.Id);

        using HttpResponseMessage otraBase = await AgregarAsync(cliente, articulo.Id, "036000291452", "Base");
        otraBase.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(otraBase));

        using HttpResponseMessage otraCaja = await AgregarAsync(cliente, articulo.Id, "10036000291459", "Caja", 6);
        otraCaja.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(otraCaja));

        alta.Headers.Location.ShouldNotBeNull();

        using HttpResponseMessage porSuLocation = await cliente.GetAsync(alta.Headers.Location);

        porSuLocation.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(porSuLocation));
        porSuLocation.Headers.ETag.ShouldNotBeNull("la baja exige If-Match, y la versión sale de aquí");
        (await porSuLocation.Content.ReadFromJsonAsync<CodigoBarrasDto>())!.Id.ShouldBe(codigo.Id);

        (await ListadoAsync(cliente, articulo.Id)).Select(uno => (uno.Gtin, uno.Nivel, uno.Unidades))
            .ShouldBe(
                [
                    ("00036000291452", "Base", 1),
                    ("04006381333931", "Base", 1),
                    ("10036000291459", "Caja", 6),
                    ("10012345678902", "Caja", 12),
                ],
                "de la base a las agrupaciones, por unidades y después por GTIN");
    }

    /// <summary>
    /// El GTIN-12 y su forma de trece son el mismo número: el segundo, en otro artículo de la
    /// empresa, es 409, y la búsqueda encuentra el primero escrito de cualquiera de las dos formas.
    /// </summary>
    /// <remarks>
    /// <b>En OTRO artículo</b>: un GTIN identifica una sola cosa en la empresa (ADR-0051 §5), así que
    /// una comprobación por <c>(articulo_id, gtin)</c> dejaría pasar justo este caso. El <c>409</c> lo
    /// da la comprobación previa, que lee la forma de catorce que guardó el motor; el índice no llega
    /// a verlo.
    /// </remarks>
    [Fact]
    public async Task El_gtin12_y_su_forma_de_13_chocan_en_otro_articulo_y_la_busqueda_los_iguala()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(631);
        ArticuloDto primero = await ArticuloAsync(cliente, 631, "A");
        ArticuloDto segundo = await ArticuloAsync(cliente, 631, "B");

        using HttpResponseMessage alta = await AgregarAsync(cliente, primero.Id, "036000291452", "Base");
        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        using HttpResponseMessage otra = await AgregarAsync(cliente, segundo.Id, "0036000291452", "Base");

        otra.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(otra));
        (await TypeDe(otra)).ShouldBe(Duplicado);
        (await ListadoAsync(cliente, segundo.Id)).ShouldBeEmpty("un alta rechazada ha dejado fila");

        foreach (string escrito in (string[])["036000291452", "0036000291452", "00036000291452"])
        {
            (await BuscarAsync(cliente, escrito)).ShouldHaveSingleItem($"buscando {escrito}")
                .ArticuloId.ShouldBe(primero.Id);
        }
    }

    /// <summary>
    /// Cada motivo de rechazo llega al cuerpo con su <c>type</c>, el nivel y las unidades con los
    /// suyos, y ninguno deja fila.
    /// </summary>
    [Fact]
    public async Task Cada_rechazo_es_un_400_con_su_type_y_no_deja_fila()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(632);
        ArticuloDto articulo = await ArticuloAsync(cliente, 632, "A");

        foreach ((string? gtin, string nivel, int? unidades, string type) in
            (IEnumerable<(string?, string, int?, string)>)
            [
                ("4006-381333931", "Base", null, "gtin-no-son-digitos"),
                (null, "Base", null, "gtin-largo-no-admitido"),
                ("36000291452", "Base", null, "gtin-largo-no-admitido"),
                ("4006381333932", "Base", null, "gtin-digito-de-control"),
                ("2012345678903", "Base", null, "gtin-circulacion-restringida"),
                ("98412345678908", "Base", null, "gtin-medida-variable"),
                ("9801234567892", "Base", null, "gtin-cupon"),
                ("9511234567890", "Base", null, "gtin-sin-asignar"),
                ("4006381333931", "2", 12, "codigo-barras-nivel-no-valido"),
                ("4006381333931", "Caja", null, "codigo-barras-unidades-no-validas"),
                ("4006381333931", "Base", 6, "codigo-barras-unidades-no-validas"),
            ])
        {
            using HttpResponseMessage alta = await AgregarAsync(cliente, articulo.Id, gtin, nivel, unidades);

            alta.StatusCode.ShouldBe(
                HttpStatusCode.BadRequest,
                $"«{gtin}» como {nivel} con {unidades} no es 400. {await Escenario.Detalle(alta)}");
            (await TypeDe(alta)).ShouldBe("/errors/" + type, $"«{gtin}» como {nivel} con {unidades}");
        }

        (await ListadoAsync(cliente, articulo.Id)).ShouldBeEmpty("un alta rechazada ha dejado fila");

        using HttpResponseMessage deVerdad = await AgregarAsync(cliente, articulo.Id, "4006381333931", "Base");

        deVerdad.StatusCode.ShouldBe(
            HttpStatusCode.Created,
            "el artículo rechaza también un GTIN bueno, así que los 400 de arriba no dicen nada. " +
            await Escenario.Detalle(deVerdad));
    }

    /// <summary>
    /// Otra empresa lleva el mismo GTIN sin chocar, cada una encuentra el suyo, y ninguna puede
    /// colgarlo en un artículo de la otra.
    /// </summary>
    /// <remarks>
    /// El índice es por <c>(empresa_id, gtin)</c> y el filtro de empresa va en el contexto (R8): un
    /// GTIN lo asigna el dueño de la marca, y dos clientes de esta instalación pueden vender lo
    /// mismo.
    /// </remarks>
    [Fact]
    public async Task Otra_empresa_lleva_el_mismo_gtin_y_cada_una_ve_solo_el_suyo()
    {
        HttpClient enUna = await EnUnaEmpresaNuevaAsync(633);
        HttpClient enOtra = await EnUnaEmpresaNuevaAsync(634);
        ArticuloDto deUna = await ArticuloAsync(enUna, 633, "A");
        ArticuloDto deOtra = await ArticuloAsync(enOtra, 634, "A");

        using HttpResponseMessage enLaPrimera = await AgregarAsync(enUna, deUna.Id, "4006381333931", "Base");
        enLaPrimera.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(enLaPrimera));

        using HttpResponseMessage enLaSegunda = await AgregarAsync(enOtra, deOtra.Id, "4006381333931", "Base");
        enLaSegunda.StatusCode.ShouldBe(
            HttpStatusCode.Created,
            "el mismo GTIN en otra empresa no es un duplicado. " + await Escenario.Detalle(enLaSegunda));

        (await BuscarAsync(enUna, "4006381333931")).ShouldHaveSingleItem().ArticuloId.ShouldBe(deUna.Id);
        (await BuscarAsync(enOtra, "4006381333931")).ShouldHaveSingleItem().ArticuloId.ShouldBe(deOtra.Id);

        using HttpResponseMessage cruzado = await AgregarAsync(enUna, deOtra.Id, "036000291452", "Base");

        cruzado.StatusCode.ShouldBe(
            HttpStatusCode.NotFound,
            "el artículo de otra empresa no existe para esta. " + await Escenario.Detalle(cruzado));
        (await TypeDe(cruzado)).ShouldBe("/errors/articulo-no-encontrado");
    }

    /// <summary>
    /// La baja sin <c>If-Match</c> es 428, con una versión que no es la suya 412, con la suya 204, y
    /// otra vez 404.
    /// </summary>
    /// <remarks>
    /// La fila no cambia nunca, así que una versión vieja de verdad no existe: la que se cita es
    /// inventada, y el caso afirma antes que no es la que emite el <c>GET</c>.
    /// </remarks>
    [Fact]
    public async Task La_baja_exige_su_version_borra_la_fila_y_la_segunda_es_404()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(635);
        ArticuloDto articulo = await ArticuloAsync(cliente, 635, "A");
        CodigoBarrasDto codigo = await AgregadoAsync(cliente, articulo.Id, "4006381333931");
        string recurso = $"{Gtins}/{codigo.Id}";

        using HttpResponseMessage sinVersion = await cliente.EnviarConVersionAsync(
            HttpMethod.Delete, recurso, etiqueta: null);

        sinVersion.StatusCode.ShouldBe((HttpStatusCode)428, await Escenario.Detalle(sinVersion));

        string etiqueta = await cliente.EtiquetaDeAsync(recurso);
        etiqueta.ShouldNotBe("\"1\"");

        using HttpResponseMessage conOtra = await cliente.EnviarConVersionAsync(
            HttpMethod.Delete, recurso, "\"1\"");

        conOtra.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed, await Escenario.Detalle(conOtra));
        (await ListadoAsync(cliente, articulo.Id)).ShouldHaveSingleItem("un 412 ha borrado la fila");

        using HttpResponseMessage conLaSuya = await cliente.EnviarConVersionAsync(
            HttpMethod.Delete, recurso, etiqueta);

        conLaSuya.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(conLaSuya));
        (await ListadoAsync(cliente, articulo.Id)).ShouldBeEmpty();
        (await BuscarAsync(cliente, "4006381333931")).ShouldBeEmpty();

        using HttpResponseMessage otraVez = await cliente.EnviarConVersionAsync(
            HttpMethod.Delete, recurso, etiqueta);

        otraVez.StatusCode.ShouldBe(HttpStatusCode.NotFound, await Escenario.Detalle(otraVez));
        (await TypeDe(otraVez)).ShouldBe("/errors/codigo-barras-no-encontrado");
    }

    /// <summary>
    /// Quitado de un artículo, el GTIN se puede dar de alta enseguida en otro.
    /// </summary>
    /// <remarks>
    /// La regla de los 48 meses antes de reutilizar un GTIN está derogada (ADR-0051 §8), y lo que
    /// este caso afirma es que la baja borra de verdad: un borrado lógico dejaría la fila en el
    /// índice único, y la segunda alta sería un 409.
    /// </remarks>
    [Fact]
    public async Task Tras_la_baja_el_mismo_gtin_se_da_de_alta_en_otro_articulo()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(636);
        ArticuloDto primero = await ArticuloAsync(cliente, 636, "A");
        ArticuloDto segundo = await ArticuloAsync(cliente, 636, "B");
        CodigoBarrasDto codigo = await AgregadoAsync(cliente, primero.Id, "4006381333931");

        using HttpResponseMessage baja = await cliente.SuprimirAsync($"{Gtins}/{codigo.Id}");
        baja.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(baja));

        using HttpResponseMessage otra = await AgregarAsync(cliente, segundo.Id, "4006381333931", "Base");

        otra.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(otra));
        (await BuscarAsync(cliente, "04006381333931")).ShouldHaveSingleItem().ArticuloId.ShouldBe(segundo.Id);
    }

    /// <summary>
    /// Las lecturas que no encuentran: el listado de un artículo que no existe es 404, la búsqueda
    /// de lo que no es un GTIN es 400, y la de un GTIN que nadie lleva, una lista vacía.
    /// </summary>
    [Fact]
    public async Task Las_lecturas_distinguen_lo_que_no_existe_de_lo_que_no_es_un_gtin()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(637);

        using HttpResponseMessage listado = await cliente.GetAsync($"{Articulos}/{Guid.CreateVersion7()}/gtins");

        listado.StatusCode.ShouldBe(HttpStatusCode.NotFound, await Escenario.Detalle(listado));
        (await TypeDe(listado)).ShouldBe("/errors/articulo-no-encontrado");

        using HttpResponseMessage uno = await cliente.GetAsync($"{Gtins}/{Guid.CreateVersion7()}");

        uno.StatusCode.ShouldBe(HttpStatusCode.NotFound, await Escenario.Detalle(uno));
        (await TypeDe(uno)).ShouldBe("/errors/codigo-barras-no-encontrado");

        foreach ((string ruta, string type) in (IEnumerable<(string, string)>)
            [
                (Gtins, "gtin-largo-no-admitido"),
                ($"{Gtins}?gtin=40063813339A1", "gtin-no-son-digitos"),
                ($"{Gtins}?gtin=4006381333932", "gtin-digito-de-control"),
            ])
        {
            using HttpResponseMessage busqueda = await cliente.GetAsync(ruta);

            busqueda.StatusCode.ShouldBe(HttpStatusCode.BadRequest, $"{ruta}. {await Escenario.Detalle(busqueda)}");
            (await TypeDe(busqueda)).ShouldBe("/errors/" + type, ruta);
        }

        (await BuscarAsync(cliente, "4006381333931")).ShouldBeEmpty();
    }

    /// <summary>
    /// El reintento con la misma clave devuelve la misma respuesta, y no un 409 de sí mismo.
    /// </summary>
    /// <remarks>
    /// Es el caso que separa la clave del duplicado: sin ella, el móvil que perdió la cobertura y
    /// reenvía recibiría «ese GTIN ya lo lleva un artículo», que es verdad y es su propio alta.
    /// </remarks>
    [Fact]
    public async Task El_reintento_con_la_misma_clave_repite_el_201_y_no_es_un_409()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(638);
        ArticuloDto articulo = await ArticuloAsync(cliente, 638, "A");
        string clave = Guid.NewGuid().ToString();

        using HttpResponseMessage primera = await AgregarConClaveAsync(cliente, articulo.Id, clave);
        using HttpResponseMessage segunda = await AgregarConClaveAsync(cliente, articulo.Id, clave);

        primera.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(primera));
        segunda.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(segunda));
        (await segunda.Content.ReadAsStringAsync()).ShouldBe(await primera.Content.ReadAsStringAsync());
        segunda.Headers.GetValues(RespuestaRepetida.CabeceraDeRepeticion).ShouldContain("true");

        (await ListadoAsync(cliente, articulo.Id)).ShouldHaveSingleItem();
    }

    private async Task<HttpClient> EnUnaEmpresaNuevaAsync(int semilla)
    {
        (HttpClient cliente, EmpresaDto _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(semilla));
        _clientes.Add(cliente);

        return cliente;
    }

    /// <summary>Un artículo con su unidad y su tramo de impuesto, propios del caso y de la letra.</summary>
    private static async Task<ArticuloDto> ArticuloAsync(HttpClient cliente, int semilla, string letra)
    {
        string sufijo = semilla.ToString(CultureInfo.InvariantCulture) + letra;

        using HttpResponseMessage unidad = await cliente.PostAsJsonAsync(
            Unidades,
            new CrearUnidadMedidaDto { Codigo = "U" + sufijo, Nombre = "Unidad " + sufijo, Decimales = 0 });

        unidad.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(unidad));

        using HttpResponseMessage impuesto = await cliente.PostAsJsonAsync(
            Impuestos,
            new CrearImpuestoDto
            {
                Codigo = "GTN" + sufijo,
                Nombre = "Tramo GTN" + sufijo,
                Tipo = "Iva",
                Porcentaje = 21m,
                VigenteDesde = s_desde,
                VigenteHasta = null,
            });

        impuesto.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(impuesto));

        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Articulos,
            new CrearArticuloDto
            {
                Codigo = "ART-" + sufijo,
                Descripcion = "Artículo " + sufijo,
                Tipo = "Bien",
                UnidadBaseId = (await unidad.Content.ReadFromJsonAsync<UnidadMedidaDto>())!.Id,
                ImpuestoPorDefectoId = (await impuesto.Content.ReadFromJsonAsync<ImpuestoDto>())!.Id,
                CategoriaId = null,
            });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<ArticuloDto>())!;
    }

    private static Task<HttpResponseMessage> AgregarAsync(
        HttpClient cliente,
        Guid articuloId,
        string? gtin,
        string nivel,
        int? unidades = null) =>
        cliente.PostAsJsonAsync(
            $"{Articulos}/{articuloId}/gtins",
            new AgregarCodigoBarrasDto { Gtin = gtin, Nivel = nivel, Unidades = unidades });

    private static async Task<CodigoBarrasDto> AgregadoAsync(HttpClient cliente, Guid articuloId, string gtin)
    {
        using HttpResponseMessage alta = await AgregarAsync(cliente, articuloId, gtin, "Base");

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<CodigoBarrasDto>())!;
    }

    private static Task<HttpResponseMessage> AgregarConClaveAsync(HttpClient cliente, Guid articuloId, string clave)
    {
        HttpRequestMessage peticion = new(HttpMethod.Post, $"{Articulos}/{articuloId}/gtins")
        {
            Content = JsonContent.Create(new AgregarCodigoBarrasDto { Gtin = "4006381333931", Nivel = "Base" }),
        };

        peticion.Headers.TryAddWithoutValidation("Idempotency-Key", clave);

        return cliente.SendAsync(peticion);
    }

    private static async Task<IReadOnlyList<CodigoBarrasDto>> ListadoAsync(HttpClient cliente, Guid articuloId)
    {
        using HttpResponseMessage listado = await cliente.GetAsync($"{Articulos}/{articuloId}/gtins");

        listado.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(listado));

        return (await listado.Content.ReadFromJsonAsync<List<CodigoBarrasDto>>())!;
    }

    private static async Task<IReadOnlyList<CodigoBarrasDto>> BuscarAsync(HttpClient cliente, string gtin)
    {
        using HttpResponseMessage busqueda = await cliente.GetAsync($"{Gtins}?gtin={Uri.EscapeDataString(gtin)}");

        busqueda.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(busqueda));

        return (await busqueda.Content.ReadFromJsonAsync<List<CodigoBarrasDto>>())!;
    }

    private static async Task<string?> TypeDe(HttpResponseMessage respuesta)
    {
        respuesta.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        return JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync())
            .RootElement.GetProperty("type").GetString();
    }
}
