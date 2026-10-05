using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Domain.Transferencias;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Organizacion.Contracts.Ejercicios;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Bastion.Organizacion.Domain.Series;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Las guardas de la transferencia que no tenían un caso que las viera (ítem 2.11): los maestros del
/// alta, el día sin ejercicio, la fecha anterior al último movimiento, la divisa de las dos puntas y
/// lo que el ejercicio le pregunta a Inventario al cerrarse y al encogerse.
/// </summary>
/// <remarks>
/// <para>
/// <b>Salen de la revisión del paso 5</b>: cada código tenía su traducción en el borde y su prueba
/// de dominio, pero ningún caso lo hacía saltar de punta a punta. Cada uno lleva su otra mitad —el
/// mismo documento que la guarda para, ya en regla, pasa—, porque una guarda que contestara siempre
/// que no daría el mismo rojo que la buena.
/// </para>
/// <para>
/// <b>Y de la tanda de mutaciones del paso 8</b>, las guardas del caso de uso que la tanda quitó una
/// a una sin que nada se pusiera rojo: los estados de cada acción, el motivo, el ejercicio cerrado
/// de la anulación, las del alta, la marca releída al enviar, y la divisa que estrena una clave
/// vaciada. El dominio repite casi todas, así que sin la del caso de uso el documento no se
/// estropeaba: el borde contestaba un <c>500</c> donde tenía que ir un <c>409</c> o un <c>400</c>.
/// Por eso estos casos miran el código y el <c>type</c>, no solo que la acción falle.
/// </para>
/// <para>
/// <b>Los días son de años que ya pasaron</b>, salvo en la divisa, que no mira fechas: una guarda
/// de fechas probada con «hoy» dejaría de ver algo el 1 de enero. Los casos del paso 8 van con
/// «hoy» porque ninguno mira una fecha: el ejercicio cerrado es el de la anulación, que no tiene
/// otro día que hoy.
/// </para>
/// <para>
/// <b>Semillas: del 713 al 718, del 727 al 729, el 733 y el 734</b>, empresas y maestros de
/// instalación con el mismo número. El reparto del bloque está en la cabecera de
/// <c>LaTransferenciaTests</c>.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LasGuardasDeLaTransferenciaTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string RutaDeEmpresas = "/api/v1/organizacion/empresas";

    private readonly ApiDeVerdad _api = new(postgres);
    private readonly List<HttpClient> _clientes = [];

    private static DateOnly Hoy => EscenaDeTransferencia.Hoy;

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
    /// El alta pregunta por cada ubicación a su propio almacén, y no deja salir ni llegar nada a un
    /// almacén o a un hueco bloqueados.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El mismo hueco en las dos puntas son dos preguntas</b>, una al origen y otra al destino, y
    /// una de las dos tiene que contestar que no. Un alta que preguntara por el hueco a secas, sin su
    /// almacén, daría por buena una línea que sale de A y deja la mercancía en una estantería de A.
    /// </para>
    /// <para>
    /// <b>La primera línea pasa</b>, y es la que demuestra que las demás se paran por lo que dicen y
    /// no porque el alta no funcione. Al final hay una sola transferencia: lo rechazado no deja
    /// borradores a medias.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task El_alta_pregunta_cada_hueco_a_su_almacen_y_no_toca_lo_bloqueado()
    {
        EscenaDeTransferencia escena = await MontarAsync(713, "TRG-A");

        UbicacionDto otroHueco = await LosMaestrosPorLaApi.CrearUbicacionAsync(
            escena.Cliente, escena.AlmacenB, "TRG-A-B2");

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        Resultado<TransferenciaDto> buena = await escena.IntentarAbrirAsync(
            modulo, escena.AlmacenA, escena.AlmacenB, [escena.Linea(escena.UbicacionA, escena.UbicacionB, 1m)]);

        buena.EsCorrecto.ShouldBeTrue($"«{buena.Error?.Codigo}»");

        await ExigirElRechazoAsync(
            escena,
            modulo,
            escena.AlmacenA,
            escena.AlmacenA,
            escena.Linea(escena.UbicacionA, escena.UbicacionA, 1m),
            "transferencia-mismo-almacen",
            TipoDeError.Validacion);

        foreach ((Guid desde, Guid hasta) in new[]
        {
            (escena.UbicacionA, escena.UbicacionA),
            (escena.UbicacionB, escena.UbicacionB),
            (escena.UbicacionA, Guid.CreateVersion7()),
        })
        {
            await ExigirElRechazoAsync(
                escena,
                modulo,
                escena.AlmacenA,
                escena.AlmacenB,
                escena.Linea(desde, hasta, 1m),
                "transferencia-ubicacion-no-encontrada",
                TipoDeError.Validacion);
        }

        using (HttpResponseMessage bloqueo = await escena.Cliente.SuprimirAsync(
            $"{LosMaestrosPorLaApi.Ubicaciones}/{otroHueco.Id}"))
        {
            bloqueo.IsSuccessStatusCode.ShouldBeTrue(await Escenario.Detalle(bloqueo));
        }

        await ExigirElRechazoAsync(
            escena,
            modulo,
            escena.AlmacenA,
            escena.AlmacenB,
            escena.Linea(escena.UbicacionA, otroHueco.Id, 1m),
            "transferencia-ubicacion-bloqueada",
            TipoDeError.Conflicto);

        using (HttpResponseMessage bloqueo = await escena.Cliente.SuprimirAsync(
            $"{LosMaestrosPorLaApi.Almacenes}/{escena.AlmacenC}"))
        {
            bloqueo.IsSuccessStatusCode.ShouldBeTrue(await Escenario.Detalle(bloqueo));
        }

        await ExigirElRechazoAsync(
            escena,
            modulo,
            escena.AlmacenA,
            escena.AlmacenC,
            escena.Linea(escena.UbicacionA, escena.UbicacionC, 1m),
            "transferencia-almacen-bloqueado",
            TipoDeError.Conflicto);

        await ExigirElRechazoAsync(
            escena,
            modulo,
            escena.AlmacenC,
            escena.AlmacenA,
            escena.Linea(escena.UbicacionC, escena.UbicacionA, 1m),
            "transferencia-almacen-bloqueado",
            TipoDeError.Conflicto);

        await using InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId);

        (await contexto.Transferencias.CountAsync()).ShouldBe(1, "un alta rechazada no deja un borrador a medias");
    }

    /// <summary>
    /// Recibir y anular un día que no tiene ejercicio es <c>409</c> y no mueve nada; con el ejercicio
    /// creado, las dos pasan.
    /// </summary>
    /// <remarks>
    /// <b>El ejercicio que falta es el de hoy</b>: la recepción va con la fecha de hoy y la anulación
    /// la pone el servidor (ADR-0053 §5). Que crearlo salga bien es, además, la prueba de que no
    /// existía.
    /// </remarks>
    [Fact]
    public async Task Sin_ejercicio_para_el_dia_ni_se_recibe_ni_se_anula()
    {
        int anio = Hoy.Year - 1;
        EscenaDeTransferencia escena = await MontarAsync(714, "TRG-B", anio);

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 5m, 2m, new DateOnly(anio, 6, 1));

        TransferenciaDto enviada = await escena.EnviarAsync(postgres, 3m, new DateOnly(anio, 6, 10));

        using (HttpResponseMessage recepcion = await escena.RecibirPorLaApiAsync(enviada.Id, Hoy))
        {
            recepcion.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(recepcion));
            (await EscenaDeTransferencia.TipoDelProblemaAsync(recepcion)).ShouldBe("/errors/transferencia-sin-ejercicio");
        }

        using (HttpResponseMessage anulacion = await escena.AnularPorLaApiAsync(enviada.Id))
        {
            anulacion.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(anulacion));
            (await EscenaDeTransferencia.TipoDelProblemaAsync(anulacion)).ShouldBe("/errors/transferencia-sin-ejercicio");
        }

        (await escena.LaTransferenciaAsync(postgres, enviada.Id)).Estado.ShouldBe(EstadoDeTransferencia.Enviada);
        (await escena.FilasDelLibroAsync(postgres, enviada.Id)).Count.ShouldBe(1, "solo la salida del envío");
        (await escena.InversosDeAsync(postgres, enviada.Id)).ShouldBe(0);
        (await escena.ContadorAsync()).ShouldBe(1, "la anulación rechazada no deja un número gastado");

        await LosMaestrosPorLaApi.CrearEjercicioAsync(escena.Cliente, Hoy.Year);

        using HttpResponseMessage recibida = await escena.RecibirPorLaApiAsync(enviada.Id, Hoy);

        (await EscenaDeTransferencia.LeerAsync<TransferenciaDto>(recibida)).Estado
            .ShouldBe(nameof(EstadoDeTransferencia.Recibida));

        using HttpResponseMessage anulada = await escena.AnularPorLaApiAsync(enviada.Id);

        AnulacionDeTransferenciaDto par = await EscenaDeTransferencia.LeerAsync<AnulacionDeTransferenciaDto>(anulada);

        par.Inverso.SerieId.ShouldBe(escena.SerieDeTransferencias.Id, "el inverso numera en la serie del original (§5)");
        par.Inverso.Numero.ShouldBe(2);
    }

    /// <summary>
    /// Enviar con un día anterior al último movimiento del origen, o recibir con uno anterior al del
    /// destino, es <c>422</c>; el mismo día, no.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Cada punta se mira en su momento</b> (ADR-0053 §3): el envío contra el origen y la
    /// recepción contra el destino. Y el tránsito no mueve la última fecha del destino: la valoración
    /// de B nace con el envío, y nace sin fecha.
    /// </para>
    /// <para>
    /// <b>El envío rechazado ya había tomado su número</b>, porque la fecha se mira con las
    /// valoraciones bloqueadas y eso va después del numerador. El contador en cero dice que el
    /// <c>422</c> deshizo la transacción entera.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Una_fecha_anterior_al_ultimo_movimiento_de_su_punta_es_422()
    {
        int anio = Hoy.Year - 1;
        EscenaDeTransferencia escena = await MontarAsync(715, "TRG-C", anio);

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 5m, 2m, new DateOnly(anio, 6, 10));

        Guid antes = await escena.AbrirAsync(postgres, 2m, new DateOnly(anio, 6, 9));

        using (HttpResponseMessage envio = await escena.EnviarPorLaApiAsync(antes))
        {
            envio.StatusCode.ShouldBe(HttpStatusCode.UnprocessableContent, await Escenario.Detalle(envio));
            (await EscenaDeTransferencia.TipoDelProblemaAsync(envio))
                .ShouldBe("/errors/transferencia-fecha-anterior-al-ultimo-movimiento");
        }

        (await escena.LaTransferenciaAsync(postgres, antes)).Estado.ShouldBe(EstadoDeTransferencia.Borrador);
        (await escena.ContadorAsync()).ShouldBe(0);

        TransferenciaDto enviada = await escena.EnviarAsync(postgres, 2m, new DateOnly(anio, 6, 10));

        (await escena.LaValoracionDeAsync(postgres, escena.AlmacenB)).UltimaFecha
            .ShouldBeNull("el tránsito no mueve la última fecha del destino (§3)");

        await escena.EntrarAsync(postgres, escena.AlmacenB, escena.UbicacionB, 1m, 3m, new DateOnly(anio, 6, 15));

        using (HttpResponseMessage recepcion =
            await escena.RecibirPorLaApiAsync(enviada.Id, new DateOnly(anio, 6, 14)))
        {
            recepcion.StatusCode.ShouldBe(HttpStatusCode.UnprocessableContent, await Escenario.Detalle(recepcion));
            (await EscenaDeTransferencia.TipoDelProblemaAsync(recepcion))
                .ShouldBe("/errors/transferencia-fecha-anterior-al-ultimo-movimiento");
        }

        (await escena.LaTransferenciaAsync(postgres, enviada.Id)).Estado.ShouldBe(EstadoDeTransferencia.Enviada);

        using HttpResponseMessage recibida =
            await escena.RecibirPorLaApiAsync(enviada.Id, new DateOnly(anio, 6, 15));

        (await EscenaDeTransferencia.LeerAsync<TransferenciaDto>(recibida)).Estado
            .ShouldBe(nameof(EstadoDeTransferencia.Recibida));

        (await escena.LaValoracionDeAsync(postgres, escena.AlmacenB)).UltimaFecha
            .ShouldBe(new DateOnly(anio, 6, 15));
    }

    /// <summary>
    /// Una punta valorada en otra divisa que la del documento para el envío con un <c>422</c>, sea
    /// el destino o el origen; entre dos puntas de la divisa nueva, el tránsito viaja en ella.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La empresa pasa del euro al dólar con B ya valorado en euros.</b> Lo que entra en A después
    /// se valora en dólares, y la transferencia abierta después también lleva dólares. Hacia B, el
    /// destino no puede sumar el tránsito; desde B, el origen no puede valorar la salida. Las dos son
    /// el mismo código, y salen de dos sitios distintos del envío: la comprobación del destino y el
    /// impedimento del origen.
    /// </para>
    /// <para>
    /// <b>La otra mitad, hacia C</b>, que no tiene valoración: el tránsito nace en dólares, con el
    /// medio de A.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Una_punta_valorada_en_otra_divisa_para_el_envio_por_los_dos_lados()
    {
        EscenaDeTransferencia escena = await MontarAsync(716, "TRG-D");

        await escena.EntrarAsync(postgres, escena.AlmacenB, escena.UbicacionB, 5m, 4m);

        using (HttpResponseMessage cambio = await escena.Cliente.ModificarAsync(
            $"{RutaDeEmpresas}/{escena.EmpresaId}",
            new ModificarEmpresaDto
            {
                RazonSocial = "Empresa que pasa a llevar las cuentas en dólares",
                DomicilioFiscal = Escenario.Domicilio(),
                DivisaBase = "USD",
                RegimenDeIva = "General",
            }))
        {
            cambio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(cambio));
        }

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 3m, 2m);

        Guid haciaB;
        Guid desdeB;
        Guid haciaC;

        await using (ElModuloDeInventario modulo = new(postgres, escena.EmpresaId))
        {
            haciaB = await escena.AbrirAsync(modulo, 1m);
            desdeB = await AbrirAsync(
                escena, modulo, escena.AlmacenB, escena.AlmacenC, escena.Linea(escena.UbicacionB, escena.UbicacionC, 1m));
            haciaC = await AbrirAsync(
                escena, modulo, escena.AlmacenA, escena.AlmacenC, escena.Linea(escena.UbicacionA, escena.UbicacionC, 1m));
        }

        foreach (Guid rechazada in new[] { haciaB, desdeB })
        {
            using HttpResponseMessage envio = await escena.EnviarPorLaApiAsync(rechazada);

            envio.StatusCode.ShouldBe(HttpStatusCode.UnprocessableContent, await Escenario.Detalle(envio));
            (await EscenaDeTransferencia.TipoDelProblemaAsync(envio))
                .ShouldBe("/errors/transferencia-valoracion-en-otra-divisa");

            (await escena.LaTransferenciaAsync(postgres, rechazada)).Estado.ShouldBe(EstadoDeTransferencia.Borrador);
            (await escena.FilasDelLibroAsync(postgres, rechazada)).ShouldBeEmpty();
        }

        (await escena.ContadorAsync()).ShouldBe(0);
        (await escena.LaValoracionDeAsync(postgres, escena.AlmacenB)).EnTransito.ShouldBe(0m);

        using HttpResponseMessage enviada = await escena.EnviarPorLaApiAsync(haciaC);

        (await EscenaDeTransferencia.LeerAsync<TransferenciaDto>(enviada)).Divisa.ShouldBe("USD");

        Valoracion enC = await escena.LaValoracionDeAsync(postgres, escena.AlmacenC);

        enC.EnTransito.ShouldBe(1m);
        enC.ValorEnTransito.ShouldBe(Importe.De(2m, "USD"));
    }

    /// <summary>
    /// Una transferencia en borrador con el envío dentro del ejercicio impide cerrarlo, y enviada,
    /// no.
    /// </summary>
    /// <remarks>
    /// <b>Enviada y sin recibir deja cerrar</b>: lo que el cierre exige es que no quede nada a medias
    /// de escribir, y una enviada ya escribió su salida (ADR-0053 §12). Lo que vuela al cerrar se
    /// recibe en el ejercicio siguiente.
    /// </remarks>
    [Fact]
    public async Task Una_transferencia_en_borrador_impide_cerrar_el_ejercicio_y_enviada_no()
    {
        int anio = Hoy.Year - 1;
        EscenaDeTransferencia escena = await MontarAsync(717, "TRG-E", anio);

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 3m, 1m, new DateOnly(anio, 6, 1));

        Guid borrador = await escena.AbrirAsync(postgres, 1m, new DateOnly(anio, 6, 10));

        using (HttpResponseMessage negado = await escena.CerrarElEjercicioAsync())
        {
            negado.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(negado));
            (await EscenaDeTransferencia.TipoDelProblemaAsync(negado)).ShouldBe("/errors/ejercicio-con-borradores");
            (await DetalleDelProblemaAsync(negado)).ShouldContain(
                "Inventario", Case.Sensitive, "el error tiene que decir qué módulo se ha negado");
        }

        using (HttpResponseMessage envio = await escena.EnviarPorLaApiAsync(borrador))
        {
            envio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(envio));
        }

        using HttpResponseMessage cerrado = await escena.CerrarElEjercicioAsync();

        cerrado.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(cerrado));
    }

    /// <summary>
    /// Encoger un ejercicio por encima de la recepción de una transferencia enviada el año anterior
    /// es <c>409</c>; por el lado en el que no hay nada, no.
    /// </summary>
    /// <remarks>
    /// <b>El ejercicio solo tiene la recepción</b>: el envío y la entrada en A son del anterior. Una
    /// pregunta que mirara solo la fecha de envío, como la del borrador, daría el ejercicio por
    /// vacío y lo dejaría encoger, y la recepción pasaría a no ser de ningún ejercicio sin que nadie
    /// la tocara.
    /// </remarks>
    [Fact]
    public async Task Encoger_el_ejercicio_por_encima_de_una_recepcion_es_409_y_por_el_otro_lado_no()
    {
        int anio = Hoy.Year - 2;
        EscenaDeTransferencia escena = await MontarAsync(718, "TRG-F", anio);

        EjercicioDto siguiente = await LosMaestrosPorLaApi.CrearEjercicioAsync(escena.Cliente, anio + 1);

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 3m, 1m, new DateOnly(anio, 12, 1));

        TransferenciaDto enviada = await escena.EnviarAsync(postgres, 1m, new DateOnly(anio, 12, 20));

        using (HttpResponseMessage recibida =
            await escena.RecibirPorLaApiAsync(enviada.Id, new DateOnly(anio + 1, 1, 5)))
        {
            recibida.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(recibida));
        }

        string recurso = $"{LosMaestrosPorLaApi.Ejercicios}/{siguiente.Id}";

        using (HttpResponseMessage encogido = await EncogerAsync(
            escena, recurso, new DateOnly(anio + 1, 2, 1), new DateOnly(anio + 1, 12, 31)))
        {
            encogido.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(encogido));
            (await EscenaDeTransferencia.TipoDelProblemaAsync(encogido))
                .ShouldBe("/errors/ejercicio-dejaria-documentos-fuera");
            (await DetalleDelProblemaAsync(encogido)).ShouldContain(
                "Inventario", Case.Sensitive, "el error tiene que decir qué módulo se queda con documentos fuera");
        }

        using HttpResponseMessage permitido = await EncogerAsync(
            escena, recurso, new DateOnly(anio + 1, 1, 1), new DateOnly(anio + 1, 6, 30));

        permitido.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(permitido));
    }

    /// <summary>
    /// Enviar, recibir y anular fuera de su estado es <c>409</c>, con el código del estado que
    /// pedían; en el suyo, las tres pasan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Un solo documento recorre los cuatro estados</b>, y en cada uno se intenta lo que no le
    /// toca: así cada rechazo tiene su pareja, la misma acción que pasa en cuanto el documento llega
    /// a su estado. El inverso no tiene pareja, porque no se anula nunca.
    /// </para>
    /// <para>
    /// <b>Las mutaciones 224 a 227</b> quitaban cada una de estas guardas, y el dominio lanzaba en su
    /// lugar: nada se escribía, pero la respuesta era un <c>500</c>.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Cada_accion_fuera_de_su_estado_es_409_y_en_el_suyo_pasa()
    {
        EscenaDeTransferencia escena = await MontarAsync(727, "TRG-G");

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 5m, 2m);

        Guid transferenciaId = await escena.AbrirAsync(postgres, 2m);

        await ExigirElConflictoAsync(
            escena.RecibirPorLaApiAsync(transferenciaId, Hoy), "transferencia-no-esta-enviada");
        await ExigirElConflictoAsync(escena.AnularPorLaApiAsync(transferenciaId), "transferencia-no-se-anula");

        using (HttpResponseMessage envio = await escena.EnviarPorLaApiAsync(transferenciaId))
        {
            (await EscenaDeTransferencia.LeerAsync<TransferenciaDto>(envio)).Estado
                .ShouldBe(nameof(EstadoDeTransferencia.Enviada));
        }

        await ExigirElConflictoAsync(escena.EnviarPorLaApiAsync(transferenciaId), "transferencia-no-esta-en-borrador");

        using (HttpResponseMessage recepcion = await escena.RecibirPorLaApiAsync(transferenciaId, Hoy))
        {
            (await EscenaDeTransferencia.LeerAsync<TransferenciaDto>(recepcion)).Estado
                .ShouldBe(nameof(EstadoDeTransferencia.Recibida));
        }

        await ExigirElConflictoAsync(
            escena.RecibirPorLaApiAsync(transferenciaId, Hoy), "transferencia-no-esta-enviada");
        await ExigirElConflictoAsync(escena.EnviarPorLaApiAsync(transferenciaId), "transferencia-no-esta-en-borrador");

        AnulacionDeTransferenciaDto par;

        using (HttpResponseMessage anulacion = await escena.AnularPorLaApiAsync(transferenciaId))
        {
            par = await EscenaDeTransferencia.LeerAsync<AnulacionDeTransferenciaDto>(anulacion);
        }

        await ExigirElConflictoAsync(escena.AnularPorLaApiAsync(transferenciaId), "transferencia-no-se-anula");
        await ExigirElConflictoAsync(escena.AnularPorLaApiAsync(par.Inverso.Id), "transferencia-no-se-anula");
        await ExigirElConflictoAsync(escena.EnviarPorLaApiAsync(par.Inverso.Id), "transferencia-no-esta-en-borrador");
        await ExigirElConflictoAsync(
            escena.RecibirPorLaApiAsync(par.Inverso.Id, Hoy), "transferencia-no-esta-enviada");

        (await escena.ContadorAsync()).ShouldBe(2, "el envío y el inverso: ningún rechazo gastó número");
        (await escena.InversosDeAsync(postgres, transferenciaId)).ShouldBe(1);
        (await escena.InversosDeAsync(postgres, par.Inverso.Id)).ShouldBe(0, "un inverso no se anula");
        (await escena.FilasDelLibroAsync(postgres, transferenciaId)).Count.ShouldBe(2, "una salida y una entrada");
        (await escena.FilasDelLibroAsync(postgres, par.Inverso.Id)).Count.ShouldBe(2);
    }

    /// <summary>
    /// Una anulación sin motivo o con uno más largo que su columna es <c>400</c>, y con el ejercicio
    /// de hoy cerrado es <c>409</c>; con trescientos caracteres justos y el ejercicio abierto, pasa.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El motivo se pregunta al caso de uso</b>, que es donde vive la guarda: así el caso no
    /// depende de lo que el borde valide antes de llamarlo. Las mutaciones 228 y 229 la partían en sus
    /// dos mitades, el vacío y el largo, y las dos salieron verdes.
    /// </para>
    /// <para>
    /// <b>El ejercicio de hoy existe y está cerrado</b>, que no es lo de
    /// <c>Sin_ejercicio_para_el_dia_ni_se_recibe_ni_se_anula</c>, donde no existe. Una enviada sin
    /// recibir deja cerrarlo, y eso es lo que deja a la segunda sin poder anularse. La mutación 235
    /// solo paraba la anulación sin ejercicio.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task La_anulacion_pide_motivo_y_el_ejercicio_de_hoy_abierto()
    {
        EscenaDeTransferencia escena = await MontarAsync(728, "TRG-H");

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 5m, 2m);

        TransferenciaDto primera = await escena.EnviarAsync(postgres, 1m);
        TransferenciaDto segunda = await escena.EnviarAsync(postgres, 1m);

        await using (ElModuloDeInventario modulo = new(postgres, escena.EmpresaId))
        {
            foreach (string motivo in new[] { string.Empty, "   ", new string('x', Transferencia.LargoDelMotivo + 1) })
            {
                Resultado<AnulacionDeTransferenciaDto> rechazo =
                    await modulo.AnularLaTransferenciaAsync(primera.Id, motivo);

                rechazo.EsCorrecto.ShouldBeFalse($"con un motivo de {motivo.Length} caracteres");
                rechazo.Error!.Codigo.ShouldBe("transferencia-motivo-no-valido");
                rechazo.Error!.Tipo.ShouldBe(TipoDeError.Validacion);
            }

            (await escena.InversosDeAsync(postgres, primera.Id)).ShouldBe(0);

            Resultado<AnulacionDeTransferenciaDto> justa = await modulo.AnularLaTransferenciaAsync(
                primera.Id, new string('x', Transferencia.LargoDelMotivo));

            justa.EsCorrecto.ShouldBeTrue($"«{justa.Error?.Codigo}»");
        }

        using (HttpResponseMessage cerrado = await escena.CerrarElEjercicioAsync())
        {
            cerrado.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(cerrado));
        }

        await ExigirElConflictoAsync(escena.AnularPorLaApiAsync(segunda.Id), "transferencia-en-ejercicio-cerrado");

        (await escena.LaTransferenciaAsync(postgres, segunda.Id)).Estado.ShouldBe(EstadoDeTransferencia.Enviada);
        (await escena.InversosDeAsync(postgres, segunda.Id)).ShouldBe(0);
        (await escena.ContadorAsync()).ShouldBe(3, "las dos enviadas y el inverso de la primera");
    }

    /// <summary>
    /// El alta rechaza, cada cosa con su código, lo que no se puede mover: nada, una cantidad que no
    /// es positiva, un almacén que no existe, una línea que no casa con la marca, una serie cerrada,
    /// una empresa que no opera, una unidad retirada y un artículo que no se almacena.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La primera alta pasa</b>, con la misma línea que llevan los rechazos, y al final sigue
    /// siendo la única transferencia: cada rechazo para por lo que dice y no deja un borrador a
    /// medias. Salen de las mutaciones 230 a 232, 234 y 236 a 239.
    /// </para>
    /// <para>
    /// <b>Los dos últimos van en ese orden porque el alta pregunta por el artículo antes que por la
    /// unidad.</b> Con la unidad ya retirada, el artículo que pasa a servicio tiene que dar su código
    /// y no el de la unidad, y eso es lo que dice que la pregunta por el artículo está.
    /// </para>
    /// <para>
    /// <b>La empresa que no opera es una que no existe</b>: el módulo se abre con un identificador
    /// nuevo, y su divisa base no aparece. Sin la guarda, el alta seguiría y se pararía en el
    /// almacén, que no es de esa empresa, con otro código.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task El_alta_rechaza_cada_cosa_que_no_se_mueve_con_su_codigo()
    {
        EscenaDeTransferencia escena = await MontarAsync(729, "TRG-I");

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        LineaDeTransferenciaDto linea = escena.Linea(escena.UbicacionA, escena.UbicacionB, 1m);

        Resultado<TransferenciaDto> buena = await escena.IntentarAbrirAsync(
            modulo, escena.AlmacenA, escena.AlmacenB, [linea]);

        buena.EsCorrecto.ShouldBeTrue($"«{buena.Error?.Codigo}»");

        ExigirElError(
            await escena.IntentarAbrirAsync(modulo, escena.AlmacenA, escena.AlmacenB, []),
            "transferencia-sin-lineas",
            TipoDeError.Validacion);

        foreach (decimal cantidad in new[] { 0m, -1m })
        {
            await ExigirElRechazoAsync(
                escena,
                modulo,
                escena.AlmacenA,
                escena.AlmacenB,
                escena.Linea(escena.UbicacionA, escena.UbicacionB, cantidad),
                "transferencia-cantidad-no-valida",
                TipoDeError.Validacion);
        }

        await ExigirElRechazoAsync(
            escena,
            modulo,
            escena.AlmacenA,
            Guid.CreateVersion7(),
            linea,
            "transferencia-almacen-no-encontrado",
            TipoDeError.Validacion);

        await ExigirElRechazoAsync(
            escena,
            modulo,
            escena.AlmacenA,
            escena.AlmacenB,
            escena.Linea(escena.UbicacionA, escena.UbicacionB, 1m, serie: "SN-TRG-I"),
            "transferencia-trazabilidad-no-casa",
            TipoDeError.Conflicto);

        SerieDto cerrada = await LosMaestrosPorLaApi.CrearSerieEnAsync(
            escena.Cliente, escena.Ejercicio.Id, "TRG-I-T2", TipoDeDocumento.TransferenciaDeInventario);

        await escena.CerrarLaSerieAsync(postgres, cerrada.Id);

        ExigirElError(
            await modulo.AltaDeTransferencia.EjecutarAsync(
                new AbrirTransferenciaDto(cerrada.Id, escena.AlmacenA, escena.AlmacenB, Hoy, [linea]),
                CancellationToken.None),
            "transferencia-serie-cerrada",
            TipoDeError.Conflicto);

        await using (ElModuloDeInventario deNinguna = new(postgres, Guid.CreateVersion7()))
        {
            ExigirElError(
                await escena.IntentarAbrirAsync(deNinguna, escena.AlmacenA, escena.AlmacenB, [linea]),
                "empresa-activa-no-operativa",
                TipoDeError.Conflicto);
        }

        string unidad = $"{LosMaestrosPorLaApi.Unidades}/{escena.UnidadId}";

        using (HttpResponseMessage retirada = await escena.Cliente.AccionarAsync(
            unidad, $"{unidad}/retirada", HttpMethod.Post))
        {
            retirada.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(retirada));
        }

        await ExigirElRechazoAsync(
            escena,
            modulo,
            escena.AlmacenA,
            escena.AlmacenB,
            linea,
            "transferencia-unidad-retirada",
            TipoDeError.Conflicto);

        using (HttpResponseMessage servicio = await escena.CambiarElArticuloAsync("Servicio", "Ninguna"))
        {
            servicio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(servicio));
        }

        await ExigirElRechazoAsync(
            escena,
            modulo,
            escena.AlmacenA,
            escena.AlmacenB,
            linea,
            "transferencia-articulo-no-se-almacena",
            TipoDeError.Conflicto);

        await using InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId);

        (await contexto.Transferencias.CountAsync()).ShouldBe(1, "un alta rechazada no deja un borrador a medias");
    }

    /// <summary>
    /// Un borrador abierto con la marca de antes no se envía: el envío la vuelve a leer, con cerrojo,
    /// y la línea ya no casa. Con la marca de vuelta, sale.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es la guarda de verdad</b> (ADR-0048 §4). La del alta es de cortesía, y entre las dos la
    /// marca puede cambiar, porque un borrador no es un movimiento. La mutación 233 quitaba esta y
    /// salió verde.
    /// </para>
    /// <para>
    /// <b>El origen está vacío cuando se rechaza</b>, a propósito: sin la guarda, el envío seguiría
    /// hasta el libro y se pararía en el <c>422</c> del stock, que no es lo que tiene que contestar.
    /// La entrada en A va después de devolver la marca, porque con el artículo ya movido la marca no
    /// cambia.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task El_envio_vuelve_a_leer_la_marca_y_no_saca_un_borrador_que_ya_no_casa()
    {
        EscenaDeTransferencia escena = await MontarAsync(733, "TRG-J");

        Guid borrador = await escena.AbrirAsync(postgres, 1m);

        using (HttpResponseMessage cambio = await escena.CambiarElArticuloAsync("Bien", "PorLote"))
        {
            cambio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(cambio));
        }

        await ExigirElConflictoAsync(escena.EnviarPorLaApiAsync(borrador), "transferencia-trazabilidad-no-casa");

        (await escena.LaTransferenciaAsync(postgres, borrador)).Estado.ShouldBe(EstadoDeTransferencia.Borrador);
        (await escena.ContadorAsync()).ShouldBe(0, "el rechazo no gastó número");

        using (HttpResponseMessage vuelta = await escena.CambiarElArticuloAsync("Bien", "Ninguna"))
        {
            vuelta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(vuelta));
        }

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 2m, 3m);

        using HttpResponseMessage envio = await escena.EnviarPorLaApiAsync(borrador);

        (await EscenaDeTransferencia.LeerAsync<TransferenciaDto>(envio)).Estado
            .ShouldBe(nameof(EstadoDeTransferencia.Enviada));
    }

    /// <summary>
    /// Una clave vaciada en la divisa de antes recibe el tránsito en la nueva: la valoración del
    /// destino cambia de divisa con lo primero que le llega.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es la otra mitad de <c>Una_punta_valorada_en_otra_divisa_para_el_envio_por_los_dos_lados</c></b>:
    /// allí el destino tiene existencias en euros y el envío se para; aquí las tuvo, y ya no. Una
    /// clave vacía no tiene nada que convertir (ADR-0053 §1), así que empieza de nuevo en la divisa
    /// del documento.
    /// </para>
    /// <para>
    /// <b>Se mira mientras vuela</b>, porque la recepción vuelve a poner la divisa al sumar al libro y
    /// taparía que el tránsito la dejó en euros. Y la clave ya existía: hacia una que no existe, la
    /// fila nace en la divisa del documento, y la sentencia del tránsito no tiene nada que cambiar.
    /// La mutación 244 le quitaba ese cambio y salió verde.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Una_clave_vaciada_en_la_divisa_de_antes_recibe_el_transito_en_la_nueva()
    {
        EscenaDeTransferencia escena = await MontarAsync(734, "TRG-K");

        await escena.EntrarAsync(postgres, escena.AlmacenB, escena.UbicacionB, 2m, 4m);
        await escena.SalirAsync(postgres, escena.AlmacenB, escena.UbicacionB, 2m);

        Valoracion vaciada = await escena.LaValoracionDeAsync(postgres, escena.AlmacenB);

        vaciada.Saldo.EstaVacio.ShouldBeTrue();
        vaciada.Valor.ShouldBe(Importe.De(0m, "EUR"), "la clave existe, vacía y en euros");

        using (HttpResponseMessage cambio = await escena.Cliente.ModificarAsync(
            $"{RutaDeEmpresas}/{escena.EmpresaId}",
            new ModificarEmpresaDto
            {
                RazonSocial = "Empresa que pasa a llevar las cuentas en dólares",
                DomicilioFiscal = Escenario.Domicilio(),
                DivisaBase = "USD",
                RegimenDeIva = "General",
            }))
        {
            cambio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(cambio));
        }

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 3m, 2m);

        TransferenciaDto enviada = await escena.EnviarAsync(postgres, 1m);

        enviada.Divisa.ShouldBe("USD");

        Valoracion enVuelo = await escena.LaValoracionDeAsync(postgres, escena.AlmacenB);

        enVuelo.EnTransito.ShouldBe(1m);
        enVuelo.ValorEnTransito.ShouldBe(Importe.De(2m, "USD"));
        enVuelo.Valor.ShouldBe(Importe.De(0m, "USD"), "la clave entera pasa a la divisa nueva, no solo lo que vuela");

        using HttpResponseMessage recibida = await escena.RecibirPorLaApiAsync(enviada.Id, Hoy);

        recibida.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(recibida));

        Valoracion llegada = await escena.LaValoracionDeAsync(postgres, escena.AlmacenB);

        llegada.Cantidad.ShouldBe(1m);
        llegada.Valor.ShouldBe(Importe.De(2m, "USD"));
    }

    private static async Task ExigirElConflictoAsync(Task<HttpResponseMessage> peticion, string codigo)
    {
        using HttpResponseMessage respuesta = await peticion;

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(respuesta));
        (await EscenaDeTransferencia.TipoDelProblemaAsync(respuesta)).ShouldBe("/errors/" + codigo);
    }

    private static void ExigirElError(Resultado<TransferenciaDto> alta, string codigo, TipoDeError tipo)
    {
        alta.EsCorrecto.ShouldBeFalse($"tenía que salir «{codigo}»");
        alta.Error!.Codigo.ShouldBe(codigo);
        alta.Error!.Tipo.ShouldBe(tipo);
    }

    private static async Task ExigirElRechazoAsync(
        EscenaDeTransferencia escena,
        ElModuloDeInventario modulo,
        Guid origen,
        Guid destino,
        LineaDeTransferenciaDto linea,
        string codigo,
        TipoDeError tipo) =>
        ExigirElError(await escena.IntentarAbrirAsync(modulo, origen, destino, [linea]), codigo, tipo);

    private static async Task<Guid> AbrirAsync(
        EscenaDeTransferencia escena,
        ElModuloDeInventario modulo,
        Guid origen,
        Guid destino,
        LineaDeTransferenciaDto linea)
    {
        Resultado<TransferenciaDto> alta = await escena.IntentarAbrirAsync(modulo, origen, destino, [linea]);

        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");

        return alta.Valor.Id;
    }

    private static async Task<HttpResponseMessage> EncogerAsync(
        EscenaDeTransferencia escena, string recurso, DateOnly inicio, DateOnly fin) =>
        await escena.Cliente.EnviarConVersionAsync(
            HttpMethod.Put,
            recurso,
            await escena.Cliente.EtiquetaDeAsync(recurso),
            JsonContent.Create(new ModificarEjercicioDto { FechaDeInicio = inicio, FechaDeFin = fin }));

    private static async Task<string> DetalleDelProblemaAsync(HttpResponseMessage respuesta)
    {
        using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        return problema.RootElement.GetProperty("detail").GetString() ?? string.Empty;
    }

    private async Task<EscenaDeTransferencia> MontarAsync(int semilla, string codigo, int? anio = null)
    {
        EscenaDeTransferencia escena =
            await EscenaDeTransferencia.MontarAsync(_api, semilla, codigo, semilla, anio: anio);

        _clientes.Add(escena.Cliente);

        return escena;
    }
}
