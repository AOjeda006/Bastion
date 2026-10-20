using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// La superficie HTTP de la transferencia que llega con su pantalla (ADR-0068 §1): el alta, el
/// listado, la ficha con su versión y el descarte, contra la API de verdad.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo que se prueba aquí es el viaje, no la regla</b>, como en la del ajuste. El factor, el
/// descarte y los rechazos de cada línea ya se prueban contra el caso de uso, en sus ficheros. Lo que
/// solo se ve por HTTP es lo que lee la pantalla: la <c>Location</c> del alta y la falta de
/// <c>ETag</c>, la clave obligatoria, el tope de líneas con su motivo, los dos filtros de almacén, la
/// versión de la ficha que acepta el <c>DELETE</c>, y el <c>errors</c> y el <c>actual</c> de una
/// línea con los nombres con que viajan, la punta incluida.
/// </para>
/// <para>
/// <b>Cada ausencia lleva su pareja</b>: lo que no sale en la lista salía antes de descartarlo, lo que
/// la otra empresa no ve lo ve la suya, y el alta rechazada pasa con lo que le faltaba.
/// </para>
/// <para>
/// <b>Semillas: de la 914 a la 922</b>, del bloque del 2.19 (su reparto está en la cabecera de
/// <c>ElFactorDeLaTransferenciaTests</c>), empresas y maestros con el mismo número; la 920 es solo
/// una empresa, la ajena.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaSuperficieDeLaTransferenciaTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Transferencias = EscenaDeTransferencia.Transferencias;

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
    public async Task El_alta_es_201_sin_etag_y_la_ficha_dice_las_dos_ubicaciones_el_factor_y_la_cantidad_base()
    {
        EscenaDeTransferencia escena = await MontarAsync(914, "STR-A");

        Guid caja = await LosMaestrosPorLaApi.CrearUnidadQueConvierteAAsync(
            escena.Cliente, "K914", 12m, escena.UnidadId);
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 50m, 2m);

        using HttpResponseMessage alta = await AbrirAsync(
            escena,
            Peticion(escena, escena.AlmacenA, escena.AlmacenB, DeAaB(escena, 5m), DeAaB(escena, 3m, caja)),
            Guid.NewGuid().ToString());

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        TransferenciaDto creada = (await alta.Content.ReadFromJsonAsync<TransferenciaDto>())!;

        creada.Estado.ShouldBe("Borrador");
        creada.Numero.ShouldBeNull("el borrador no numera");
        creada.Lineas.ShouldBe(2);

        // SIN ETAG, Y ES LA AFIRMACIÓN: con la clave obligatoria, la repetición devolvería la versión
        // de entonces (ADR-0057 §1). La versión se lee de la ficha, que es a donde lleva la Location.
        alta.Headers.ETag.ShouldBeNull("el alta no publica versión; la publica la ficha");
        alta.Headers.Location.ShouldNotBeNull().AbsolutePath.ShouldBe($"{Transferencias}/{creada.Id}");

        using HttpResponseMessage lectura = await escena.Cliente.GetAsync(alta.Headers.Location);

        FichaDeTransferenciaDto ficha = await EscenaDeTransferencia.LeerAsync<FichaDeTransferenciaDto>(lectura);

        lectura.Headers.ETag.ShouldNotBeNull("la ficha publica la versión que pide el descarte");

        ficha.Id.ShouldBe(creada.Id);
        (ficha.AlmacenOrigenId, ficha.AlmacenDestinoId).ShouldBe((escena.AlmacenA, escena.AlmacenB));
        ficha.Estado.ShouldBe("Borrador");
        ficha.FechaDeRecepcion.ShouldBeNull("no ha llegado");
        ficha.AnulaAId.ShouldBeNull();
        ficha.Lineas.Select(linea => linea.Numero).ShouldBe([1, 2]);
        ficha.Lineas.ShouldAllBe(
            linea => linea.UbicacionOrigenId == escena.UbicacionA && linea.UbicacionDestinoId == escena.UbicacionB,
            "cada línea lleva el hueco de cada punta");
        ficha.Lineas.Select(linea => linea.UnidadIntroducidaId).ShouldBe([escena.UnidadId, caja]);
        ficha.Lineas.Select(linea => linea.FactorAUnidadBase).ShouldBe(
            [1m, 12m], "el factor lo puso el servidor, y la ficha lo enseña");
        ficha.Lineas.Select(linea => linea.CantidadEnUnidadBase).ShouldBe([5m, 36m], "cinco sueltas y tres cajas de doce");
        ficha.Lineas.ShouldAllBe(linea => linea.Valor == null, "el valor lo pone el envío");

        using (HttpResponseMessage enviada = await escena.EnviarPorLaApiAsync(creada.Id))
        {
            enviada.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(enviada));
        }

        FichaDeTransferenciaDto salida = await FichaAsync(escena, creada.Id);

        salida.Estado.ShouldBe("Enviada");
        salida.Numero.ShouldNotBeNull("enviada, numera");
        salida.Lineas.Select(linea => linea.Valor).ShouldBe(
            [10m, 72m], "lo que salió del origen a su precio medio, dos: cinco y treinta y seis unidades");

        using HttpResponseMessage anulada = await escena.AnularPorLaApiAsync(creada.Id);

        AnulacionDeTransferenciaDto anulacion = await EscenaDeTransferencia.LeerAsync<AnulacionDeTransferenciaDto>(anulada);
        FichaDeTransferenciaDto inverso = await FichaAsync(escena, anulacion.Inverso.Id);

        inverso.AnulaAId.ShouldBe(creada.Id, "la ficha del inverso dice a quién anula");
        inverso.Motivo.ShouldBe("Se envió al almacén equivocado");
        inverso.Lineas.Select(linea => linea.FactorAUnidadBase).ShouldBe([1m, 12m], "el factor del original");
        inverso.Lineas.Select(linea => linea.CantidadEnUnidadBase).ShouldBe([-5m, -36m]);
        inverso.Lineas.Select(linea => linea.Valor).ShouldBe([-10m, -72m], "el valor del original, negado");
    }

    [Fact]
    public async Task Sin_idempotency_key_el_alta_es_428_y_con_la_misma_clave_no_abre_otra()
    {
        EscenaDeTransferencia escena = await MontarAsync(915, "STR-B");
        AbrirTransferenciaDto peticion = Peticion(escena, escena.AlmacenA, escena.AlmacenB, DeAaB(escena, 1m));

        using (HttpResponseMessage sinClave = await AbrirAsync(escena, peticion, clave: null))
        {
            sinClave.StatusCode.ShouldBe(HttpStatusCode.PreconditionRequired, await Escenario.Detalle(sinClave));
        }

        (await TransferenciasDeAsync(escena)).ShouldBe(0, "sin clave no se abre nada");

        // LA PAREJA, Y EL PORQUÉ DE LA OBLIGACIÓN: el móvil que reenvía el alta con su clave recibe el
        // mismo borrador, y no dos que se podrían enviar los dos (ADR-0068 §1).
        string clave = Guid.NewGuid().ToString();

        using HttpResponseMessage primera = await AbrirAsync(escena, peticion, clave);
        using HttpResponseMessage repetida = await AbrirAsync(escena, peticion, clave);

        primera.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(primera));
        repetida.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(repetida));

        (await repetida.Content.ReadFromJsonAsync<TransferenciaDto>())!.Id
            .ShouldBe((await primera.Content.ReadFromJsonAsync<TransferenciaDto>())!.Id, "la repetición es la primera");

        (await TransferenciasDeAsync(escena)).ShouldBe(1, "un solo borrador para las dos peticiones");
    }

    [Fact]
    public async Task Doscientas_lineas_abren_y_una_mas_es_400_con_el_motivo_del_tope()
    {
        EscenaDeTransferencia escena = await MontarAsync(916, "STR-C");
        LineaDeTransferenciaDto linea = DeAaB(escena, 1m);

        using HttpResponseMessage justas = await AbrirAsync(
            escena,
            Peticion(
                escena, escena.AlmacenA, escena.AlmacenB, [.. Enumerable.Repeat(linea, Paginacion.TamanioMaximo)]),
            Guid.NewGuid().ToString());

        justas.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(justas));
        (await justas.Content.ReadFromJsonAsync<TransferenciaDto>())!.Lineas.ShouldBe(Paginacion.TamanioMaximo);

        using HttpResponseMessage demasiadas = await AbrirAsync(
            escena,
            Peticion(
                escena, escena.AlmacenA, escena.AlmacenB, [.. Enumerable.Repeat(linea, Paginacion.TamanioMaximo + 1)]),
            Guid.NewGuid().ToString());

        demasiadas.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await Escenario.Detalle(demasiadas));

        // UN SOLO CAMPO, CON SU MOTIVO, y sin mirar bajo qué clave: la pone el enlace de modelo, como
        // en el ajuste. La pantalla no la necesita, porque su rejilla no deja teclear la 201.
        JsonElement problema = await ProblemaAsync(demasiadas);

        problema.GetProperty("type").GetString().ShouldBe("/errors/datos-no-validos");
        problema.GetProperty("errors").EnumerateObject()
            .ShouldHaveSingleItem().Value.EnumerateArray().Select(motivo => motivo.GetString())
            .ShouldBe([$"Una transferencia lleva como mucho {Paginacion.TamanioMaximo} líneas."]);

        (await TransferenciasDeAsync(escena)).ShouldBe(1, "solo la de doscientas");
    }

    [Fact]
    public async Task La_lista_va_de_la_ultima_fecha_de_envio_a_la_primera_se_acota_por_cada_punta_y_no_trae_la_descartada()
    {
        EscenaDeTransferencia escena = await MontarAsync(917, "STR-D");

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 10m, 2m);
        await escena.EntrarAsync(postgres, escena.AlmacenB, escena.UbicacionB, 10m, 2m);

        Guid vieja = await AbrirConLaClaveAsync(escena, escena.AlmacenA, escena.AlmacenB);
        Guid deB = await AbrirConLaClaveAsync(escena, escena.AlmacenB, escena.AlmacenC);
        Guid deA = await AbrirConLaClaveAsync(escena, escena.AlmacenA, escena.AlmacenB);
        Guid tirada = await AbrirConLaClaveAsync(escena, escena.AlmacenA, escena.AlmacenB);

        // TODAS LAS ALTAS SON DE HOY, así que la primera se lleva a ayer por debajo del ORM: es un
        // borrador, y no tiene filas en el libro que se queden con otra fecha. Dentro del día van por el
        // identificador, que crece con el tiempo y no se invierte.
        await TocarAsync(vieja, "fecha_de_envio = fecha_de_envio - 1");

        // DE A PRIMERO, para que el orden por número no sea el de la lista; y la de B llega hoy, para
        // que la fecha de recepción tenga algo que ordenar.
        await AvanzarAsync(escena.EnviarPorLaApiAsync(deA));
        await AvanzarAsync(escena.EnviarPorLaApiAsync(deB));
        await AvanzarAsync(escena.RecibirPorLaApiAsync(deB, EscenaDeTransferencia.Hoy));

        (await IdsAsync(escena, "")).ShouldBe([deB, deA, tirada, vieja], "antes de tirarla, el borrador sale");

        using (HttpResponseMessage descarte = await DescartarAsync(
            escena, tirada, await escena.Cliente.EtiquetaDeAsync($"{Transferencias}/{tirada}")))
        {
            descarte.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(descarte));
        }

        PaginaDe<TransferenciaResumenDto> pagina = await ListarAsync(escena, "");

        pagina.Elementos.Select(transferencia => transferencia.Id).ShouldBe([deB, deA, vieja], "la descartada no sale (§5)");
        pagina.Total.ShouldBe(3, "ni cuenta");

        (await IdsAsync(escena, "?sort=fechaDeEnvio")).ShouldBe([vieja, deB, deA]);
        (await IdsAsync(escena, "?sort=numero")).ShouldBe([deA, deB, vieja], "el borrador, sin número, al final");
        (await IdsAsync(escena, "?sort=-numero")).ShouldBe([vieja, deB, deA]);

        // LA FECHA DE RECEPCIÓN ES NULA EN LO QUE NO HA LLEGADO, y PostgreSQL la pone la última de
        // menor a mayor y la primera de mayor a menor: quien recibe ordena al revés y ve arriba lo
        // que todavía le tiene que llegar.
        (await IdsAsync(escena, "?sort=fechaDeRecepcion")).ShouldBe([deB, vieja, deA]);
        (await IdsAsync(escena, "?sort=-fechaDeRecepcion")).ShouldBe([vieja, deA, deB]);

        (await IdsAsync(escena, "?estado=Borrador")).ShouldBe([vieja]);
        (await IdsAsync(escena, "?estado=Enviada")).ShouldBe([deA]);
        (await IdsAsync(escena, "?estado=Recibida")).ShouldBe([deB]);
        (await IdsAsync(escena, "?estado=Anulada")).ShouldBeEmpty();

        // CADA PUNTA ACOTA LA SUYA, y las dos juntas, lo que va de una a otra: de A sale algo, a C
        // llega algo, y de A a C no va nada.
        (await IdsAsync(escena, $"?origen={escena.AlmacenA}")).ShouldBe([deA, vieja]);
        (await IdsAsync(escena, $"?origen={escena.AlmacenB}")).ShouldBe([deB]);
        (await IdsAsync(escena, $"?destino={escena.AlmacenB}")).ShouldBe([deA, vieja]);
        (await IdsAsync(escena, $"?destino={escena.AlmacenC}")).ShouldBe([deB]);
        (await IdsAsync(escena, $"?origen={escena.AlmacenB}&destino={escena.AlmacenC}")).ShouldBe([deB]);
        (await IdsAsync(escena, $"?origen={escena.AlmacenA}&destino={escena.AlmacenC}")).ShouldBeEmpty();

        // SIN FILTRO DE TEXTO (§1): el `q` llega y no filtra nada, como en las líneas de tarifa.
        (await IdsAsync(escena, "?q=no-esta-en-ninguna")).ShouldBe([deB, deA, vieja]);

        pagina.Elementos[^1].ShouldBe(new TransferenciaResumenDto(
            vieja, null, escena.AlmacenA, escena.AlmacenB, EscenaDeTransferencia.Hoy.AddDays(-1), null, "Borrador", null));
        pagina.Elementos[0].ShouldBe(new TransferenciaResumenDto(
            deB, 2, escena.AlmacenB, escena.AlmacenC, EscenaDeTransferencia.Hoy, EscenaDeTransferencia.Hoy, "Recibida", null));

        await NoSeAdmiteAsync(escena, "?estado=Descartada", "/errors/datos-no-validos");
        await NoSeAdmiteAsync(escena, "?sort=motivo", "/errors/orden-no-admitido");
    }

    [Fact]
    public async Task El_descarte_pide_la_version_de_la_ficha_y_repetirlo_es_404()
    {
        EscenaDeTransferencia escena = await MontarAsync(918, "STR-E");

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 10m, 2m);

        Guid enviada = await AbrirConLaClaveAsync(escena, escena.AlmacenA, escena.AlmacenB);
        string deAntes = await escena.Cliente.EtiquetaDeAsync($"{Transferencias}/{enviada}");

        using (HttpResponseMessage sinVersion = await DescartarAsync(escena, enviada, etiqueta: null))
        {
            sinVersion.StatusCode.ShouldBe(HttpStatusCode.PreconditionRequired, await Escenario.Detalle(sinVersion));
        }

        await AvanzarAsync(escena.EnviarPorLaApiAsync(enviada));

        // LA VERSIÓN ANTES QUE EL ESTADO (ADR-0057 §3): con la de antes de enviarla, 412; con la de
        // ahora, el 409 de una transferencia que ya no es un borrador.
        using (HttpResponseMessage vieja = await DescartarAsync(escena, enviada, deAntes))
        {
            vieja.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed, await Escenario.Detalle(vieja));
        }

        using (HttpResponseMessage deAhora = await DescartarAsync(
            escena, enviada, await escena.Cliente.EtiquetaDeAsync($"{Transferencias}/{enviada}")))
        {
            deAhora.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(deAhora));
            (await EscenaDeTransferencia.TipoDelProblemaAsync(deAhora))
                .ShouldBe("/errors/transferencia-no-esta-en-borrador");
        }

        Guid borrador = await AbrirConLaClaveAsync(escena, escena.AlmacenA, escena.AlmacenB);
        string suVersion = await escena.Cliente.EtiquetaDeAsync($"{Transferencias}/{borrador}");

        using (HttpResponseMessage descarte = await DescartarAsync(escena, borrador, suVersion))
        {
            descarte.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(descarte));
            (await descarte.Content.ReadAsStringAsync()).ShouldBeEmpty("un 204 no lleva cuerpo");
        }

        using (HttpResponseMessage lectura = await escena.Cliente.GetAsync($"{Transferencias}/{borrador}"))
        {
            lectura.StatusCode.ShouldBe(HttpStatusCode.NotFound, "la ficha de una descartada no se encuentra");
        }

        using (HttpResponseMessage otraVez = await DescartarAsync(escena, borrador, suVersion))
        {
            otraVez.StatusCode.ShouldBe(HttpStatusCode.NotFound, "repetirlo da un 404, que es lo que es");
        }

        using HttpResponseMessage enviarla = await escena.EnviarPorLaApiAsync(borrador);

        enviarla.StatusCode.ShouldBe(HttpStatusCode.NotFound, await Escenario.Detalle(enviarla));
    }

    [Fact]
    public async Task Otra_empresa_no_ve_la_transferencia_ni_la_descarta()
    {
        EscenaDeTransferencia escena = await MontarAsync(919, "STR-F");

        Guid propia = await AbrirConLaClaveAsync(escena, escena.AlmacenA, escena.AlmacenB);
        string version = await escena.Cliente.EtiquetaDeAsync($"{Transferencias}/{propia}");

        (HttpClient ajena, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(920));
        _clientes.Add(ajena);

        using (HttpResponseMessage lectura = await ajena.GetAsync($"{Transferencias}/{propia}"))
        {
            lectura.StatusCode.ShouldBe(HttpStatusCode.NotFound, await Escenario.Detalle(lectura));
        }

        using (HttpResponseMessage lista = await ajena.GetAsync($"{Transferencias}?origen={escena.AlmacenA}"))
        {
            (await EscenaDeTransferencia.LeerAsync<PaginaDe<TransferenciaResumenDto>>(lista)).Total.ShouldBe(0);
        }

        using (HttpResponseMessage descarte = await ajena.EnviarConVersionAsync(
            HttpMethod.Delete, $"{Transferencias}/{propia}", version))
        {
            descarte.StatusCode.ShouldBe(HttpStatusCode.NotFound, await Escenario.Detalle(descarte));
        }

        // LA PAREJA: la suya la ve, con la misma versión, porque nadie la ha tocado (R8).
        (await escena.Cliente.EtiquetaDeAsync($"{Transferencias}/{propia}")).ShouldBe(version);
        (await IdsAsync(escena, $"?origen={escena.AlmacenA}")).ShouldBe([propia]);
    }

    [Fact]
    public async Task Una_linea_sin_conversion_es_422_con_la_linea_y_las_dos_unidades_en_actual()
    {
        EscenaDeTransferencia escena = await MontarAsync(921, "STR-G");

        Guid caja = await LosMaestrosPorLaApi.CrearUnidadAsync(escena.Cliente, "K921");
        AbrirTransferenciaDto peticion = Peticion(
            escena, escena.AlmacenA, escena.AlmacenB, DeAaB(escena, 1m), DeAaB(escena, 2m, caja));

        using (HttpResponseMessage rechazo = await AbrirAsync(escena, peticion, Guid.NewGuid().ToString()))
        {
            rechazo.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity, await Escenario.Detalle(rechazo));

            // CON LOS NOMBRES CON QUE VIAJA, que son los que lee la pantalla para señalar la línea y
            // ofrecer la conversión que falta (ADR-0068 §6).
            JsonElement problema = await ProblemaAsync(rechazo);
            JsonElement actual = problema.GetProperty("actual");

            problema.GetProperty("type").GetString().ShouldBe("/errors/transferencia-sin-conversion");
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
    public async Task Una_ubicacion_que_falla_viaja_con_el_campo_de_su_punta_o_con_la_punta_en_actual()
    {
        EscenaDeTransferencia escena = await MontarAsync(922, "STR-H");

        UbicacionDto otroDeB = await LosMaestrosPorLaApi.CrearUbicacionAsync(
            escena.Cliente, escena.AlmacenB, "STR-H-B2");
        LineaDeTransferenciaDto buena = DeAaB(escena, 1m);

        // LA DEL ALMACÉN DE ENFRENTE NO EXISTE EN EL DESTINO, y va al campo de su punta, con el índice
        // desde cero de la ruta de zod.
        using (HttpResponseMessage rechazo = await AbrirAsync(
            escena,
            Peticion(
                escena, escena.AlmacenA, escena.AlmacenB, buena, buena with { UbicacionDestinoId = escena.UbicacionA }),
            Guid.NewGuid().ToString()))
        {
            rechazo.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await Escenario.Detalle(rechazo));

            JsonElement problema = await ProblemaAsync(rechazo);

            problema.GetProperty("type").GetString().ShouldBe("/errors/transferencia-ubicacion-no-encontrada");
            problema.GetProperty("errors").EnumerateObject().Select(campo => campo.Name)
                .ShouldBe(["lineas[1].ubicacionDestinoId"]);
        }

        using (HttpResponseMessage bloqueo = await escena.Cliente.SuprimirAsync(
            $"{LosMaestrosPorLaApi.Ubicaciones}/{otroDeB.Id}"))
        {
            bloqueo.IsSuccessStatusCode.ShouldBeTrue(await Escenario.Detalle(bloqueo));
        }

        // LA BLOQUEADA DICE LA PUNTA, con el nombre y el valor con que viaja: sin ella, la pantalla no
        // sabría en cuál de las dos celdas de la línea ponerlo (§6).
        using (HttpResponseMessage rechazo = await AbrirAsync(
            escena,
            Peticion(escena, escena.AlmacenA, escena.AlmacenB, buena, buena with { UbicacionDestinoId = otroDeB.Id }),
            Guid.NewGuid().ToString()))
        {
            rechazo.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(rechazo));

            JsonElement problema = await ProblemaAsync(rechazo);
            JsonElement actual = problema.GetProperty("actual");

            problema.GetProperty("type").GetString().ShouldBe("/errors/transferencia-ubicacion-bloqueada");
            actual.GetProperty("linea").GetInt32().ShouldBe(2);
            actual.GetProperty("punta").GetString().ShouldBe("Destino");
        }

        (await TransferenciasDeAsync(escena)).ShouldBe(0, "un alta rechazada no deja un borrador a medias");

        // LA PAREJA: con el hueco de su almacén, la misma línea abre.
        using HttpResponseMessage alta = await AbrirAsync(
            escena,
            Peticion(escena, escena.AlmacenA, escena.AlmacenB, buena, buena),
            Guid.NewGuid().ToString());

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));
    }

    private static LineaDeTransferenciaDto DeAaB(EscenaDeTransferencia escena, decimal cantidad, Guid? unidadId = null) =>
        escena.Linea(escena.UbicacionA, escena.UbicacionB, cantidad) with
        {
            UnidadIntroducidaId = unidadId ?? escena.UnidadId,
        };

    private static AbrirTransferenciaDto Peticion(
        EscenaDeTransferencia escena, Guid origen, Guid destino, params LineaDeTransferenciaDto[] lineas) =>
        new(escena.SerieDeTransferencias.Id, origen, destino, EscenaDeTransferencia.Hoy, lineas);

    private static Task<HttpResponseMessage> AbrirAsync(
        EscenaDeTransferencia escena, AbrirTransferenciaDto peticion, string? clave)
    {
        HttpRequestMessage alta = new(HttpMethod.Post, Transferencias) { Content = JsonContent.Create(peticion) };

        if (clave is not null)
        {
            alta.Headers.TryAddWithoutValidation("Idempotency-Key", clave);
        }

        return escena.Cliente.SendAsync(alta);
    }

    /// <summary>Abre una de una unidad entre dos almacenes de la escena, con el hueco de cada uno.</summary>
    private static async Task<Guid> AbrirConLaClaveAsync(EscenaDeTransferencia escena, Guid origen, Guid destino)
    {
        using HttpResponseMessage alta = await AbrirAsync(
            escena,
            Peticion(
                escena,
                origen,
                destino,
                escena.Linea(ElHuecoDe(escena, origen), ElHuecoDe(escena, destino), 1m)),
            Guid.NewGuid().ToString());

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<TransferenciaDto>())!.Id;
    }

    private static Guid ElHuecoDe(EscenaDeTransferencia escena, Guid almacenId) =>
        almacenId == escena.AlmacenA ? escena.UbicacionA
        : almacenId == escena.AlmacenB ? escena.UbicacionB
        : escena.UbicacionC;

    private static async Task AvanzarAsync(Task<HttpResponseMessage> transicion)
    {
        using HttpResponseMessage respuesta = await transicion;

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(respuesta));
    }

    private static Task<HttpResponseMessage> DescartarAsync(
        EscenaDeTransferencia escena, Guid transferenciaId, string? etiqueta) =>
        escena.Cliente.EnviarConVersionAsync(HttpMethod.Delete, $"{Transferencias}/{transferenciaId}", etiqueta);

    private static async Task<FichaDeTransferenciaDto> FichaAsync(EscenaDeTransferencia escena, Guid transferenciaId)
    {
        using HttpResponseMessage lectura = await escena.Cliente.GetAsync($"{Transferencias}/{transferenciaId}");

        return await EscenaDeTransferencia.LeerAsync<FichaDeTransferenciaDto>(lectura);
    }

    private static async Task<PaginaDe<TransferenciaResumenDto>> ListarAsync(
        EscenaDeTransferencia escena, string consulta)
    {
        using HttpResponseMessage lectura = await escena.Cliente.GetAsync(Transferencias + consulta);

        return await EscenaDeTransferencia.LeerAsync<PaginaDe<TransferenciaResumenDto>>(lectura);
    }

    private static async Task<IReadOnlyList<Guid>> IdsAsync(EscenaDeTransferencia escena, string consulta) =>
        [.. (await ListarAsync(escena, consulta)).Elementos.Select(transferencia => transferencia.Id)];

    private static async Task NoSeAdmiteAsync(EscenaDeTransferencia escena, string consulta, string tipo)
    {
        using HttpResponseMessage lectura = await escena.Cliente.GetAsync(Transferencias + consulta);

        lectura.StatusCode.ShouldBe(HttpStatusCode.BadRequest, $"{consulta}: {await Escenario.Detalle(lectura)}");
        (await EscenaDeTransferencia.TipoDelProblemaAsync(lectura)).ShouldBe(tipo);
    }

    private static async Task<JsonElement> ProblemaAsync(HttpResponseMessage respuesta)
    {
        using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        return problema.RootElement.Clone();
    }

    // UNA ESCRITURA POR DEBAJO DEL ORM, que mueve lo que diga y nada más.
    private async Task TocarAsync(Guid transferenciaId, string cambio)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(
            $"UPDATE inventario.transferencias SET {cambio} WHERE id = @id", conexion);
        orden.Parameters.AddWithValue("id", transferenciaId);

        (await orden.ExecuteNonQueryAsync()).ShouldBe(1, $"transferencia {transferenciaId}");
    }

    private async Task<int> TransferenciasDeAsync(EscenaDeTransferencia escena)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId);

        return await contexto.Transferencias.CountAsync();
    }

    private async Task<EscenaDeTransferencia> MontarAsync(int semilla, string codigo)
    {
        EscenaDeTransferencia escena = await EscenaDeTransferencia.MontarAsync(_api, semilla, codigo, semilla);

        _clientes.Add(escena.Cliente);

        return escena;
    }
}
