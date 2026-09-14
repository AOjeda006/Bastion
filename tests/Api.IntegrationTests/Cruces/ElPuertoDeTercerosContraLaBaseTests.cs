using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Terceros.Contracts.Terceros;
using Bastion.Terceros.Infrastructure.Persistencia;
using Bastion.Terceros.Infrastructure.Persistencia.Repositorios;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Cruces;

/// <summary>
/// <c>IConsultaDeTerceros</c> contra PostgreSQL: qué estado contesta de cada ficha, y a qué
/// ficha deja pasar el filtro del listado.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué el puerto y no la API.</b> El alta de un proveedor contesta el MISMO 400 a todo lo
/// que no autoriza, a propósito, así que desde la API un puerto que confundiera «no existe» con
/// «no hace ese papel» sería indistinguible de uno correcto. Lo que decide cada estado es una
/// consulta con dos filtros —empresa y bloqueo—, y eso solo se ve preguntándole a la consulta.
/// </para>
/// <para>
/// <b>Las fichas se dan de alta por la API y se preguntan por el adaptador.</b> Montarlas con SQL
/// probaría filas que el sistema no produce; y preguntar por la API, como se ha dicho, no dejaría
/// ver la respuesta. El contexto se abre como el de una petición ordinaria: con su empresa y con el
/// acceso a lo bloqueado cerrado.
/// </para>
/// <para>
/// <b>Y aquí se encontró que <c>Bloqueado</c> sobraba.</b> El tercer caso se escribió primero
/// afirmando que un bloqueado contesta <c>Bloqueado</c>, y salió rojo: <c>NoExiste</c>. El valor se
/// quitó del enumerado y el caso se quedó afirmando lo que la base contesta, con su contraria —al
/// desbloquearlo vuelve a estar disponible— para que no lo pase un puerto que conteste
/// <c>NoExiste</c> a todo.
/// </para>
/// <para>
/// <b>Semillas del bloque 200-205</b>, y los terceros del 32 000 001 al 32 000 009; el reparto está
/// en <c>ContratoDeLosCrucesTests</c>, que tiene el resto del bloque.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElPuertoDeTercerosContraLaBaseTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Terceros = "/api/v1/terceros/terceros";

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
    /// Un proveedor está <c>Disponible</c> como proveedor, y el papel por el que se pregunta no es
    /// decorativo.
    /// </summary>
    /// <remarks>
    /// Las dos casillas con su contraria, sobre las MISMAS dos fichas: la que solo compra y la que
    /// solo vende, preguntadas por los dos papeles. Un adaptador que ignorara el parámetro
    /// contestaría lo mismo en las dos direcciones de al menos una de ellas.
    /// </remarks>
    [Fact]
    [CubreEstadoDelPuerto(typeof(IConsultaDeTerceros), EstadoDelTercero.Disponible)]
    [CubreEstadoDelPuerto(typeof(IConsultaDeTerceros), EstadoDelTercero.NoHaceEseRol)]
    public async Task El_papel_por_el_que_se_pregunta_decide_entre_Disponible_y_NoHaceEseRol()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(200);

        TerceroDto proveedor = await CrearAsync(
            cliente, 32_000_001, esCliente: false, esProveedor: true);

        TerceroDto soloCliente = await CrearAsync(
            cliente, 32_000_002, esCliente: true, esProveedor: false);

        await using TercerosDbContext contexto = postgres.AbrirTerceros(empresa.Id);
        ConsultaDeTerceros puerto = new(contexto);

        (await puerto.EstadoDeAsync(proveedor.Id, RolDeTercero.Proveedor, CancellationToken.None))
            .ShouldBe(EstadoDelTercero.Disponible);

        (await puerto.EstadoDeAsync(proveedor.Id, RolDeTercero.Cliente, CancellationToken.None))
            .ShouldBe(
                EstadoDelTercero.NoHaceEseRol,
                "la ficha solo compra, y se ha dado por buena para venderle");

        (await puerto.EstadoDeAsync(soloCliente.Id, RolDeTercero.Proveedor, CancellationToken.None))
            .ShouldBe(
                EstadoDelTercero.NoHaceEseRol,
                "la ficha solo vende, y se ha dado por buena para colgarle un suministro");

        (await puerto.EstadoDeAsync(soloCliente.Id, RolDeTercero.Cliente, CancellationToken.None))
            .ShouldBe(EstadoDelTercero.Disponible);
    }

    /// <summary>
    /// Uno inventado y uno de OTRA empresa contestan <c>NoExiste</c>, y el de otra empresa existe
    /// en la suya.
    /// </summary>
    /// <remarks>
    /// La contraria es la que impide el falso verde: sin la última pregunta, un adaptador que
    /// contestara <c>NoExiste</c> a todo pasaría las dos primeras. Y es la R8 dicha como un
    /// estado: el filtro de empresa no es una optimización de esta consulta, es la comprobación.
    /// </remarks>
    [Fact]
    [CubreEstadoDelPuerto(typeof(IConsultaDeTerceros), EstadoDelTercero.NoExiste)]
    public async Task Uno_inventado_y_uno_de_otra_empresa_NoExisten_y_el_ajeno_si_en_la_suya()
    {
        (HttpClient _, EmpresaDto propia) = await EnUnaEmpresaNuevaAsync(201);
        (HttpClient enLaAjena, EmpresaDto ajena) = await EnUnaEmpresaNuevaAsync(202);

        TerceroDto deLaOtra = await CrearAsync(
            enLaAjena, 32_000_003, esCliente: false, esProveedor: true);

        await using (TercerosDbContext contexto = postgres.AbrirTerceros(propia.Id))
        {
            ConsultaDeTerceros puerto = new(contexto);

            (await puerto.EstadoDeAsync(
                    Guid.CreateVersion7(), RolDeTercero.Proveedor, CancellationToken.None))
                .ShouldBe(EstadoDelTercero.NoExiste);

            (await puerto.EstadoDeAsync(deLaOtra.Id, RolDeTercero.Proveedor, CancellationToken.None))
                .ShouldBe(
                    EstadoDelTercero.NoExiste,
                    "un tercero de otra empresa de la instalación se ve desde esta. Un suministro " +
                    "podría colgar de él y la R8 quedaría rota por dentro, sin clave ajena que " +
                    "chistara");
        }

        await using TercerosDbContext enSuEmpresa = postgres.AbrirTerceros(ajena.Id);

        (await new ConsultaDeTerceros(enSuEmpresa)
                .EstadoDeAsync(deLaOtra.Id, RolDeTercero.Proveedor, CancellationToken.None))
            .ShouldBe(EstadoDelTercero.Disponible, "en su propia empresa sí existe");
    }

    /// <summary>
    /// Un tercero bloqueado contesta <c>NoExiste</c> desde fuera, y al desbloquearlo vuelve a estar
    /// <c>Disponible</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La casilla <c>NoExiste</c> por su tercera situación</b>, que es la del art. 32: el filtro
    /// del bloqueo esconde la ficha y el puerto no sabe —ni puede contar— que la hay.
    /// </para>
    /// <para>
    /// <b>La contraria va en el mismo caso y sobre la misma ficha</b>, porque es la que dice que lo
    /// que la escondía era el bloqueo y no otra cosa: sin ella, un puerto roto que contestara
    /// <c>NoExiste</c> a toda pregunta pasaría este caso igual de verde.
    /// </para>
    /// </remarks>
    [Fact]
    [CubreEstadoDelPuerto(typeof(IConsultaDeTerceros), EstadoDelTercero.NoExiste)]
    public async Task Un_bloqueado_NoExiste_desde_fuera_y_desbloquearlo_lo_devuelve_Disponible()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(203);

        TerceroDto tercero = await CrearAsync(
            cliente, 32_000_004, esCliente: false, esProveedor: true);

        await BloquearAsync(cliente, tercero.Id);

        await using (TercerosDbContext contexto = postgres.AbrirTerceros(empresa.Id))
        {
            (await new ConsultaDeTerceros(contexto)
                    .EstadoDeAsync(tercero.Id, RolDeTercero.Proveedor, CancellationToken.None))
                .ShouldBe(
                    EstadoDelTercero.NoExiste,
                    "desde fuera de Terceros se ve que la ficha bloqueada existe. Cualquier módulo " +
                    "que guarde el identificador de un tercero podría colgarle algo nuevo, o " +
                    "contar quién tiene sus datos reservados");
        }

        await DesbloquearAsync(cliente, tercero.Id);

        await using TercerosDbContext despues = postgres.AbrirTerceros(empresa.Id);

        (await new ConsultaDeTerceros(despues)
                .EstadoDeAsync(tercero.Id, RolDeTercero.Proveedor, CancellationToken.None))
            .ShouldBe(
                EstadoDelTercero.Disponible,
                "desbloqueado, la ficha sigue sin verse: lo que la escondía no era el bloqueo");
    }

    /// <summary>
    /// De un conjunto, se pueden tratar los que no están bloqueados, son de esta empresa y existen
    /// — hagan el papel que hagan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Cinco identificadores, uno por cada motivo de estar o no estar</b>: un proveedor, un
    /// cliente que no compra nada —que SÍ entra, porque el listado de un artículo enseña el
    /// suministro de quien dejó de ser proveedor—, un bloqueado, uno de otra empresa y uno inventado.
    /// </para>
    /// <para>
    /// <b>Es el caso que lleva la mutación del filtro del bloqueo por el lado del conjunto.</b> El
    /// <c>WHERE</c> no repite el filtro a mano, así que esquivar el filtro aquí devuelve al bloqueado
    /// y este caso lo dice.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task De_un_conjunto_se_tratan_los_de_aqui_no_bloqueados_hagan_el_papel_que_hagan()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(204);
        (HttpClient enLaAjena, EmpresaDto _) = await EnUnaEmpresaNuevaAsync(205);

        TerceroDto proveedor = await CrearAsync(
            cliente, 32_000_005, esCliente: false, esProveedor: true);

        TerceroDto soloCliente = await CrearAsync(
            cliente, 32_000_006, esCliente: true, esProveedor: false);

        TerceroDto bloqueado = await CrearAsync(
            cliente, 32_000_007, esCliente: false, esProveedor: true);

        TerceroDto deLaOtra = await CrearAsync(
            enLaAjena, 32_000_008, esCliente: false, esProveedor: true);

        await BloquearAsync(cliente, bloqueado.Id);

        await using TercerosDbContext contexto = postgres.AbrirTerceros(empresa.Id);

        IReadOnlySet<Guid> tratables = await new ConsultaDeTerceros(contexto).CualesSePuedenTratarAsync(
            [proveedor.Id, soloCliente.Id, bloqueado.Id, deLaOtra.Id, Guid.CreateVersion7()],
            CancellationToken.None);

        tratables.OrderBy(id => id).ShouldBe(
            new[] { proveedor.Id, soloCliente.Id }.OrderBy(id => id),
            "el conjunto no deja pasar justo a los dos que se pueden tratar. Si sobra el bloqueado, " +
            "el listado de proveedores de un artículo enseña a quien tiene sus datos reservados; si " +
            "sobra el de otra empresa, la R8 está rota por la lectura; y si falta el que solo vende, " +
            "el listado esconde un suministro que existe");
    }

    private async Task<(HttpClient Cliente, EmpresaDto Empresa)> EnUnaEmpresaNuevaAsync(int semilla)
    {
        (HttpClient cliente, EmpresaDto empresa) =
            await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(semilla));

        _clientes.Add(cliente);

        return (cliente, empresa);
    }

    private static async Task<TerceroDto> CrearAsync(
        HttpClient cliente,
        int numero,
        bool esCliente,
        bool esProveedor)
    {
        CrearTerceroDto alta = new()
        {
            Identificacion = new IdentificacionDeAltaDto
            {
                Pais = "ES",
                Numero = Escenario.NifInventado(numero),
            },
            RazonSocial = "Tercero de prueba",
            DomicilioFiscal = Escenario.Domicilio(),
            EsCliente = esCliente,
            EsProveedor = esProveedor,
        };

        using HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Terceros, alta);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(respuesta));

        return (await respuesta.Content.ReadFromJsonAsync<TerceroDto>())!;
    }

    private static async Task BloquearAsync(HttpClient cliente, Guid id)
    {
        using HttpResponseMessage bloqueo = await cliente.SuprimirAsync($"{Terceros}/{id}");

        bloqueo.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(bloqueo));
    }

    private static async Task DesbloquearAsync(HttpClient cliente, Guid id)
    {
        // Sin If-Match: un bloqueado no se puede leer, así que no hay versión que mandar (ADR-0027).
        using HttpResponseMessage desbloqueo = await cliente.EnviarConVersionAsync(
            HttpMethod.Post, $"{Terceros}/{id}/desbloqueo", etiqueta: null);

        desbloqueo.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(desbloqueo));
    }
}
