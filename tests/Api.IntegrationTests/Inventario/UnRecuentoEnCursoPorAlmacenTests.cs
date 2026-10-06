using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Application.Recuentos;
using Bastion.Inventario.Contracts.Recuentos;
using Microsoft.EntityFrameworkCore.Storage;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Un solo recuento en curso por almacén (ADR-0055 §1.7): el segundo es un <c>409</c>, también
/// cuando las dos altas llegan a la vez, y otro almacén se cuenta al mismo tiempo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo guardan dos cosas, y cada una tiene su caso.</b> La comprobación previa contesta el caso
/// de todos los días con un <c>Resultado</c>; el índice único parcial contesta la carrera, en la que
/// la segunda alta no ve la fila que la primera todavía no ha publicado. Por la API las dos dan el
/// mismo <c>409</c>, así que quitar la comprobación no pondría rojo ningún caso del borde: la ve el
/// caso de uso solo, sin el manejador que traduce el índice, que sin ella lanzaría.
/// </para>
/// <para>
/// <b>En la carrera, la primera ya ha escrito, y esa espera es el sujeto.</b> La regla de parar la
/// primera transacción con nada escrito es para cuando la segunda espera un cerrojo; aquí lo que se
/// prueba es justo que la segunda se pare en el índice, detrás de una fila sin publicar, y que el
/// desenlace dependa de lo que haga la primera: si confirma, <c>409</c>; si deshace, <c>201</c>.
/// </para>
/// <para>
/// <b>Semillas: del 773 al 776</b>, empresas y maestros con el mismo número. El reparto del bloque
/// del 2.12 está en la cabecera de <c>ElPuertoDelArticuloContraLaBaseTests</c>.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class UnRecuentoEnCursoPorAlmacenTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string TipoDeUnoEnCurso = "/errors/recuento-ya-hay-uno-en-curso";

    private readonly ApiDeVerdad _api = new(postgres);
    private readonly List<HttpClient> _clientes = [];

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (HttpClient cliente in _clientes)
        {
            cliente.Dispose();
        }

        _api.Dispose();
    }

    /// <summary>
    /// Con un recuento en curso, el almacén no admite otro; otro almacén de la misma empresa sí.
    /// </summary>
    [Fact]
    public async Task Un_almacen_con_un_recuento_en_curso_no_admite_otro_y_otro_almacen_si()
    {
        EscenaDeRecuento escena = await MontarAsync(773, "RCU-S");
        EscenaDeTransferencia de = escena.Escena;

        RecuentoDto primero = await escena.AbrirAsync(de.AlmacenA);

        using (HttpResponseMessage segundo = await escena.AbrirPorLaApiAsync(escena.Peticion(de.AlmacenA)))
        {
            segundo.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(segundo));
            (await EscenaDeTransferencia.TipoDelProblemaAsync(segundo)).ShouldBe(TipoDeUnoEnCurso);
        }

        RecuentoDto deOtroAlmacen = await escena.AbrirAsync(de.AlmacenB);

        (await EnCursoDeAsync(escena, de.AlmacenA)).ShouldBe([primero.Id]);
        (await EnCursoDeAsync(escena, de.AlmacenB)).ShouldBe([deOtroAlmacen.Id]);
    }

    /// <summary>
    /// Dos altas del mismo almacén a la vez: la segunda pasa la comprobación previa, se para en el
    /// índice detrás de la primera y, cuando la primera confirma, es un <c>409</c>.
    /// </summary>
    [Fact]
    public async Task Dos_altas_a_la_vez_del_mismo_almacen_dejan_una_y_la_otra_es_un_409()
    {
        EscenaDeRecuento escena = await MontarAsync(774, "RCU-C");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 3m);

        await using ElModuloDeInventario modulo = new(postgres, de.EmpresaId);

        // TRANSACCIÓN 1: el alta escribe el recuento y sus líneas, y NO suelta.
        await using IDbContextTransaction abriendo = await modulo.AbrirTransaccionAsync();

        Resultado<RecuentoDto> primera =
            await modulo.AltaDeRecuento.EjecutarAsync(escena.Peticion(de.AlmacenA), CancellationToken.None);

        primera.EsCorrecto.ShouldBeTrue($"«{primera.Error?.Codigo}»");

        // TRANSACCIÓN 2: la misma alta por la API. No ve la fila de la primera, así que la
        // comprobación previa la deja pasar, y su `INSERT` se para en el índice.
        Task<HttpResponseMessage> segunda = escena.AbrirPorLaApiAsync(escena.Peticion(de.AlmacenA));

        await EsperarAQueLaFreneAsync(modulo.ProcesoDeLaBase, segunda);

        await abriendo.CommitAsync();

        using HttpResponseMessage respuesta = await segunda.WaitAsync(TimeSpan.FromSeconds(30));

        // SIN EL ÍNDICE ESTO ES UN 201, y el almacén se queda con dos recuentos en curso: dos
        // conteos del mismo hueco que confirmarían cada uno su diferencia.
        respuesta.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(respuesta));
        (await EscenaDeTransferencia.TipoDelProblemaAsync(respuesta)).ShouldBe(
            TipoDeUnoEnCurso, "el índice se traduce al mismo error que la comprobación previa");

        (await EnCursoDeAsync(escena, de.AlmacenA)).ShouldBe([primera.Valor!.Id]);
    }

    /// <summary>
    /// La misma carrera, con la primera alta deshecha: la segunda, que esperaba en el índice, abre.
    /// </summary>
    [Fact]
    public async Task Si_la_primera_alta_se_deshace_la_segunda_abre()
    {
        EscenaDeRecuento escena = await MontarAsync(775, "RCU-R");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 3m);

        await using ElModuloDeInventario modulo = new(postgres, de.EmpresaId);
        await using IDbContextTransaction abriendo = await modulo.AbrirTransaccionAsync();

        Resultado<RecuentoDto> primera =
            await modulo.AltaDeRecuento.EjecutarAsync(escena.Peticion(de.AlmacenA), CancellationToken.None);

        primera.EsCorrecto.ShouldBeTrue($"«{primera.Error?.Codigo}»");

        Task<HttpResponseMessage> segunda = escena.AbrirPorLaApiAsync(escena.Peticion(de.AlmacenA));

        await EsperarAQueLaFreneAsync(modulo.ProcesoDeLaBase, segunda);

        await abriendo.RollbackAsync();

        using HttpResponseMessage respuesta = await segunda.WaitAsync(TimeSpan.FromSeconds(30));

        // ES LA PAREJA DEL 409 DE LA CARRERA: la segunda no decide al llegar, espera a ver qué hace
        // la primera, y una primera deshecha no ha existido nunca. Un alta que contestara 409 en
        // cuanto encontrara a otra en vuelo daría el mismo rojo en la carrera y aquí no abriría.
        respuesta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(respuesta));

        RecuentoDto abierto = (await respuesta.Content.ReadFromJsonAsync<RecuentoDto>())!;

        abierto.Lineas.ShouldBe(1);
        (await EnCursoDeAsync(escena, de.AlmacenA)).ShouldBe([abierto.Id]);
    }

    /// <summary>
    /// La comprobación previa contesta sola: el caso de uso, sin el manejador del borde que traduce
    /// el índice, devuelve el error en vez de lanzar, y no escribe nada.
    /// </summary>
    [Fact]
    public async Task La_comprobacion_previa_contesta_sola_sin_el_borde_que_traduce_el_indice()
    {
        EscenaDeRecuento escena = await MontarAsync(776, "RCU-P");
        EscenaDeTransferencia de = escena.Escena;

        RecuentoDto primero = await escena.AbrirAsync(de.AlmacenA);

        await using ElModuloDeInventario modulo = new(postgres, de.EmpresaId);

        // SIN LA COMPROBACIÓN, ESTO LANZA el `DbUpdateException` del índice: aquí no hay nadie que
        // lo traduzca, y por la API saldría el mismo 409 por el otro camino.
        Resultado<RecuentoDto> segunda =
            await modulo.AltaDeRecuento.EjecutarAsync(escena.Peticion(de.AlmacenA), CancellationToken.None);

        segunda.EsCorrecto.ShouldBeFalse();
        segunda.Error!.Codigo.ShouldBe(ErroresDeRecuento.YaHayUnoEnCurso().Codigo);

        (await EnCursoDeAsync(escena, de.AlmacenA)).ShouldBe([primero.Id]);
    }

    private static async Task<IReadOnlyList<Guid>> EnCursoDeAsync(EscenaDeRecuento escena, Guid almacenId)
    {
        using HttpResponseMessage lectura =
            await escena.Cliente.GetAsync($"{EscenaDeRecuento.Recuentos}?almacen={almacenId}&estado=EnCurso");

        PaginaDe<RecuentoResumenDto> pagina =
            await EscenaDeTransferencia.LeerAsync<PaginaDe<RecuentoResumenDto>>(lectura);

        return [.. pagina.Elementos.Select(recuento => recuento.Id)];
    }

    private Task EsperarAQueLaFreneAsync(int procesoQueFrena, Task enVuelo) =>
        LaEspera.AQueLaFreneAsync(
            postgres.CadenaDeConexion,
            procesoQueFrena,
            enVuelo,
            "el alta en vuelo",
            "ha escrito su recuento sin chocar con el índice de uno en curso por almacén");

    private async Task<EscenaDeRecuento> MontarAsync(int semilla, string codigo)
    {
        EscenaDeRecuento escena = await EscenaDeRecuento.MontarAsync(_api, semilla, codigo);

        _clientes.Add(escena.Cliente);

        return escena;
    }
}
