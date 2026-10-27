using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Inventario;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Contracts.Maestros;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Catalogo.Contracts;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Infrastructure.Persistencia;
using Bastion.Catalogo.Infrastructure.Persistencia.Repositorios;
using Bastion.Organizacion.Contracts;
using Bastion.Organizacion.Contracts.Divisas;
using Bastion.Organizacion.Contracts.Empresas;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Exportacion;

/// <summary>
/// Las exportaciones de Catálogo contra la base: el fichero entero, en el dialecto de la
/// importación, con el orden y los filtros del listado, y cada maestro al que apunta una fila con
/// su código y su nombre (ADR-0073, ADR-0074).
/// </summary>
/// <remarks>
/// <para>
/// <b>Un maestro se nombra si quien exporta lo puede ver</b>, y si no, sale su identificador, que
/// es lo que enseña la pantalla. Se prueba con dos cuentas que se reparten los tres permisos, y
/// cada una es la pareja de la otra: lo que una ve nombrado, la otra lo ve por su identificador.
/// </para>
/// <para>
/// <b>Los códigos de barras cuelgan de su artículo y no se paginan</b>: la exportación los parte en
/// tramos ella misma, contesta el <c>404</c> del listado si el artículo no existe, y rechaza un
/// <c>?q=</c>, que el listado no mira.
/// </para>
/// <para>
/// <b>Semillas: del 943 al 948</b>, del bloque del 3.2 (ver
/// <c>ElTextoSoloLoRecibeQuienLoMiraTests</c>).
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LasExportacionesDeCatalogoTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Categorias = "/api/v1/catalogo/categorias";
    private const string Tarifas = "/api/v1/catalogo/tarifas";
    private const string Divisas = "/api/v1/organizacion/divisas";

    private const string CabeceraDeArticulos =
        "codigo;descripcion;tipo;trazabilidad;unidad_base_codigo;unidad_base_nombre;" +
        "impuesto_por_defecto_codigo;impuesto_por_defecto_nombre;categoria_codigo;categoria_nombre\r\n";

    private const string CabeceraDeCategorias = "codigo;nombre;padre_codigo;padre_nombre\r\n";

    private const string CabeceraDeTarifas =
        "codigo;nombre;divisa_codigo;divisa_nombre;vigente_desde;vigente_hasta\r\n";

    private const string CabeceraDeGtins = "articulo_codigo;articulo_nombre;gtin;nivel;unidades\r\n";

    private readonly ApiDeVerdad _api = new(postgres);

    /// <inheritdoc/>
    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Los_articulos_salen_con_sus_maestros_nombrados_y_los_filtros_del_listado()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(943));
        using HttpClient suyo = cliente;

        (Guid articuloId, _) = await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, 943, "PorLote");
        ArticuloDto primero = await ArticuloAsync(cliente, articuloId);
        CategoriaDto fruta = await CrearCategoriaAsync(cliente, "FRUTA", "Fruta");

        await CrearArticuloAsync(cliente, primero, "APT-943B", "=1+1 manzanas", "Servicio", fruta.Id);

        const string LineaDeA = "APT-943;Artículo 943;Bien;PorLote;W943;Unidad 943;ART943;Tramo ART943;;\r\n";
        const string LineaDeB =
            "APT-943B;'=1+1 manzanas;Servicio;Ninguna;W943;Unidad 943;ART943;Tramo ART943;FRUTA;Fruta\r\n";

        (await ExportarAsync(cliente, "articulos")).ShouldBe(
            CabeceraDeArticulos + LineaDeA + LineaDeB,
            "sin categoría, sus dos columnas vacías; y la descripción, con el apóstrofo");

        (await ExportarAsync(cliente, "articulos", "?sort=-codigo")).ShouldBe(
            CabeceraDeArticulos + LineaDeB + LineaDeA);
        (await ExportarAsync(cliente, "articulos", "?q=manzanas")).ShouldBe(CabeceraDeArticulos + LineaDeB);
        (await ExportarAsync(cliente, "articulos", $"?categoria={fruta.Id}")).ShouldBe(
            CabeceraDeArticulos + LineaDeB, "la categoría del listado, que no es un texto");
    }

    /// <summary>
    /// Cada maestro del artículo se nombra con el permiso de verlo, y sin él sale su identificador.
    /// </summary>
    [Fact]
    public async Task Cada_maestro_del_articulo_se_nombra_solo_si_quien_exporta_lo_puede_ver()
    {
        // En la empresa de la semilla, que es en la que ConPermisosAsync da los permisos.
        using HttpClient administrador = await _api.ComoAdministradorAsync();

        (Guid articuloId, _) = await LosMaestrosPorLaApi.CrearArticuloAsync(administrador, 944);
        ArticuloDto plantilla = await ArticuloAsync(administrador, articuloId);
        CategoriaDto categoria = await CrearCategoriaAsync(administrador, "CAT944", "Categoría 944");
        ArticuloDto conCategoria = await CrearArticuloAsync(
            administrador, plantilla, "APT-944C", "Con categoría 944", "Bien", categoria.Id);

        string consulta = $"?categoria={categoria.Id}";

        using HttpClient conLaUnidad = await _api.ConPermisosAsync(
            PermisosDeCatalogo.ArticuloVer, PermisosDeOrganizacion.UnidadMedidaVer);
        using HttpClient conLoDemas = await _api.ConPermisosAsync(
            PermisosDeCatalogo.ArticuloVer, PermisosDeOrganizacion.ImpuestoVer, PermisosDeCatalogo.CategoriaVer);

        (await ExportarAsync(conLaUnidad, "articulos", consulta)).ShouldBe(
            CabeceraDeArticulos +
            $"APT-944C;Con categoría 944;Bien;Ninguna;W944;Unidad 944;{conCategoria.ImpuestoPorDefectoId};;" +
            $"{categoria.Id};\r\n");

        (await ExportarAsync(conLoDemas, "articulos", consulta)).ShouldBe(
            CabeceraDeArticulos +
            $"APT-944C;Con categoría 944;Bien;Ninguna;{conCategoria.UnidadBaseId};;ART944;Tramo ART944;" +
            "CAT944;Categoría 944\r\n");
    }

    [Fact]
    public async Task Las_categorias_nombran_a_su_madre_y_las_tarifas_su_divisa_por_su_orden()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(945));
        using HttpClient suyo = cliente;

        CategoriaDto raiz = await CrearCategoriaAsync(cliente, "RAIZ", "Raíz");
        await CrearCategoriaAsync(cliente, "HOJA", "Hoja; la de \"dentro\"", raiz.Id);

        const string LineaDeHoja = "HOJA;\"Hoja; la de \"\"dentro\"\"\";RAIZ;Raíz\r\n";
        const string LineaDeRaiz = "RAIZ;Raíz;;\r\n";

        (await ExportarAsync(cliente, "categorias")).ShouldBe(
            CabeceraDeCategorias + LineaDeHoja + LineaDeRaiz, "la raíz no tiene madre, y sus dos columnas van vacías");
        (await ExportarAsync(cliente, "categorias", "?sort=-codigo")).ShouldBe(
            CabeceraDeCategorias + LineaDeRaiz + LineaDeHoja);
        (await ExportarAsync(cliente, "categorias", "?q=dentro")).ShouldBe(CabeceraDeCategorias + LineaDeHoja);

        DivisaDto euro = await EuroAsync(cliente);

        await CrearTarifaAsync(
            cliente, "GEN", "General vieja", euro.Id, new DateOnly(2000, 1, 1), new DateOnly(2000, 12, 31));
        await CrearTarifaAsync(cliente, "GEN", "General", euro.Id, new DateOnly(2001, 1, 1), null);
        await CrearTarifaAsync(cliente, "MAY", "Mayorista", euro.Id, new DateOnly(2001, 1, 1), null);

        string divisa = $"{euro.Codigo};{euro.Nombre}";
        string general = $"GEN;General;{divisa};2001-01-01;\r\n";
        string vieja = $"GEN;General vieja;{divisa};2000-01-01;2000-12-31\r\n";
        string mayorista = $"MAY;Mayorista;{divisa};2001-01-01;\r\n";

        (await ExportarAsync(cliente, "tarifas")).ShouldBe(
            CabeceraDeTarifas + general + vieja + mayorista,
            "el desempate del listado: dentro del código, el tramo más reciente primero");
        (await ExportarAsync(cliente, "tarifas", "?codigo=GEN")).ShouldBe(CabeceraDeTarifas + general + vieja);
        (await ExportarAsync(cliente, "tarifas", "?q=mayor")).ShouldBe(CabeceraDeTarifas + mayorista);
    }

    [Fact]
    public async Task Los_codigos_de_barras_salen_en_el_orden_del_listado_con_su_articulo_nombrado()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(946));
        using HttpClient suyo = cliente;

        (Guid articuloId, _) = await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, 946);

        // Las cajas, al revés de como salen: el listado va por unidades, no por orden de alta.
        await AgregarGtinAsync(cliente, articuloId, "4006381333931", "Base", null);
        await AgregarGtinAsync(cliente, articuloId, "10012345678902", "Caja", 12);
        await AgregarGtinAsync(cliente, articuloId, "10036000291459", "Caja", 6);

        string gtins = $"{LosMaestrosPorLaApi.Articulos}/{articuloId}/gtins";

        string fichero = CabeceraDeGtins +
            "APT-946;Artículo 946;04006381333931;Base;1\r\n" +
            "APT-946;Artículo 946;10036000291459;Caja;6\r\n" +
            "APT-946;Artículo 946;10012345678902;Caja;12\r\n";

        (await Exportaciones.ExportarAsync(cliente, gtins, "gtins")).ShouldBe(
            fichero, "el GTIN en sus catorce dígitos, que es como se guarda y como lo da el listado");

        // El artículo que no existe es el 404 del listado, con su problema y sin fichero.
        string inventado = $"{LosMaestrosPorLaApi.Articulos}/{Guid.CreateVersion7()}/gtins";

        using HttpResponseMessage listado = await cliente.GetAsync(inventado);
        using HttpResponseMessage exportado = await cliente.GetAsync($"{inventado}/exportacion");

        listado.StatusCode.ShouldBe(HttpStatusCode.NotFound, "el arnés: el listado no encuentra el artículo");
        exportado.StatusCode.ShouldBe(HttpStatusCode.NotFound, await Escenario.Detalle(exportado));
        exportado.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await Exportaciones.ProblemaAsync(exportado)).ShouldBe(await Exportaciones.ProblemaAsync(listado));

        // Y un texto, que el listado no mira: el mismo 400 que daría un listado paginado sin ?q=.
        using HttpResponseMessage conTexto = await cliente.GetAsync($"{gtins}/exportacion?q=4006");

        conTexto.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await Escenario.Detalle(conTexto));
        (await EscenaDeTransferencia.TipoDelProblemaAsync(conTexto)).ShouldBe("/errors/filtro-no-admitido");

        // La pareja: la caja del texto en blanco no es un texto, y el fichero sale entero.
        (await Exportaciones.ExportarAsync(cliente, gtins, "gtins", "?q=%20")).ShouldBe(fichero);
    }

    /// <summary>
    /// El código y el nombre de las categorías salen por lotes, los de las que se preguntan, y una
    /// de otra empresa no vuelve, como una inventada.
    /// </summary>
    /// <remarks>
    /// <b>Contra el repositorio y no contra el fichero</b>, porque el fichero no lo distingue: en
    /// una exportación solo se pregunta por madres de la empresa, y que volvieran todas las de la
    /// empresa no cambiaría ningún nombre. Con una tercera que está y no se pregunta, para que no
    /// pase un repositorio que las nombrara a todas; y la contraria al final, en su empresa.
    /// </remarks>
    [Fact]
    public async Task Los_nombres_de_las_categorias_salen_por_lotes_y_los_ajenos_no_vuelven()
    {
        (HttpClient cliente, EmpresaDto propia) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(947));
        using HttpClient suyo = cliente;
        (HttpClient enLaAjena, EmpresaDto ajena) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(948));
        using HttpClient delAjeno = enLaAjena;

        CategoriaDto una = await CrearCategoriaAsync(cliente, "UNA", "Una");
        CategoriaDto otra = await CrearCategoriaAsync(cliente, "OTRA", "Otra", una.Id);
        await CrearCategoriaAsync(cliente, "TERCERA", "Tercera");
        CategoriaDto deLaOtra = await CrearCategoriaAsync(enLaAjena, "AJENA", "Ajena");

        await using (CatalogoDbContext contexto = postgres.AbrirCatalogo(propia.Id))
        {
            (await new RepositorioDeCategorias(contexto)
                .NombresDeAsync([una.Id, otra.Id, deLaOtra.Id, Guid.CreateVersion7()], CancellationToken.None))
                .ShouldBe(
                    new Dictionary<Guid, NombreDeMaestro>
                    {
                        [una.Id] = new("UNA", "Una"),
                        [otra.Id] = new("OTRA", "Otra"),
                    },
                    ignoreOrder: true,
                    "cada categoría por la que se pregunta con lo suyo, y ni la de otra empresa, ni la " +
                    "inventada, ni la de la empresa por la que no se pregunta");
        }

        await using CatalogoDbContext enSuEmpresa = postgres.AbrirCatalogo(ajena.Id);

        (await new RepositorioDeCategorias(enSuEmpresa).NombresDeAsync([deLaOtra.Id], CancellationToken.None))
            .ShouldBe(
                new Dictionary<Guid, NombreDeMaestro> { [deLaOtra.Id] = new("AJENA", "Ajena") },
                ignoreOrder: true,
                "en su propia empresa sí vuelve");
    }

    private static async Task<ArticuloDto> ArticuloAsync(HttpClient cliente, Guid articuloId) =>
        (await cliente.GetFromJsonAsync<ArticuloDto>($"{LosMaestrosPorLaApi.Articulos}/{articuloId}"))!;

    /// <summary>Otro artículo, con la unidad y el tramo de impuesto de uno que ya hay.</summary>
    private static async Task<ArticuloDto> CrearArticuloAsync(
        HttpClient cliente,
        ArticuloDto plantilla,
        string codigo,
        string descripcion,
        string tipo,
        Guid? categoriaId)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            LosMaestrosPorLaApi.Articulos,
            new CrearArticuloDto
            {
                Codigo = codigo,
                Descripcion = descripcion,
                Tipo = tipo,
                UnidadBaseId = plantilla.UnidadBaseId,
                ImpuestoPorDefectoId = plantilla.ImpuestoPorDefectoId,
                CategoriaId = categoriaId,
            });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<ArticuloDto>())!;
    }

    private static async Task<CategoriaDto> CrearCategoriaAsync(
        HttpClient cliente,
        string codigo,
        string nombre,
        Guid? padreId = null)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Categorias, new CrearCategoriaDto { Codigo = codigo, Nombre = nombre, PadreId = padreId });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<CategoriaDto>())!;
    }

    private static async Task CrearTarifaAsync(
        HttpClient cliente,
        string codigo,
        string nombre,
        Guid divisaId,
        DateOnly desde,
        DateOnly? hasta)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Tarifas,
            new CrearTarifaDto
            {
                Codigo = codigo,
                Nombre = nombre,
                DivisaId = divisaId,
                VigenteDesde = desde,
                VigenteHasta = hasta,
            });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));
    }

    private static async Task AgregarGtinAsync(
        HttpClient cliente,
        Guid articuloId,
        string gtin,
        string nivel,
        int? unidades)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            $"{LosMaestrosPorLaApi.Articulos}/{articuloId}/gtins",
            new AgregarCodigoBarrasDto { Gtin = gtin, Nivel = nivel, Unidades = unidades });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));
    }

    /// <summary>El euro, dándolo de alta si todavía no está, con el nombre que tenga.</summary>
    private static async Task<DivisaDto> EuroAsync(HttpClient cliente)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Divisas, new CrearDivisaDto { Codigo = "EUR", Nombre = "Euro" });

        if (alta.StatusCode == HttpStatusCode.Created)
        {
            return (await alta.Content.ReadFromJsonAsync<DivisaDto>())!;
        }

        alta.StatusCode.ShouldBe(
            HttpStatusCode.Conflict,
            "si no se ha creado, lo único esperable es que ya estuviera. " + await Escenario.Detalle(alta));

        for (int pagina = 1; ; pagina++)
        {
            PaginaDe<DivisaDto> trozo = (await cliente.GetFromJsonAsync<PaginaDe<DivisaDto>>(
                $"{Divisas}?page={pagina}&size={Paginacion.TamanioMaximo}"))!;
            DivisaDto? euro = trozo.Elementos.FirstOrDefault(divisa => divisa.Codigo == "EUR");

            if (euro is not null)
            {
                return euro;
            }

            if (trozo.Elementos.Count == 0 || pagina * Paginacion.TamanioMaximo >= trozo.Total)
            {
                throw new InvalidOperationException("El euro ni se ha creado ni está en el listado.");
            }
        }
    }

    private static Task<string> ExportarAsync(HttpClient cliente, string recurso, string consulta = "") =>
        Exportaciones.ExportarAsync(cliente, $"/api/v1/catalogo/{recurso}", recurso, consulta);
}
