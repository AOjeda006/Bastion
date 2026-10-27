using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Inventario;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Fechas;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Empresas;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Exportacion;

/// <summary>
/// Las exportaciones de Organización contra la base: el fichero entero, en el dialecto de la
/// importación, con el orden y el texto del listado, todas sus páginas y sin lo bloqueado
/// (ADR-0073).
/// </summary>
/// <remarks>
/// <para>
/// <b>El fichero se compara entero</b>, byte a byte desde el BOM: la cabecera, el orden de las
/// columnas, el CRLF, el apóstrofo y los campos vacíos de una dirección que falta. Una columna de
/// más, o una cambiada de sitio, no pasa.
/// </para>
/// <para>
/// <b>Lo bloqueado sale del fichero porque sale del listado</b>: se exporta antes y después de
/// bloquearlo, y lo único que cambia entre las dos es el bloqueo.
/// </para>
/// <para>
/// <b>Semillas: del 935 al 939</b>, del bloque del 3.2 (ver <c>ElTextoSoloLoRecibeQuienLoMiraTests</c>).
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LasExportacionesDeOrganizacionTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Empresas = "/api/v1/organizacion/empresas";

    private const string CabeceraDeAlmacenes =
        "codigo;nombre;tipo;direccion_calle;direccion_numero;direccion_codigo_postal;direccion_poblacion;" +
        "direccion_subdivision;direccion_pais\r\n";

    private const string CabeceraDeEmpresas =
        "nif;razon_social;domicilio_calle;domicilio_numero;domicilio_codigo_postal;domicilio_poblacion;" +
        "domicilio_subdivision;domicilio_pais;divisa_base;regimen_de_iva\r\n";

    private static readonly byte[] s_bom = [0xEF, 0xBB, 0xBF];

    private readonly ApiDeVerdad _api = new(postgres);

    /// <inheritdoc/>
    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Los_almacenes_salen_enteros_en_el_dialecto_y_sin_el_bloqueado()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(935));
        using HttpClient suyo = cliente;

        await LosMaestrosPorLaApi.CrearAlmacenAsync(cliente, "B-2");
        await CrearAlmacenAsync(cliente, "A-1", "=SUMA(A1); el de \"fuera\"", "Virtual");
        AlmacenDto cerrado = await LosMaestrosPorLaApi.CrearAlmacenAsync(cliente, "C-3");

        const string LineaDeA = "A-1;\"'=SUMA(A1); el de \"\"fuera\"\"\";Virtual;;;;;;\r\n";
        const string LineaDeB = "B-2;Almacén B-2;Fisico;Gran Vía;31;28013;Madrid;Madrid;ES\r\n";

        (await ExportarAsync(cliente, "almacenes")).ShouldBe(
            CabeceraDeAlmacenes + LineaDeA + LineaDeB + "C-3;Almacén C-3;Fisico;Gran Vía;31;28013;Madrid;Madrid;ES\r\n",
            "la pareja: antes del bloqueo, el tercero sale");

        using HttpResponseMessage bloqueo = await cliente.SuprimirAsync($"{LosMaestrosPorLaApi.Almacenes}/{cerrado.Id}");
        bloqueo.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(bloqueo));

        (await ExportarAsync(cliente, "almacenes")).ShouldBe(
            CabeceraDeAlmacenes + LineaDeA + LineaDeB,
            "el almacén bloqueado sale del listado, y con él del fichero (R16)");
    }

    [Fact]
    public async Task La_exportacion_lleva_el_orden_y_el_texto_del_listado()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(936));
        using HttpClient suyo = cliente;

        await CrearAlmacenAsync(cliente, "A-1", "Zeta", "Virtual");
        await CrearAlmacenAsync(cliente, "B-2", "Alfa", "Virtual");
        await CrearAlmacenAsync(cliente, "C-3", "Media", "Virtual");

        Codigos(await ExportarAsync(cliente, "almacenes")).ShouldBe(["A-1", "B-2", "C-3"], "sin orden, el del listado");
        Codigos(await ExportarAsync(cliente, "almacenes", "?sort=-nombre")).ShouldBe(["A-1", "C-3", "B-2"]);
        Codigos(await ExportarAsync(cliente, "almacenes", "?q=alf")).ShouldBe(["B-2"]);

        // Y lo que el listado rechaza, la exportación lo rechaza igual, con su problema y sin fichero.
        using HttpResponseMessage listado = await cliente.GetAsync($"{LosMaestrosPorLaApi.Almacenes}?sort=inventado");
        using HttpResponseMessage exportado =
            await cliente.GetAsync($"{LosMaestrosPorLaApi.Almacenes}/exportacion?sort=inventado");

        listado.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "el arnés: el listado rechaza el orden");
        exportado.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await Escenario.Detalle(exportado));
        exportado.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        // El mismo problema, salvo dónde se pidió y la traza, que son de cada petición.
        (await ProblemaAsync(exportado)).ShouldBe(await ProblemaAsync(listado));
    }

    /// <summary>
    /// Más de una página interna: el fichero lleva todas, en el orden del listado, que es el de sus
    /// páginas una detrás de otra.
    /// </summary>
    [Fact]
    public async Task Mas_de_una_pagina_sale_entera_y_en_el_orden_de_las_paginas_del_listado()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(937));
        using HttpClient suyo = cliente;

        int total = Paginacion.TamanioMaximo + 1;

        for (int numero = 1; numero <= total; numero++)
        {
            await CrearAlmacenAsync(cliente, $"P-{numero:D3}", $"Pasillo {total - numero:D3}", "Virtual");
        }

        List<string> delListado = [];

        for (int pagina = 1; pagina <= 2; pagina++)
        {
            PaginaDe<AlmacenDto>? leida = await cliente.GetFromJsonAsync<PaginaDe<AlmacenDto>>(
                $"{LosMaestrosPorLaApi.Almacenes}?sort=nombre&page={pagina}&size={Paginacion.TamanioMaximo}");

            delListado.AddRange(leida!.Elementos.Select(almacen => almacen.Codigo));
        }

        delListado.Count.ShouldBe(total, "el arnés: el listado tiene más de una página");
        delListado[0].ShouldBe($"P-{total:D3}", "el arnés: el orden pedido no es el del código");

        Codigos(await ExportarAsync(cliente, "almacenes", "?sort=nombre")).ShouldBe(delListado);
    }

    /// <summary>
    /// Las empresas, desde dentro de una: la suya y ninguna más, porque el listado es el de la
    /// empresa activa (R8), con su texto, y nada cuando está bloqueada.
    /// </summary>
    [Fact]
    public async Task Las_empresas_salen_con_el_texto_del_listado_sin_las_ajenas_y_sin_la_bloqueada()
    {
        string nif = Escenario.NifInventado(938);
        (HttpClient cliente, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync(nif);
        using HttpClient suyo = cliente;

        // La otra existe, y no sale: el fichero no es el padrón de empresas de la instalación.
        await Escenario.CrearEmpresaAsync(cliente, Escenario.NifInventado(939));

        string suya = CabeceraDeEmpresas + $"{nif};Empresa {nif};Gran Vía;31;28013;Madrid;Madrid;ES;EUR;General\r\n";

        (await ExportarAsync(cliente, "empresas")).ShouldBe(suya);
        (await ExportarAsync(cliente, "empresas", $"?q=Empresa {nif}")).ShouldBe(suya);
        (await ExportarAsync(cliente, "empresas", "?q=ninguna")).ShouldBe(
            CabeceraDeEmpresas, "la pareja del texto: uno que no casa la deja fuera");

        using HttpResponseMessage bloqueo = await cliente.SuprimirAsync($"{Empresas}/{empresa.Id}");
        bloqueo.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(bloqueo));

        (await ExportarAsync(cliente, "empresas")).ShouldBe(
            CabeceraDeEmpresas, "la empresa bloqueada sale del listado, y con él del fichero (R16)");
    }

    private static async Task CrearAlmacenAsync(HttpClient cliente, string codigo, string nombre, string tipo)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            LosMaestrosPorLaApi.Almacenes,
            new CrearAlmacenDto { Codigo = codigo, Nombre = nombre, Tipo = tipo });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));
    }

    /// <summary>
    /// Pide la exportación y devuelve el fichero sin el BOM, después de afirmar el BOM, el tipo del
    /// contenido y el nombre del adjunto.
    /// </summary>
    private static async Task<string> ExportarAsync(HttpClient cliente, string recurso, string consulta = "")
    {
        DateOnly antes = DiaDeNegocio.Hoy(TimeProvider.System);
        using HttpResponseMessage respuesta =
            await cliente.GetAsync($"/api/v1/organizacion/{recurso}/exportacion{consulta}");
        DateOnly despues = DiaDeNegocio.Hoy(TimeProvider.System);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(respuesta));

        MediaTypeHeaderValue? tipo = respuesta.Content.Headers.ContentType;
        tipo.ShouldNotBeNull();
        tipo.MediaType.ShouldBe("text/csv");
        tipo.CharSet.ShouldBe("utf-8");

        // El nombre lleva el día de negocio, y una exportación que cruza la medianoche puede llevar
        // cualquiera de los dos.
        ContentDispositionHeaderValue? adjunto = respuesta.Content.Headers.ContentDisposition;
        adjunto.ShouldNotBeNull("el fichero sale sin Content-Disposition, y el navegador no lo guarda");
        adjunto.DispositionType.ShouldBe("attachment");
        adjunto.FileNameStar.ShouldBeOneOf(Nombre(recurso, antes), Nombre(recurso, despues));
        adjunto.FileName.ShouldNotBeNull("sin filename, un navegador viejo no sabe cómo llamarlo");
        adjunto.FileName.Trim('"').ShouldBe(adjunto.FileNameStar);

        byte[] fichero = await respuesta.Content.ReadAsByteArrayAsync();

        fichero[..3].ShouldBe(s_bom, "el fichero empieza por el BOM de UTF-8");

        return Encoding.UTF8.GetString(fichero[3..]);
    }

    private static async Task<string> ProblemaAsync(HttpResponseMessage respuesta)
    {
        JsonObject problema = JsonNode.Parse(await respuesta.Content.ReadAsStringAsync())!.AsObject();

        problema.Remove("instance").ShouldBeTrue("el arnés: el problema dice dónde se pidió");
        problema.Remove("traceId").ShouldBeTrue("el arnés: el problema lleva su traza");

        return problema.ToJsonString();
    }

    private static string Nombre(string recurso, DateOnly dia) =>
        string.Create(CultureInfo.InvariantCulture, $"{recurso}-{dia:yyyy-MM-dd}.csv");

    /// <summary>La primera columna de cada fila, sin la cabecera.</summary>
    private static string[] Codigos(string fichero) =>
        [.. fichero.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(linea => linea.Split(';')[0])];
}
