using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Organizacion.Contracts.Divisas;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Retiradas;

/// <summary>
/// La retirada y el bloqueo se parecen en todo menos en la línea que los separa: la fila retirada
/// <b>se sigue devolviendo</b> por su identificador, y la bloqueada responde como si no existiera.
/// </summary>
/// <remarks>
/// <para>
/// <b>Esa línea es la que se pierde primero.</b> Las dos cosas quitan una fila de en medio sin
/// borrarla, las dos son reversibles, y las dos se leen igual en un diagrama. Al que venga después
/// le va a parecer natural «unificar» los dos comportamientos, y el 404 es lo más fácil de copiar
/// porque ya está escrito para el bloqueo. Pero son dos cosas distintas por el MOTIVO: el bloqueo
/// del artículo 32 esconde datos personales, y esconderlos es su función; la retirada de una
/// divisa no esconde nada —una divisa no tiene datos de nadie—, solo deja de ofrecerla. Una
/// factura emitida en pesetas tiene que poder seguir diciendo en qué se emitió mucho después de
/// que nadie pueda emitir una nueva, y un 404 se lo impediría.
/// </para>
/// <para>
/// <b>Y por eso el ADR-0027 no se aplica aquí.</b> Lo que hace del listado de lo bloqueado un
/// extremo aparte, nominativo y trazado, es que expone datos del artículo 32. Ver las divisas
/// retiradas no expone nada de nadie, así que se pide con un parámetro del listado de siempre.
/// </para>
/// <para>
/// Se ejercita por HTTP y contra PostgreSQL de verdad porque las dos mitades que se comprueban
/// —qué devuelve el <c>GET</c> y qué trae la colección— las decide una consulta traducida a SQL,
/// no una rama en C#.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaRetiradaNoEsUnBloqueoTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Divisas = "/api/v1/organizacion/divisas";

    private readonly ApiDeVerdad _api = new(postgres);

    public void Dispose() => _api.Dispose();

    /// <summary>
    /// La diferencia con el bloqueo, entera: retirada, la fila sigue saliendo por su
    /// identificador, y lo dice.
    /// </summary>
    [Fact]
    public async Task El_GET_por_identificador_sigue_devolviendo_la_fila_retirada()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(150));
        using HttpClient suyo = cliente;

        DivisaDto divisa = await CrearAsync(cliente, "CHF", "Franco suizo");

        DivisaDto antes = await LeerAsync(cliente, divisa.Id);
        antes.Retirada.ShouldBeFalse(
            "recién dada de alta no está retirada: sin esta mitad, un `Retirada` cableado a " +
            "`true` pasaría la aserción de abajo");

        await RetirarAsync(cliente, divisa.Id);

        // La línea que separa la retirada del bloqueo. Un 404 aquí sería el comportamiento del
        // artículo 32 aplicado a algo que no tiene datos personales, y dejaría a toda factura
        // emitida en esta divisa sin poder decir en qué se emitió.
        using HttpResponseMessage lectura = await cliente.GetAsync($"{Divisas}/{divisa.Id}");

        lectura.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            "una divisa retirada NO desaparece: se sigue devolviendo por su identificador. Eso " +
            "es lo que la distingue de una fila bloqueada, que responde 404 a propósito. " +
            await Escenario.Detalle(lectura));

        DivisaDto despues = (await lectura.Content.ReadFromJsonAsync<DivisaDto>())!;

        despues.Id.ShouldBe(divisa.Id, "es la misma fila, no una copia");
        despues.Codigo.ShouldBe(antes.Codigo);
        despues.Retirada.ShouldBeTrue(
            "y lo dice: sin el campo, quien la lee no puede distinguir una divisa retirada de " +
            "una que se ofrece, y la seguiría eligiendo para una factura nueva");
    }

    /// <summary>La colección la excluye por omisión, y la trae si se le pide.</summary>
    /// <remarks>
    /// Las dos mitades juntas, y no de una en una: un listado que devolviera siempre todo pasaría
    /// la segunda, y uno que no devolviera nunca lo retirado pasaría la primera. La misma fila
    /// tiene que faltar en una llamada y estar en la otra.
    /// </remarks>
    [Fact]
    public async Task La_coleccion_excluye_lo_retirado_por_omision_y_lo_trae_al_pedirlo()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(151));
        using HttpClient suyo = cliente;

        DivisaDto divisa = await CrearAsync(cliente, "GBP", "Libra esterlina");

        (await EstaEnLaPaginaAsync(cliente, divisa.Id, retiradas: false)).ShouldBeTrue(
            "antes de retirarla sale en la colección, que es lo que hace que su ausencia de " +
            "después signifique algo");

        await RetirarAsync(cliente, divisa.Id);

        (await EstaEnLaPaginaAsync(cliente, divisa.Id, retiradas: false)).ShouldBeFalse(
            "por omisión la colección no ofrece lo retirado: es la lista con la que se elige " +
            "para una operación nueva");

        (await EstaEnLaPaginaAsync(cliente, divisa.Id, retiradas: true)).ShouldBeTrue(
            "y hay una manera de verlas, porque «excluye por omisión» solo significa algo si " +
            "existe la otra mitad: sin ella, retirar sería esconder");
    }

    /// <summary>Reincorporar deshace la retirada sobre la MISMA fila.</summary>
    /// <remarks>
    /// Los cuatro maestros son de instalación (R8): retirar por error deja a todas las empresas
    /// sin esa divisa. Una retirada irreversible cambiaría un error permanente por otro, y por eso
    /// existe la puerta de vuelta.
    /// </remarks>
    [Fact]
    public async Task Reincorporar_la_vuelve_a_ofrecer_y_no_crea_una_fila_nueva()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(152));
        using HttpClient suyo = cliente;

        DivisaDto divisa = await CrearAsync(cliente, "USD", "Dólar estadounidense");

        await RetirarAsync(cliente, divisa.Id);
        await ReincorporarAsync(cliente, divisa.Id);

        DivisaDto vuelta = await LeerAsync(cliente, divisa.Id);

        vuelta.Id.ShouldBe(divisa.Id, "el identificador es el de siempre: se movió una columna");
        vuelta.Retirada.ShouldBeFalse();

        (await EstaEnLaPaginaAsync(cliente, divisa.Id, retiradas: false)).ShouldBeTrue(
            "vuelve a la lista con la que se elige, que es lo que significa reincorporarla");
    }

    /// <summary>Y el borrado no existe, por HTTP y no solo en la tabla de enrutado.</summary>
    /// <remarks>
    /// <c>NingunMaestroRetirableSeBorra</c> lo mira en la tabla de rutas, sin base de datos y en
    /// segundos. Esto lo mira desde fuera, que es donde lo mira un cliente: el verbo no está
    /// atendido. Las dos fuentes dicen lo mismo por caminos que no se derivan uno del otro.
    /// </remarks>
    [Fact]
    public async Task La_API_no_atiende_el_borrado_de_una_divisa()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(153));
        using HttpClient suyo = cliente;

        DivisaDto divisa = await CrearAsync(cliente, "JPY", "Yen japonés");

        using HttpResponseMessage borrado = await cliente.EnviarConVersionAsync(
            HttpMethod.Delete, $"{Divisas}/{divisa.Id}", etiqueta: null);

        borrado.StatusCode.ShouldBe(
            HttpStatusCode.MethodNotAllowed,
            "el ADR-0023 no admite ningún borrado para ninguno de los cuatro maestros, nunca: " +
            "son de instalación (R8) y todo lo emitido bajo ellos les apunta. " +
            await Escenario.Detalle(borrado));

        // Y la fila sigue donde estaba, que es lo que un 405 tiene que significar aquí.
        (await LeerAsync(cliente, divisa.Id)).Id.ShouldBe(divisa.Id);
    }

    private static async Task<DivisaDto> CrearAsync(HttpClient cliente, string codigo, string nombre)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Divisas, new CrearDivisaDto { Codigo = codigo, Nombre = nombre });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<DivisaDto>())!;
    }

    private static async Task<DivisaDto> LeerAsync(HttpClient cliente, Guid id)
    {
        using HttpResponseMessage lectura = await cliente.GetAsync($"{Divisas}/{id}");

        lectura.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(lectura));

        return (await lectura.Content.ReadFromJsonAsync<DivisaDto>())!;
    }

    private static async Task RetirarAsync(HttpClient cliente, Guid id)
    {
        using HttpResponseMessage retirada = await cliente.AccionarAsync(
            $"{Divisas}/{id}", $"{Divisas}/{id}/retirada", HttpMethod.Post);

        retirada.StatusCode.ShouldBe(
            HttpStatusCode.NoContent, await Escenario.Detalle(retirada));
    }

    private static async Task ReincorporarAsync(HttpClient cliente, Guid id)
    {
        using HttpResponseMessage vuelta = await cliente.AccionarAsync(
            $"{Divisas}/{id}", $"{Divisas}/{id}/retirada", HttpMethod.Delete);

        vuelta.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(vuelta));
    }

    /// <summary>
    /// Si la divisa sale en la colección, recorriéndola entera por si cae en otra página.
    /// </summary>
    /// <remarks>
    /// Se pagina de verdad y no se pide una página grande: los tests de esta colección comparten
    /// base, así que cuántas divisas hay depende de qué otros casos hayan corrido antes. Una
    /// aserción sobre la primera página sería verde o roja según el orden de ejecución.
    /// </remarks>
    private static async Task<bool> EstaEnLaPaginaAsync(HttpClient cliente, Guid id, bool retiradas)
    {
        for (int pagina = 1; ; pagina++)
        {
            string ruta = $"{Divisas}?page={pagina}&size=50"
                + (retiradas ? "&retiradas=true" : string.Empty);

            using HttpResponseMessage respuesta = await cliente.GetAsync(ruta);

            respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(respuesta));

            PaginaDe<DivisaDto> trozo =
                (await respuesta.Content.ReadFromJsonAsync<PaginaDe<DivisaDto>>())!;

            if (trozo.Elementos.Any(divisa => divisa.Id == id))
            {
                return true;
            }

            if (trozo.Elementos.Count == 0 || pagina * 50 >= trozo.Total)
            {
                return false;
            }
        }
    }
}
