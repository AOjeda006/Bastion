using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Bloqueos;
using Bastion.Organizacion.Contracts.Empresas;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Bloqueos;

/// <summary>
/// El único camino que enseña lo bloqueado, por el efecto: qué entrega, qué <b>no</b> entrega, y
/// que sigue respetando el filtro de empresa (ADR-0027).
/// </summary>
/// <remarks>
/// <para>
/// <b>Todo por HTTP, y con dos empresas donde hace falta.</b> Lo que hay que demostrar no es que el
/// repositorio sepa unir tres tablas: es que una petición corriente con el permiso del artículo 32
/// ve lo suyo bloqueado, no ve lo de nadie más, y no recibe de vuelta ninguna llave de
/// concurrencia. Un test que abriera el contexto a mano probaría la consulta y no el sistema.
/// </para>
/// <para>
/// <b>La ausencia de testigo de versión se comprueba también aquí, y no es una duplicación.</b>
/// <c>NingunaLecturaEntregaTestigoDeVersionTests</c> mira el <i>contrato</i> —los tipos que la API
/// declara devolver— y esto mira <b>la respuesta de verdad</b>: la cabecera <c>ETag</c> que el
/// servidor pone o no pone, y el JSON que sale por el cable. Son dos preguntas distintas, y las dos
/// sostienen las cuatro exenciones de <c>If-Match</c> del ADR-0017.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElAccesoReservadoDelArticulo32Tests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Almacenes = "/api/v1/organizacion/almacenes";
    private const string Empresas = "/api/v1/organizacion/empresas";
    private const string Bloqueados = "/api/v1/organizacion/bloqueados";

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

    [Fact]
    public async Task Un_almacen_bloqueado_desaparece_de_los_caminos_ordinarios_y_aparece_en_este()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync("00000089C");
        AlmacenDto almacen = await CrearAlmacenAsync(cliente, "BLOQ-1");

        using HttpResponseMessage bloqueo = await cliente.SuprimirAsync($"{Almacenes}/{almacen.Id}");
        bloqueo.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(bloqueo));

        // 1. Por los dos caminos ordinarios, no existe. Es la mitad de R16 que ya estaba.
        using HttpResponseMessage ficha = await cliente.GetAsync($"{Almacenes}/{almacen.Id}");
        ficha.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        PaginaDe<AlmacenDto> ordinario = await PaginaAsync<AlmacenDto>(cliente, Almacenes);
        ordinario.Elementos.ShouldNotContain(fila => fila.Id == almacen.Id);

        // 2. Por el camino del artículo 32, sí. Y con lo que hace falta para decidir: desde cuándo
        // está bloqueado y hasta cuándo dura la reserva.
        BloqueadoDto bloqueado = await BuscarAsync(cliente, almacen.Id);

        bloqueado.Tipo.ShouldBe("Almacen");
        bloqueado.Codigo.ShouldBe("BLOQ-1");
        bloqueado.Motivo.ShouldBe("CeseDeUso");
        bloqueado.BloqueadoEn.ShouldBeGreaterThan(DateTimeOffset.UnixEpoch);

        // Un almacén retirado NO vence, y el nulo dice eso: se conserva por razón contable —el
        // histórico de valoración apunta a él para siempre— y sus datos no son de nadie.
        bloqueado.VenceEn.ShouldBeNull();
    }

    [Fact]
    public async Task Una_supresion_del_articulo_32_si_vence_y_la_fecha_sale_en_el_listado()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync("00000090K");

        PaginaDe<EmpresaDto> padron = await PaginaAsync<EmpresaDto>(cliente, Empresas);
        EmpresaDto empresa = padron.Elementos.ShouldHaveSingleItem();

        using HttpResponseMessage supresion = await cliente.SuprimirAsync($"{Empresas}/{empresa.Id}");
        supresion.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(supresion));

        BloqueadoDto bloqueado = await BuscarAsync(cliente, empresa.Id);

        bloqueado.Tipo.ShouldBe("Empresa");
        bloqueado.Motivo.ShouldBe("SupresionSolicitada");

        // Una empresa no tiene código: se reconoce por su razón social. El nulo es la respuesta y
        // no un hueco.
        bloqueado.Codigo.ShouldBeNull();

        // Y ESTA es la mitad que no existía antes del ítem 1.4. Un bloqueo del art. 32 sin fecha de
        // fin convierte una conservación acotada en indefinida, que es la infracción por el otro
        // lado. Seis años por omisión: el plazo del art. 30 del Código de Comercio.
        bloqueado.VenceEn.ShouldNotBeNull();
        bloqueado.VenceEn.Value.ShouldBe(bloqueado.BloqueadoEn.AddYears(6));
    }

    [Fact]
    public async Task El_listado_de_lo_bloqueado_no_devuelve_ninguna_llave_de_concurrencia()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync("00000091E");
        AlmacenDto almacen = await CrearAlmacenAsync(cliente, "BLOQ-2");

        using HttpResponseMessage bloqueo = await cliente.SuprimirAsync($"{Almacenes}/{almacen.Id}");
        bloqueo.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(bloqueo));

        using HttpResponseMessage respuesta = await cliente.GetAsync($"{Bloqueados}?page=1&size=200");
        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(respuesta));

        // De esto cuelgan las cuatro exenciones de If-Match del ADR-0017. La llave que la
        // precondición pediría es la ETIQUETA, no la fila: mientras esta lectura no emita ninguna,
        // el desbloqueo no puede exigir una que no hay manera de conseguir.
        respuesta.Headers.ETag.ShouldBeNull(
            "el listado de lo bloqueado emite ETag, así que la llave que If-Match pide vuelve a " +
            "existir y las cuatro exenciones de los desbloqueos han caducado (ADR-0017, ADR-0027)");

        string cuerpo = await respuesta.Content.ReadAsStringAsync();

        cuerpo.ShouldContain(almacen.Id.ToString(), Case.Insensitive,
            "sin la fila bloqueada dentro, lo de abajo estaría mirando una página vacía");

        foreach (string testigo in new[] { "version", "etag", "xmin", "rowversion", "concurrencia" })
        {
            cuerpo.ShouldNotContain(testigo, Case.Insensitive,
                $"el cuerpo del listado de lo bloqueado lleva «{testigo}»: un testigo de " +
                "concurrencia por fila hace caducar las cuatro exenciones de If-Match a la vez");
        }
    }

    [Fact]
    public async Task Lo_bloqueado_de_otra_empresa_no_asoma_por_este_camino()
    {
        HttpClient enA = await EnUnaEmpresaNuevaAsync("00000092T");
        AlmacenDto deA = await CrearAlmacenAsync(enA, "BLOQ-A");

        using HttpResponseMessage bloqueo = await enA.SuprimirAsync($"{Almacenes}/{deA.Id}");
        bloqueo.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(bloqueo));

        HttpClient enB = await EnUnaEmpresaNuevaAsync("00000093R");

        // El ámbito del art. 32 apaga el filtro del BLOQUEO y ninguno más. Si apagara también el de
        // empresa —que es lo que haría un `IgnoreQueryFilters`—, B estaría viendo los almacenes
        // retirados de A, que es una fuga entre clientes de la instalación por la puerta que se
        // abrió para cumplir la ley.
        PaginaDe<BloqueadoDto> deB = await PaginaAsync<BloqueadoDto>(enB, Bloqueados);

        deB.Elementos.ShouldNotContain(fila => fila.Id == deA.Id);
        deB.Total.ShouldBe(0);
    }

    private static async Task<BloqueadoDto> BuscarAsync(HttpClient cliente, Guid id)
    {
        PaginaDe<BloqueadoDto> pagina = await PaginaAsync<BloqueadoDto>(cliente, Bloqueados);

        BloqueadoDto? fila = pagina.Elementos.SingleOrDefault(uno => uno.Id == id);

        fila.ShouldNotBeNull(
            $"la fila {id} está bloqueada y no sale por el camino del art. 32. Lo que sí sale: " +
            string.Join(", ", pagina.Elementos.Select(uno => $"{uno.Tipo} {uno.Id}")));

        return fila;
    }

    private static async Task<PaginaDe<T>> PaginaAsync<T>(HttpClient cliente, string ruta)
    {
        using HttpResponseMessage respuesta = await cliente.GetAsync($"{ruta}?page=1&size=200");

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(respuesta));

        return (await respuesta.Content.ReadFromJsonAsync<PaginaDe<T>>())!;
    }

    private async Task<HttpClient> EnUnaEmpresaNuevaAsync(string nif)
    {
        (HttpClient cliente, EmpresaDto _) = await _api.EnUnaEmpresaNuevaAsync(nif);
        _clientes.Add(cliente);

        return cliente;
    }

    private static async Task<AlmacenDto> CrearAlmacenAsync(HttpClient cliente, string codigo)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Almacenes,
            new CrearAlmacenDto
            {
                Codigo = codigo,
                Nombre = $"Almacén {codigo}",
                Tipo = "Fisico",
                Direccion = Escenario.Domicilio(),
            });

        alta.IsSuccessStatusCode.ShouldBeTrue(await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<AlmacenDto>())!;
    }
}
