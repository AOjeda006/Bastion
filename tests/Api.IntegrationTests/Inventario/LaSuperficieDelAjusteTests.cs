using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Contracts.Recuentos;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// La superficie HTTP del ajuste que llega con su pantalla (ADR-0065 §6): el alta, el listado, la
/// ficha con su versión y el descarte, contra la API de verdad.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo que se prueba aquí es el viaje, no la regla.</b> El factor, el descarte y la forma de los
/// códigos ya se prueban contra el caso de uso, en sus ficheros. Lo que solo se ve por HTTP es lo que
/// lee la pantalla: la <c>Location</c> del alta y la falta de <c>ETag</c>, la clave obligatoria, el
/// tope de líneas con su motivo, la versión de la ficha que acepta el <c>DELETE</c>, y el
/// <c>actual</c> del <c>422</c> con los nombres con que viaja.
/// </para>
/// <para>
/// <b>Cada ausencia lleva su pareja</b>: lo que no sale en la lista salía antes de descartarlo, lo que
/// la otra empresa no ve lo ve la suya, y el alta rechazada pasa con lo que le faltaba.
/// </para>
/// <para>
/// <b>Semillas: de la 887 a la 895</b>, del bloque del 2.17 (su reparto está en la cabecera de
/// <c>ElFactorDelAjusteTests</c>), empresas y maestros con el mismo número; la 893 es solo una
/// empresa, la ajena. Del 896 al 898, libres.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaSuperficieDelAjusteTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Ajustes = "/api/v1/inventario/ajustes";
    private const string Motivo = "Regularización de un recuento";

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

    [Fact]
    public async Task El_alta_es_201_sin_etag_y_la_ficha_dice_el_factor_y_la_cantidad_base_de_cada_linea()
    {
        EscenaDeTransferencia escena = await MontarAsync(887, "SAJ-A");

        Guid caja = await LosMaestrosPorLaApi.CrearUnidadQueConvierteAAsync(
            escena.Cliente, "K887", 12m, escena.UnidadId);
        Guid tercio = await LosMaestrosPorLaApi.CrearUnidadQueConvierteAAsync(
            escena.Cliente, "K887T", 0.333333m, escena.UnidadId);

        using HttpResponseMessage alta = await AbrirAsync(
            escena,
            Peticion(
                escena,
                escena.AlmacenA,
                Motivo,
                Entrada(escena, 5m),
                Entrada(escena, 3m, caja),
                Entrada(escena, 2.5m, tercio)),
            Guid.NewGuid().ToString());

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        AjusteDto creado = (await alta.Content.ReadFromJsonAsync<AjusteDto>())!;

        creado.Estado.ShouldBe("Borrador");
        creado.Numero.ShouldBeNull("el borrador no numera");
        creado.Lineas.ShouldBe(3);

        // SIN ETAG, Y ES LA AFIRMACIÓN: con la clave obligatoria, la repetición devolvería la versión
        // de entonces (ADR-0057 §1). La versión se lee de la ficha, que es a donde lleva la Location.
        alta.Headers.ETag.ShouldBeNull("el alta no publica versión; la publica la ficha");
        alta.Headers.Location.ShouldNotBeNull().AbsolutePath.ShouldBe($"{Ajustes}/{creado.Id}");

        using HttpResponseMessage lectura = await escena.Cliente.GetAsync(alta.Headers.Location);

        FichaDeAjusteDto ficha = await EscenaDeTransferencia.LeerAsync<FichaDeAjusteDto>(lectura);

        lectura.Headers.ETag.ShouldNotBeNull("la ficha publica la versión que pide el descarte");

        ficha.Id.ShouldBe(creado.Id);
        ficha.Estado.ShouldBe("Borrador");
        ficha.AnulaAId.ShouldBeNull();
        ficha.RecuentoId.ShouldBeNull("lo abrió una persona, no un recuento");
        ficha.Lineas.Select(linea => linea.Numero).ShouldBe([1, 2, 3]);
        ficha.Lineas.Select(linea => linea.UnidadIntroducidaId).ShouldBe([escena.UnidadId, caja, tercio]);
        ficha.Lineas.Select(linea => linea.FactorAUnidadBase).ShouldBe(
            [1m, 12m, 0.333333m], "el factor lo puso el servidor, y la ficha lo enseña");

        // EL TERCIO ES EL QUE REDONDEA: 2,5 × 0,333333 son 0,8333325, que no cabe en seis decimales.
        // Sin el redondeo del libro, la ficha diría una cifra que no está en ninguna fila.
        ficha.Lineas.Select(linea => linea.CantidadEnUnidadBase).ShouldBe(
            [5m, 36m, 0.833333m], "cinco sueltas, tres cajas de doce y dos tercios y medio, redondeado");
        ficha.Lineas.ShouldAllBe(linea => linea.Valor == null, "el valor lo pone la confirmación");

        using (HttpResponseMessage confirmado = await escena.ConfirmarElAjustePorLaApiAsync(creado.Id))
        {
            confirmado.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(confirmado));
        }

        FichaDeAjusteDto confirmada = await FichaAsync(escena, creado.Id);
        IReadOnlyList<MovimientoStock> filas = await escena.FilasDelLibroAsync(postgres, creado.Id);

        confirmada.Estado.ShouldBe("Confirmado");
        confirmada.Numero.ShouldNotBeNull("confirmado, numera");

        // LO QUE DICE LA FICHA ES LO QUE SUMÓ EL LIBRO, fila a fila: las dos listas van ordenadas por
        // la cantidad, y las tres son entradas distintas.
        confirmada.Lineas.Select(linea => linea.CantidadEnUnidadBase).Order()
            .ShouldBe(filas.Select(fila => fila.CantidadEnUnidadBase));
        confirmada.Lineas.OrderBy(linea => linea.CantidadEnUnidadBase).Select(linea => linea.Valor)
            .ShouldBe(filas.Select(fila => (decimal?)fila.Valor.Cantidad), "el valor de cada línea, el de su fila");

        using HttpResponseMessage anulado = await escena.AnularElAjustePorLaApiAsync(creado.Id);

        AnulacionDto anulacion = await EscenaDeTransferencia.LeerAsync<AnulacionDto>(anulado);
        FichaDeAjusteDto inverso = await FichaAsync(escena, anulacion.Inverso.Id);

        inverso.AnulaAId.ShouldBe(creado.Id, "la ficha del inverso dice a quién anula");
        inverso.Lineas.Select(linea => linea.FactorAUnidadBase).ShouldBe([1m, 12m, 0.333333m]);
        inverso.Lineas.Select(linea => linea.CantidadEnUnidadBase).ShouldBe([-5m, -36m, -0.833333m]);
    }

    [Fact]
    public async Task Sin_idempotency_key_el_alta_es_428_y_con_la_misma_clave_no_abre_otro()
    {
        EscenaDeTransferencia escena = await MontarAsync(888, "SAJ-B");
        AbrirAjusteDto peticion = Peticion(escena, escena.AlmacenA, Motivo, Entrada(escena, 1m));

        using (HttpResponseMessage sinClave = await AbrirAsync(escena, peticion, clave: null))
        {
            sinClave.StatusCode.ShouldBe(HttpStatusCode.PreconditionRequired, await Escenario.Detalle(sinClave));
        }

        (await AjustesDeAsync(escena)).ShouldBe(0, "sin clave no se abre nada");

        // LA PAREJA, Y EL PORQUÉ DE LA OBLIGACIÓN: el móvil que reenvía el alta con su clave recibe el
        // mismo borrador, y no dos que se podrían confirmar los dos (ADR-0065 §6).
        string clave = Guid.NewGuid().ToString();

        using HttpResponseMessage primera = await AbrirAsync(escena, peticion, clave);
        using HttpResponseMessage repetida = await AbrirAsync(escena, peticion, clave);

        primera.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(primera));
        repetida.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(repetida));

        (await repetida.Content.ReadFromJsonAsync<AjusteDto>())!.Id
            .ShouldBe((await primera.Content.ReadFromJsonAsync<AjusteDto>())!.Id, "la repetición es la primera");

        (await AjustesDeAsync(escena)).ShouldBe(1, "un solo borrador para las dos peticiones");
    }

    [Fact]
    public async Task Doscientas_lineas_abren_y_una_mas_es_400_con_el_motivo_del_tope()
    {
        EscenaDeTransferencia escena = await MontarAsync(889, "SAJ-C");
        LineaDeAjusteDto linea = Entrada(escena, 1m);

        using HttpResponseMessage justas = await AbrirAsync(
            escena,
            Peticion(escena, escena.AlmacenA, Motivo, [.. Enumerable.Repeat(linea, Paginacion.TamanioMaximo)]),
            Guid.NewGuid().ToString());

        justas.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(justas));
        (await justas.Content.ReadFromJsonAsync<AjusteDto>())!.Lineas.ShouldBe(Paginacion.TamanioMaximo);

        using HttpResponseMessage demasiadas = await AbrirAsync(
            escena,
            Peticion(escena, escena.AlmacenA, Motivo, [.. Enumerable.Repeat(linea, Paginacion.TamanioMaximo + 1)]),
            Guid.NewGuid().ToString());

        demasiadas.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await Escenario.Detalle(demasiadas));

        // UN SOLO CAMPO, CON SU MOTIVO, y sin mirar bajo qué clave: la pone el enlace de modelo, como
        // en todo 400 de una anotación (`ElCuerpoQueNoEncajaTests`), y no es la ruta de zod de los
        // rechazos de una línea. La pantalla no la necesita, porque no deja teclear la 201.
        JsonElement problema = await ProblemaAsync(demasiadas);

        problema.GetProperty("type").GetString().ShouldBe("/errors/datos-no-validos");
        problema.GetProperty("errors").EnumerateObject()
            .ShouldHaveSingleItem().Value.EnumerateArray().Select(motivo => motivo.GetString())
            .ShouldBe([$"Un ajuste lleva como mucho {Paginacion.TamanioMaximo} líneas."]);

        (await AjustesDeAsync(escena)).ShouldBe(1, "solo el de doscientas");
    }

    [Fact]
    public async Task La_lista_va_de_la_ultima_fecha_a_la_primera_se_acota_y_no_trae_el_descartado()
    {
        EscenaDeTransferencia escena = await MontarAsync(890, "SAJ-D");

        Guid ayer = await AbrirConLaClaveAsync(escena, escena.AlmacenA, "Rotura en el muelle");
        Guid deB = await AbrirConLaClaveAsync(escena, escena.AlmacenB, Motivo);
        Guid deA = await AbrirConLaClaveAsync(escena, escena.AlmacenA, Motivo);
        Guid tirado = await AbrirConLaClaveAsync(escena, escena.AlmacenA, Motivo);

        // TODAS LAS ALTAS SON DE HOY, así que la primera se lleva a ayer por debajo del ORM. Dentro del
        // día van por el identificador, que crece con el tiempo y no se invierte.
        await TocarAsync(ayer, "fecha_de_operacion = fecha_de_operacion - 1");

        // DE A PRIMERO, para que el orden por número no sea el de la lista.
        await ConfirmarAsync(escena, deA);
        await ConfirmarAsync(escena, deB);

        (await IdsAsync(escena, "")).ShouldBe([deB, deA, tirado, ayer], "antes de tirarlo, el borrador sale");

        using (HttpResponseMessage descarte = await DescartarAsync(
            escena, tirado, await escena.Cliente.EtiquetaDeAsync($"{Ajustes}/{tirado}")))
        {
            descarte.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(descarte));
        }

        PaginaDe<AjusteResumenDto> pagina = await ListarAsync(escena, "");

        pagina.Elementos.Select(ajuste => ajuste.Id).ShouldBe([deB, deA, ayer], "el descartado no sale (§5)");
        pagina.Total.ShouldBe(3, "ni cuenta");

        (await IdsAsync(escena, "?sort=fechaDeOperacion")).ShouldBe([ayer, deB, deA]);
        (await IdsAsync(escena, "?estado=Confirmado&sort=numero")).ShouldBe([deA, deB]);
        (await IdsAsync(escena, "?estado=Confirmado&sort=-numero")).ShouldBe([deB, deA]);
        (await IdsAsync(escena, "?estado=Borrador")).ShouldBe([ayer]);
        (await IdsAsync(escena, "?estado=Anulado")).ShouldBeEmpty();
        (await IdsAsync(escena, $"?almacen={escena.AlmacenB}")).ShouldBe([deB]);
        (await IdsAsync(escena, "?q=MUELLE")).ShouldBe([ayer], "el texto mira el motivo, sin mayúsculas");

        pagina.Elementos[^1].ShouldBe(new AjusteResumenDto(
            ayer, null, escena.AlmacenA, EscenaDeTransferencia.Hoy.AddDays(-1), "Borrador", "Rotura en el muelle"));
        pagina.Elementos[1].ShouldBe(new AjusteResumenDto(
            deA, 1, escena.AlmacenA, EscenaDeTransferencia.Hoy, "Confirmado", Motivo));

        await NoSeAdmiteAsync(escena, "?estado=Descartado", "/errors/datos-no-validos");
        await NoSeAdmiteAsync(escena, "?sort=motivo", "/errors/orden-no-admitido");
    }

    [Fact]
    public async Task El_descarte_pide_la_version_de_la_ficha_y_repetirlo_es_404()
    {
        EscenaDeTransferencia escena = await MontarAsync(891, "SAJ-E");

        Guid confirmado = await AbrirConLaClaveAsync(escena, escena.AlmacenA, Motivo);
        string deAntes = await escena.Cliente.EtiquetaDeAsync($"{Ajustes}/{confirmado}");

        using (HttpResponseMessage sinVersion = await DescartarAsync(escena, confirmado, etiqueta: null))
        {
            sinVersion.StatusCode.ShouldBe(HttpStatusCode.PreconditionRequired, await Escenario.Detalle(sinVersion));
        }

        await ConfirmarAsync(escena, confirmado);

        // LA VERSIÓN ANTES QUE EL ESTADO (ADR-0057 §3): con la de antes de confirmarlo, 412; con la de
        // ahora, el 409 de un ajuste que ya no es un borrador.
        using (HttpResponseMessage vieja = await DescartarAsync(escena, confirmado, deAntes))
        {
            vieja.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed, await Escenario.Detalle(vieja));
        }

        using (HttpResponseMessage deAhora = await DescartarAsync(
            escena, confirmado, await escena.Cliente.EtiquetaDeAsync($"{Ajustes}/{confirmado}")))
        {
            deAhora.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(deAhora));
            (await EscenaDeTransferencia.TipoDelProblemaAsync(deAhora)).ShouldBe("/errors/ajuste-no-esta-en-borrador");
        }

        Guid borrador = await AbrirConLaClaveAsync(escena, escena.AlmacenA, Motivo);
        string suVersion = await escena.Cliente.EtiquetaDeAsync($"{Ajustes}/{borrador}");

        using (HttpResponseMessage descarte = await DescartarAsync(escena, borrador, suVersion))
        {
            descarte.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(descarte));
            (await descarte.Content.ReadAsStringAsync()).ShouldBeEmpty("un 204 no lleva cuerpo");
        }

        using (HttpResponseMessage lectura = await escena.Cliente.GetAsync($"{Ajustes}/{borrador}"))
        {
            lectura.StatusCode.ShouldBe(HttpStatusCode.NotFound, "la ficha de un descartado no se encuentra");
        }

        using (HttpResponseMessage otraVez = await DescartarAsync(escena, borrador, suVersion))
        {
            otraVez.StatusCode.ShouldBe(HttpStatusCode.NotFound, "repetirlo da un 404, que es lo que es");
        }

        using HttpResponseMessage confirmarlo = await escena.ConfirmarElAjustePorLaApiAsync(borrador);

        confirmarlo.StatusCode.ShouldBe(HttpStatusCode.NotFound, await Escenario.Detalle(confirmarlo));
    }

    [Fact]
    public async Task Otra_empresa_no_ve_el_ajuste_ni_lo_descarta()
    {
        EscenaDeTransferencia escena = await MontarAsync(892, "SAJ-F");

        Guid propio = await AbrirConLaClaveAsync(escena, escena.AlmacenA, Motivo);
        string version = await escena.Cliente.EtiquetaDeAsync($"{Ajustes}/{propio}");

        (HttpClient ajena, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(893));
        _clientes.Add(ajena);

        using (HttpResponseMessage lectura = await ajena.GetAsync($"{Ajustes}/{propio}"))
        {
            lectura.StatusCode.ShouldBe(HttpStatusCode.NotFound, await Escenario.Detalle(lectura));
        }

        using (HttpResponseMessage lista = await ajena.GetAsync(Ajustes))
        {
            (await EscenaDeTransferencia.LeerAsync<PaginaDe<AjusteResumenDto>>(lista)).Total.ShouldBe(0);
        }

        using (HttpResponseMessage descarte = await ajena.EnviarConVersionAsync(
            HttpMethod.Delete, $"{Ajustes}/{propio}", version))
        {
            descarte.StatusCode.ShouldBe(HttpStatusCode.NotFound, await Escenario.Detalle(descarte));
        }

        // LA PAREJA: la suya lo ve, con la misma versión, porque nadie lo ha tocado (R8).
        (await escena.Cliente.EtiquetaDeAsync($"{Ajustes}/{propio}")).ShouldBe(version);
        (await IdsAsync(escena, "")).ShouldBe([propio]);
    }

    [Fact]
    public async Task Una_linea_sin_conversion_es_422_con_la_linea_y_las_dos_unidades_en_actual()
    {
        EscenaDeTransferencia escena = await MontarAsync(894, "SAJ-G");

        Guid caja = await LosMaestrosPorLaApi.CrearUnidadAsync(escena.Cliente, "K894");
        AbrirAjusteDto peticion = Peticion(
            escena, escena.AlmacenA, Motivo, Entrada(escena, 1m), Entrada(escena, 2m, caja));

        using (HttpResponseMessage rechazo = await AbrirAsync(escena, peticion, Guid.NewGuid().ToString()))
        {
            rechazo.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity, await Escenario.Detalle(rechazo));

            // CON LOS NOMBRES CON QUE VIAJA, que son los que lee la pantalla para señalar la línea y
            // ofrecer la conversión que falta (ADR-0065 §3).
            JsonElement problema = await ProblemaAsync(rechazo);
            JsonElement actual = problema.GetProperty("actual");

            problema.GetProperty("type").GetString().ShouldBe("/errors/ajuste-sin-conversion");
            actual.GetProperty("linea").GetInt32().ShouldBe(2, "desde uno, como la dicen los mensajes");
            actual.GetProperty("unidadIntroducidaId").GetGuid().ShouldBe(caja);
            actual.GetProperty("unidadBaseId").GetGuid().ShouldBe(escena.UnidadId);
        }

        // LA PAREJA: con su conversión, la misma petición abre.
        await LosMaestrosPorLaApi.CrearConversionAsync(escena.Cliente, caja, escena.UnidadId, 6m);

        using HttpResponseMessage alta = await AbrirAsync(escena, peticion, Guid.NewGuid().ToString());

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));
    }

    [Fact]
    public async Task La_ficha_del_ajuste_de_un_recuento_dice_de_que_recuento_es()
    {
        EscenaDeRecuento recuentos = await EscenaDeRecuento.MontarAsync(_api, 895, "SAJ-H");
        EscenaDeTransferencia escena = recuentos.Escena;
        _clientes.Add(escena.Cliente);

        Guid entrada = await recuentos.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 3m);

        RecuentoDto recuento = await recuentos.AbrirAsync(escena.AlmacenA);
        LineaDeRecuentoDto linea = (await recuentos.TodasLasLineasAsync(recuento.Id)).ShouldHaveSingleItem();

        await recuentos.ContarConSuVersionAsync(recuento.Id, linea.Id, 5m);
        await recuentos.ConfirmarConLaFichaDeAhoraAsync(recuento.Id);

        Guid delRecuento = await ElAjusteDelRecuentoAsync(escena, recuento.Id);
        FichaDeAjusteDto ficha = await FichaAsync(escena, delRecuento);

        // LA PANTALLA NO OFRECE ANULARLO, y lo sabe por aquí (ADR-0065 §8): se anula el recuento.
        ficha.RecuentoId.ShouldBe(recuento.Id);
        ficha.Lineas.ShouldHaveSingleItem().CantidadEnUnidadBase.ShouldBe(2m, "la diferencia: cinco contadas y tres");

        // LA PAREJA: la entrada que llenó el hueco la abrió una persona.
        (await FichaAsync(escena, entrada)).RecuentoId.ShouldBeNull();
    }

    private static LineaDeAjusteDto Entrada(EscenaDeTransferencia escena, decimal cantidad, Guid? unidadId = null) =>
        new(escena.UbicacionA, escena.ArticuloId, cantidad, unidadId ?? escena.UnidadId, 2m);

    private static AbrirAjusteDto Peticion(
        EscenaDeTransferencia escena, Guid almacenId, string motivo, params LineaDeAjusteDto[] lineas) =>
        new(escena.SerieDeAjustes.Id, almacenId, EscenaDeTransferencia.Hoy, motivo, lineas);

    private static Task<HttpResponseMessage> AbrirAsync(
        EscenaDeTransferencia escena, AbrirAjusteDto peticion, string? clave)
    {
        HttpRequestMessage alta = new(HttpMethod.Post, Ajustes) { Content = JsonContent.Create(peticion) };

        if (clave is not null)
        {
            alta.Headers.TryAddWithoutValidation("Idempotency-Key", clave);
        }

        return escena.Cliente.SendAsync(alta);
    }

    private static async Task<Guid> AbrirConLaClaveAsync(EscenaDeTransferencia escena, Guid almacenId, string motivo)
    {
        using HttpResponseMessage alta = await AbrirAsync(
            escena,
            Peticion(escena, almacenId, motivo, Entrada(escena, 1m) with
            {
                UbicacionId = almacenId == escena.AlmacenB ? escena.UbicacionB : escena.UbicacionA,
            }),
            Guid.NewGuid().ToString());

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<AjusteDto>())!.Id;
    }

    private static async Task ConfirmarAsync(EscenaDeTransferencia escena, Guid ajusteId)
    {
        using HttpResponseMessage confirmado = await escena.ConfirmarElAjustePorLaApiAsync(ajusteId);

        confirmado.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(confirmado));
    }

    private static Task<HttpResponseMessage> DescartarAsync(
        EscenaDeTransferencia escena, Guid ajusteId, string? etiqueta) =>
        escena.Cliente.EnviarConVersionAsync(HttpMethod.Delete, $"{Ajustes}/{ajusteId}", etiqueta);

    private static async Task<FichaDeAjusteDto> FichaAsync(EscenaDeTransferencia escena, Guid ajusteId)
    {
        using HttpResponseMessage lectura = await escena.Cliente.GetAsync($"{Ajustes}/{ajusteId}");

        return await EscenaDeTransferencia.LeerAsync<FichaDeAjusteDto>(lectura);
    }

    private static async Task<PaginaDe<AjusteResumenDto>> ListarAsync(EscenaDeTransferencia escena, string consulta)
    {
        using HttpResponseMessage lectura = await escena.Cliente.GetAsync(Ajustes + consulta);

        return await EscenaDeTransferencia.LeerAsync<PaginaDe<AjusteResumenDto>>(lectura);
    }

    private static async Task<IReadOnlyList<Guid>> IdsAsync(EscenaDeTransferencia escena, string consulta) =>
        [.. (await ListarAsync(escena, consulta)).Elementos.Select(ajuste => ajuste.Id)];

    private static async Task NoSeAdmiteAsync(EscenaDeTransferencia escena, string consulta, string tipo)
    {
        using HttpResponseMessage lectura = await escena.Cliente.GetAsync(Ajustes + consulta);

        lectura.StatusCode.ShouldBe(HttpStatusCode.BadRequest, $"{consulta}: {await Escenario.Detalle(lectura)}");
        (await EscenaDeTransferencia.TipoDelProblemaAsync(lectura)).ShouldBe(tipo);
    }

    private static async Task<JsonElement> ProblemaAsync(HttpResponseMessage respuesta)
    {
        using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        return problema.RootElement.Clone();
    }

    // UNA ESCRITURA POR DEBAJO DEL ORM, que mueve lo que diga y nada más.
    private async Task TocarAsync(Guid ajusteId, string cambio)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new($"UPDATE inventario.ajustes SET {cambio} WHERE id = @id", conexion);
        orden.Parameters.AddWithValue("id", ajusteId);

        (await orden.ExecuteNonQueryAsync()).ShouldBe(1, $"ajuste {ajusteId}");
    }

    private async Task<int> AjustesDeAsync(EscenaDeTransferencia escena)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId);

        return await contexto.Ajustes.CountAsync();
    }

    private async Task<Guid> ElAjusteDelRecuentoAsync(EscenaDeTransferencia escena, Guid recuentoId)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId);

        return await contexto.Ajustes
            .Where(ajuste => ajuste.RecuentoId == recuentoId)
            .Select(ajuste => ajuste.Id)
            .SingleAsync();
    }

    private async Task<EscenaDeTransferencia> MontarAsync(int semilla, string codigo)
    {
        EscenaDeTransferencia escena = await EscenaDeTransferencia.MontarAsync(_api, semilla, codigo, semilla);

        _clientes.Add(escena.Cliente);

        return escena;
    }
}
