using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Inventario;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Contracts.Paginacion;
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
        (await Exportaciones.ProblemaAsync(exportado)).ShouldBe(await Exportaciones.ProblemaAsync(listado));
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

    private static Task<string> ExportarAsync(HttpClient cliente, string recurso, string consulta = "") =>
        Exportaciones.ExportarAsync(cliente, $"/api/v1/organizacion/{recurso}", recurso, consulta);

    /// <summary>La primera columna de cada fila, sin la cabecera.</summary>
    private static string[] Codigos(string fichero) =>
        [.. fichero.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(linea => linea.Split(';')[0])];
}
