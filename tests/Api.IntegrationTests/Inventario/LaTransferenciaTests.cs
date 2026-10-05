using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Application.Idempotencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Domain.Existencias;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Transferencias;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Existencias;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// La transferencia de punta a punta (ítem 2.11): sale de un almacén, viaja y llega a otro, con dos
/// movimientos por línea, el tránsito contado mientras vuela, su número, su inverso y su ejercicio.
/// </summary>
/// <remarks>
/// <para>
/// <b>El tránsito vive en el destino</b> (ADR-0053 §1): lo que ha salido y no ha llegado está en
/// <c>existencias.en_transito</c> y en la valoración del almacén que lo espera, con el valor que se
/// llevó. Por eso estos casos miran las dos puntas a la vez y el valor de la empresa, que solo
/// cambian los ajustes (§6): una transferencia mueve valor de sitio, no lo crea ni lo destruye.
/// </para>
/// <para>
/// <b>Las cifras están elegidas para que el precio medio no sea redondo.</b> Siete unidades por
/// 19,00 dan un medio periódico, y dos de ellas se llevan 5,4286: un caso que valorara la salida a
/// coste o al último precio daría otra cifra, y uno que perdiera el redondeo por el camino no
/// cerraría el valor de la empresa en 39,00.
/// </para>
/// <para>
/// <b>Semillas: del 700 al 712, el 719 y el 720</b>, las empresas y, con el mismo número, la unidad y
/// el tramo de impuesto de cada artículo, que son maestros de instalación. Del 640 al 749 no había
/// ninguna, ni por literal ni por cálculo: la más alta calculada es la de
/// <c>ElSaldoEsLaSumaDelLibroPorPropiedadTests</c>, <c>465 + 130 = 595</c>, y el 750 de
/// <c>ElCerrojoDeLaNumeracionTests</c> son milisegundos. Las guardas de la transferencia, en
/// <c>LasGuardasDeLaTransferenciaTests</c>, van del 713 al 718, y las carreras, en
/// <c>LasCarrerasDeLaTransferenciaTests</c>, del 730 al 732. El cuadre del tránsito, en
/// <c>ElCuadreDelTransitoTests</c>, va del 721 al 725.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaTransferenciaTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    // 2 × 19,00 / 7, a cuatro decimales: lo que se lleva la transferencia de la escena de siempre.
    private const decimal Viaja = 5.4286m;

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
    /// Enviada y recibida: dos filas del libro, una en cada almacén, y entre medias el tránsito en el
    /// destino con el valor que salió del origen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El destino ya tiene existencias, y a otro precio</b>, para que la recepción mezcle: lo que
    /// llega entra con el valor que viajó, no con el medio del destino ni con el del origen al
    /// recibir. Y el cuadre del libro sale limpio en los dos momentos, porque el tránsito no está
    /// en el libro: está en la copia, y solo en la columna que lo dice.
    /// </para>
    /// <para>
    /// <b>Mientras vuela, entra en A una unidad a 10,00.</b> Sin ella, el medio del origen al recibir
    /// seguiría en 2,71428, y dos unidades a ese medio son 5,4286 por casualidad: el caso no
    /// distinguiría recibir el valor que viajó de recibir al medio del origen. Con ella, el medio pasa
    /// a 3,928567, y esa regla daría 7,8571.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Enviada_y_recibida_mueve_dos_veces_y_el_transito_cuenta_mientras_viaja()
    {
        EscenaDeTransferencia escena = await MontarAsync(700, "TRF-A");

        await LaEscenaDeSiempreAsync(escena);

        TransferenciaDto enviada = await escena.EnviarAsync(postgres, 2m);

        enviada.Estado.ShouldBe(nameof(EstadoDeTransferencia.Enviada));
        enviada.Numero.ShouldBe(1, "el número se toma al enviar (ADR-0053 §3)");
        enviada.FechaDeEnvio.ShouldBe(Hoy);
        enviada.FechaDeRecepcion.ShouldBeNull();

        Transferencia comoSalio = await escena.LaTransferenciaAsync(postgres, enviada.Id);

        comoSalio.Lineas.ShouldHaveSingleItem().Valor.ShouldBe(
            Viaja, "la salida se valora al medio del origen, 19,00 entre 7, y la línea guarda lo que se llevó (§2)");

        await ExigirLaValoracionAsync(escena, escena.AlmacenA, cantidad: 5m, valor: 19m - Viaja);
        await ExigirLaValoracionAsync(
            escena, escena.AlmacenB, cantidad: 5m, valor: 20m, enTransito: 2m, valorEnTransito: Viaja);

        Existencia enB = await LaExistenciaDeAsync(escena, escena.AlmacenB);

        enB.Fisico.ShouldBe(5m, "lo que vuela todavía no está en la estantería");
        enB.EnTransito.ShouldBe(2m, "pero se cuenta en el destino que lo espera (§1)");

        MovimientoStock salida = (await escena.FilasDelLibroAsync(postgres, enviada.Id)).ShouldHaveSingleItem(
            "enviada, solo se ha movido el origen: el tránsito no es una fila del libro");

        ExigirLaFila(salida, escena.AlmacenA, -2m, -Viaja, Hoy);

        (await escena.ElValorDeLaEmpresaAsync(postgres)).ShouldBe(
            39m, "lo que salió de A está en vuelo hacia B, y la empresa no ha perdido nada (§6)");

        await ExigirQueCuadraAsync(escena, existencias: 2, enTransito: 1, valoracionesEnTransito: 1);

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 1m, 10m);

        using HttpResponseMessage respuesta = await escena.RecibirPorLaApiAsync(enviada.Id, Hoy);
        TransferenciaDto recibida = await EscenaDeTransferencia.LeerAsync<TransferenciaDto>(respuesta);

        recibida.Estado.ShouldBe(nameof(EstadoDeTransferencia.Recibida));
        recibida.Numero.ShouldBe(1, "recibir no numera");
        recibida.FechaDeRecepcion.ShouldBe(Hoy);
        (await escena.ContadorAsync()).ShouldBe(1);

        await ExigirLaValoracionAsync(escena, escena.AlmacenA, cantidad: 6m, valor: 19m - Viaja + 10m);
        await ExigirLaValoracionAsync(escena, escena.AlmacenB, cantidad: 7m, valor: 20m + Viaja);

        enB = await LaExistenciaDeAsync(escena, escena.AlmacenB);

        enB.Fisico.ShouldBe(7m);
        enB.EnTransito.ShouldBe(0m, "lo que llegó deja de volar");

        IReadOnlyList<MovimientoStock> filas = await escena.FilasDelLibroAsync(postgres, enviada.Id);

        filas.Count.ShouldBe(2, "dos movimientos por línea: la salida del origen y la entrada en el destino");
        ExigirLaFila(filas[0], escena.AlmacenA, -2m, -Viaja, Hoy);
        ExigirLaFila(filas[1], escena.AlmacenB, 2m, Viaja, Hoy);

        (await escena.ElValorDeLaEmpresaAsync(postgres)).ShouldBe(
            49m, "los 39,00 de antes y los 10,00 de la entrada: recibir no crea ni destruye valor");

        await ExigirQueCuadraAsync(escena, existencias: 2);
    }

    /// <summary>
    /// Llevarse todo lo que hay en el origen se lleva todo su valor, y no deja un resto de redondeo
    /// colgando de una clave sin cantidad.
    /// </summary>
    /// <remarks>
    /// <b>Trescientas unidades por 100,00 dan un medio de 0,333…</b>, y 300 × 0,333333 son 99,9999.
    /// Esa diezmilésima no puede quedarse en el origen, porque una valoración sin cantidad no tiene
    /// valor (<c>ck_valoraciones_sin_cantidad_no_hay_valor</c>), ni perderse, porque el valor de la
    /// empresa no lo cambia una transferencia: viaja entera (ADR-0054).
    /// </remarks>
    [Fact]
    public async Task Vaciar_el_origen_se_lleva_todo_su_valor_sin_dejar_un_resto()
    {
        EscenaDeTransferencia escena = await MontarAsync(701, "TRF-B");

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 100m, 0.50m);
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 200m, 0.25m);

        TransferenciaDto enviada = await escena.EnviarAsync(postgres, 300m);

        (await escena.LaTransferenciaAsync(postgres, enviada.Id)).Lineas.ShouldHaveSingleItem().Valor.ShouldBe(100m);

        await ExigirLaValoracionAsync(escena, escena.AlmacenA, cantidad: 0m, valor: 0m);
        await ExigirLaValoracionAsync(
            escena, escena.AlmacenB, cantidad: 0m, valor: 0m, enTransito: 300m, valorEnTransito: 100m);

        using HttpResponseMessage respuesta = await escena.RecibirPorLaApiAsync(enviada.Id, Hoy);
        await EscenaDeTransferencia.LeerAsync<TransferenciaDto>(respuesta);

        await ExigirLaValoracionAsync(escena, escena.AlmacenB, cantidad: 300m, valor: 100m);

        (await escena.ElValorDeLaEmpresaAsync(postgres)).ShouldBe(100m);

        await ExigirQueCuadraAsync(escena, existencias: 2);
    }

    /// <summary>
    /// Enviada el 30 de diciembre y recibida el 3 de enero: numera en la serie del año del envío, y
    /// cada fila del libro lleva su fecha, una en cada año.
    /// </summary>
    /// <remarks>
    /// <b>Dos años pasados, y no el actual y el siguiente</b>, para que las dos fechas ya hayan
    /// pasado cualquier día que corra el carril: el 3 de enero de este año todavía es futuro el 1 y el
    /// 2 de enero, y la recepción con fecha futura es un <c>422</c> (§3).
    /// </remarks>
    [Fact]
    public async Task El_cambio_de_anio_numera_en_el_del_envio_y_recibe_en_el_siguiente()
    {
        int anio = Hoy.Year - 2;
        EscenaDeTransferencia escena = await MontarAsync(702, "TRF-C", anio: anio);

        await LosMaestrosPorLaApi.CrearEjercicioAsync(escena.Cliente, anio + 1);

        var envio = new DateOnly(anio, 12, 30);
        var recepcion = new DateOnly(anio + 1, 1, 3);

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 5m, 2m, new DateOnly(anio, 12, 20));

        TransferenciaDto enviada = await escena.EnviarAsync(postgres, 3m, envio);

        enviada.SerieId.ShouldBe(escena.SerieDeTransferencias.Id);
        enviada.Numero.ShouldBe(1, "en la serie del ejercicio de la fecha de envío (§3)");

        using HttpResponseMessage respuesta = await escena.RecibirPorLaApiAsync(enviada.Id, recepcion);
        TransferenciaDto recibida = await EscenaDeTransferencia.LeerAsync<TransferenciaDto>(respuesta);

        recibida.FechaDeEnvio.ShouldBe(envio);
        recibida.FechaDeRecepcion.ShouldBe(recepcion);

        IReadOnlyList<MovimientoStock> filas = await escena.FilasDelLibroAsync(postgres, enviada.Id);

        filas.Count.ShouldBe(2);
        ExigirLaFila(filas[0], escena.AlmacenA, -3m, -6m, envio);
        ExigirLaFila(filas[1], escena.AlmacenB, 3m, 6m, recepcion);

        (await escena.LaValoracionDeAsync(postgres, escena.AlmacenA)).UltimaFecha.ShouldBe(envio);
        (await escena.LaValoracionDeAsync(postgres, escena.AlmacenB)).UltimaFecha.ShouldBe(recepcion);

        await ExigirQueCuadraAsync(escena, existencias: 2);
    }

    /// <summary>
    /// Cerrado el ejercicio, ni se envía con una fecha suya ni se recibe con una fecha suya, y no se
    /// gasta un número; la recepción en el ejercicio siguiente sí pasa.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La enviada no impide el cierre</b>, y eso también es el caso: solo lo impiden los
    /// borradores (§12). Lo que queda en vuelo al cerrar se recibe en el ejercicio de la recepción,
    /// que es el que tiene que estar abierto.
    /// </para>
    /// <para>
    /// <b>El segundo borrador nace después del cierre</b>, por el único hueco por el que se puede
    /// intentar dejar un documento dentro: el alta mira la serie, que sigue activa, y quien lo para
    /// es la comprobación del ejercicio al enviar.
    /// </para>
    /// <para>
    /// <b>Que la guarda va antes del numerador no lo dice el contador.</b> El filtro de idempotencia
    /// deshace todo lo que no es un 2xx, así que el número vuelve a la serie tanto si se tomó como si
    /// no, y el contador en uno solo dice que el rechazo no gasta ninguno. El orden lo dice el
    /// cerrojo de la fila del contador, mirado con el envío rechazado y su transacción todavía
    /// abierta. Su pareja, el cerrojo puesto, la mide la carrera de recibir y anular.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Un_ejercicio_cerrado_no_admite_ni_el_envio_ni_la_recepcion()
    {
        int anio = Hoy.Year - 2;
        EscenaDeTransferencia escena = await MontarAsync(703, "TRF-D", anio: anio);

        await LosMaestrosPorLaApi.CrearEjercicioAsync(escena.Cliente, anio + 1);

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 10m, 1m, new DateOnly(anio, 12, 1));

        TransferenciaDto enviada = await escena.EnviarAsync(postgres, 4m, new DateOnly(anio, 12, 10));

        await CerrarPorLaApiAsync(escena);

        using (HttpResponseMessage enElCerrado =
            await escena.RecibirPorLaApiAsync(enviada.Id, new DateOnly(anio, 12, 20)))
        {
            enElCerrado.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(enElCerrado));
            (await TipoDelProblemaAsync(enElCerrado)).ShouldBe("/errors/transferencia-en-ejercicio-cerrado");
        }

        (await escena.LaTransferenciaAsync(postgres, enviada.Id)).Estado.ShouldBe(EstadoDeTransferencia.Enviada);
        (await escena.FilasDelLibroAsync(postgres, enviada.Id)).Count.ShouldBe(1, "la recepción rechazada no deja filas");

        Guid borrador = await escena.AbrirAsync(postgres, 1m, new DateOnly(anio, 12, 15));

        using (HttpResponseMessage envioEnElCerrado = await escena.EnviarPorLaApiAsync(borrador))
        {
            envioEnElCerrado.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(envioEnElCerrado));
            (await TipoDelProblemaAsync(envioEnElCerrado)).ShouldBe("/errors/transferencia-en-ejercicio-cerrado");
        }

        (await escena.ContadorAsync()).ShouldBe(
            1, "el envío rechazado no deja un número gastado: su transacción se deshizo entera");

        await using (ElModuloDeInventario modulo = new(postgres, escena.EmpresaId))
        {
            (Resultado<TransferenciaDto> envio, IDbContextTransaction abierta) =
                await modulo.EnviarYQuedarseDentroAsync(borrador);

            await using (abierta)
            {
                envio.EsCorrecto.ShouldBeFalse();
                envio.Error!.Codigo.ShouldBe("transferencia-en-ejercicio-cerrado");

                (await escena.ElContadorEstaBloqueadoAsync(postgres)).ShouldBeFalse(
                    "la guarda del ejercicio va antes del numerador: el periodo que no admite el documento " +
                    "no llega a pedirle un número a la serie");

                await abierta.RollbackAsync();
            }
        }

        (await escena.LaTransferenciaAsync(postgres, borrador)).Estado.ShouldBe(EstadoDeTransferencia.Borrador);

        using HttpResponseMessage enElAbierto =
            await escena.RecibirPorLaApiAsync(enviada.Id, new DateOnly(anio + 1, 1, 3));

        (await EscenaDeTransferencia.LeerAsync<TransferenciaDto>(enElAbierto)).Estado
            .ShouldBe(nameof(EstadoDeTransferencia.Recibida), "lo que voló al cerrar se recibe en el ejercicio siguiente");
    }

    /// <summary>
    /// Anular una transferencia de un ejercicio cerrado pone el inverso en el abierto, con la fecha de
    /// hoy, y lo numera en la serie del original.
    /// </summary>
    [Fact]
    public async Task Anular_la_de_un_ejercicio_cerrado_deja_el_inverso_en_el_abierto()
    {
        int anio = Hoy.Year - 2;
        EscenaDeTransferencia escena = await MontarAsync(704, "TRF-E", anio: anio);

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 6m, 2m, new DateOnly(anio, 12, 1));

        var envio = new DateOnly(anio, 12, 10);
        TransferenciaDto enviada = await escena.EnviarAsync(postgres, 4m, envio);

        await LosMaestrosPorLaApi.CrearEjercicioAsync(escena.Cliente, Hoy.Year);
        await CerrarPorLaApiAsync(escena);

        using HttpResponseMessage respuesta = await escena.AnularPorLaApiAsync(enviada.Id);
        AnulacionDeTransferenciaDto anulacion =
            await EscenaDeTransferencia.LeerAsync<AnulacionDeTransferenciaDto>(respuesta);

        anulacion.Original.Estado.ShouldBe(nameof(EstadoDeTransferencia.Anulada));
        anulacion.Original.FechaDeEnvio.ShouldBe(envio, "el cerrado no se toca: el original sigue con su fecha");

        anulacion.Inverso.AnulaAId.ShouldBe(enviada.Id);
        anulacion.Inverso.Estado.ShouldBe(nameof(EstadoDeTransferencia.Recibida), "el inverso nace recibido (§5)");
        anulacion.Inverso.FechaDeEnvio.ShouldBe(Hoy);
        anulacion.Inverso.FechaDeRecepcion.ShouldBe(Hoy);

        // LA SERIE DEL ORIGINAL, QUE CUELGA DEL EJERCICIO CERRADO, con la fecha de envío del
        // original: es la excepción del ADR-0043 §4, y con la fecha de hoy la sentencia contestaría
        // que cae fuera del ejercicio de la serie.
        anulacion.Inverso.SerieId.ShouldBe(escena.SerieDeTransferencias.Id);
        anulacion.Inverso.Numero.ShouldBe(2);

        MovimientoStock vuelta = (await escena.FilasDelLibroAsync(postgres, anulacion.Inverso.Id)).ShouldHaveSingleItem(
            "de una enviada, el inverso solo devuelve al origen: el destino no llegó a recibir nada");

        ExigirLaFila(vuelta, escena.AlmacenA, 4m, 8m, Hoy);

        await ExigirLaValoracionAsync(escena, escena.AlmacenA, cantidad: 6m, valor: 12m);
        await ExigirLaValoracionAsync(escena, escena.AlmacenB, cantidad: 0m, valor: 0m);
    }

    /// <summary>
    /// Anular una enviada baja el tránsito del destino y devuelve al origen exactamente lo que salió,
    /// con su valor, aunque el medio del origen ya no sea el de entonces.
    /// </summary>
    /// <remarks>
    /// <b>Entre el envío y la anulación entra en A una unidad a 10,00</b>, y el medio del origen pasa
    /// de 2,71428 a 3,928567. Sin esa entrada, las dos unidades valdrían al medio de hoy 5,4286, lo
    /// mismo que salió, y el caso no distinguiría devolver el valor que viajó de revalorarlo al medio
    /// del día. Con ella, revalorar daría 7,8571.
    /// </remarks>
    [Fact]
    public async Task Anular_una_enviada_deshace_el_transito_y_devuelve_al_origen_lo_que_salio()
    {
        EscenaDeTransferencia escena = await MontarAsync(705, "TRF-F");

        await LaEscenaDeSiempreAsync(escena);

        TransferenciaDto enviada = await escena.EnviarAsync(postgres, 2m);

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 1m, 10m);

        using HttpResponseMessage respuesta = await escena.AnularPorLaApiAsync(enviada.Id);
        AnulacionDeTransferenciaDto anulacion =
            await EscenaDeTransferencia.LeerAsync<AnulacionDeTransferenciaDto>(respuesta);

        anulacion.Inverso.Numero.ShouldBe(2, "en la serie del original, el siguiente");
        (await escena.ContadorAsync()).ShouldBe(2);

        await ExigirLaValoracionAsync(escena, escena.AlmacenA, cantidad: 8m, valor: 19m + 10m);
        await ExigirLaValoracionAsync(escena, escena.AlmacenB, cantidad: 5m, valor: 20m);

        (await LaExistenciaDeAsync(escena, escena.AlmacenB)).EnTransito.ShouldBe(0m);

        MovimientoStock vuelta = (await escena.FilasDelLibroAsync(postgres, anulacion.Inverso.Id)).ShouldHaveSingleItem();

        ExigirLaFila(vuelta, escena.AlmacenA, 2m, Viaja, Hoy);

        (await escena.ElValorDeLaEmpresaAsync(postgres)).ShouldBe(49m);
        (await escena.LaTransferenciaAsync(postgres, enviada.Id)).Estado.ShouldBe(EstadoDeTransferencia.Anulada);

        await ExigirQueCuadraAsync(escena, existencias: 2);
    }

    /// <summary>
    /// Anular una recibida saca del destino el valor que entró, no su precio medio, y lo devuelve
    /// al origen: las dos puntas quedan como antes de enviar.
    /// </summary>
    /// <remarks>
    /// <b>La salida del destino se lleva el valor que entró</b>, como mucho el que queda (ADR-0053
    /// §5). Al recibir, las dos unidades se mezclaron con las cinco de 4,00, y al medio de las siete
    /// saldrían 7,2653: el destino se quedaría con 18,1633 por cinco unidades que entraron por 20,00,
    /// y el origen recibiría más de lo que salió de él. Lo que sale es 5,4286, lo que viajó, y el
    /// valor de la empresa sigue en 39,00 porque lo que sale de B entra en A céntimo a céntimo (§6).
    /// </remarks>
    [Fact]
    public async Task Anular_una_recibida_saca_del_destino_el_valor_que_entro_y_lo_devuelve_al_origen()
    {
        EscenaDeTransferencia escena = await MontarAsync(706, "TRF-G");

        await LaEscenaDeSiempreAsync(escena);

        TransferenciaDto enviada = await escena.EnviarAsync(postgres, 2m);

        using (HttpResponseMessage recepcion = await escena.RecibirPorLaApiAsync(enviada.Id, Hoy))
        {
            await EscenaDeTransferencia.LeerAsync<TransferenciaDto>(recepcion);
        }

        using HttpResponseMessage respuesta = await escena.AnularPorLaApiAsync(enviada.Id);
        AnulacionDeTransferenciaDto anulacion =
            await EscenaDeTransferencia.LeerAsync<AnulacionDeTransferenciaDto>(respuesta);

        await ExigirLaValoracionAsync(escena, escena.AlmacenB, cantidad: 5m, valor: 20m);
        await ExigirLaValoracionAsync(escena, escena.AlmacenA, cantidad: 7m, valor: 19m);

        IReadOnlyList<MovimientoStock> filas = await escena.FilasDelLibroAsync(postgres, anulacion.Inverso.Id);

        filas.Count.ShouldBe(2, "de una recibida, el inverso niega las dos patas");
        ExigirLaFila(filas[0], escena.AlmacenB, -2m, -Viaja, Hoy);
        ExigirLaFila(filas[1], escena.AlmacenA, 2m, Viaja, Hoy);

        (await escena.LaTransferenciaAsync(postgres, enviada.Id)).Estado.ShouldBe(
            EstadoDeTransferencia.Anulada, "el original pasa a anulada venga de enviada o de recibida (§5)");
        (await escena.ElValorDeLaEmpresaAsync(postgres)).ShouldBe(39m);

        await ExigirQueCuadraAsync(escena, existencias: 2);
    }

    /// <summary>
    /// Si la anulación de una recibida vacía el destino, se lleva todo lo que queda en él, aunque
    /// valga más de lo que entró, y eso es lo que recibe el origen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es el caso del ADR-0054, con sus cifras.</b> A tiene 5 unidades por 50,00 y B, 10 por
    /// 200,00. Las 5 viajan por 50,00, y B queda con 15 por 250,00. Un ajuste saca 10 de B a su
    /// medio, 166,6667, y B se queda con 5 por 83,3333. Anular saca esas 5: vacía la clave, y una
    /// valoración sin cantidad no puede tener valor (<c>ck_valoraciones_sin_cantidad_no_hay_valor</c>),
    /// así que se lleva los 83,3333 enteros, no los 50,00 que entraron.
    /// </para>
    /// <para>
    /// <b>El valor de la empresa no cambia al anular</b>: ya lo cambió el ajuste, que es lo único
    /// que puede (ADR-0053 §6).
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Anular_una_recibida_que_vacia_el_destino_se_lleva_todo_lo_que_queda_en_el()
    {
        EscenaDeTransferencia escena = await MontarAsync(712, "TRF-M");

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 5m, 10m);
        await escena.EntrarAsync(postgres, escena.AlmacenB, escena.UbicacionB, 10m, 20m);

        TransferenciaDto enviada = await escena.EnviarAsync(postgres, 5m);

        using (HttpResponseMessage recepcion = await escena.RecibirPorLaApiAsync(enviada.Id, Hoy))
        {
            await EscenaDeTransferencia.LeerAsync<TransferenciaDto>(recepcion);
        }

        await escena.SalirAsync(postgres, escena.AlmacenB, escena.UbicacionB, 10m);

        await ExigirLaValoracionAsync(escena, escena.AlmacenB, cantidad: 5m, valor: 83.3333m);
        (await escena.ElValorDeLaEmpresaAsync(postgres)).ShouldBe(83.3333m);

        using HttpResponseMessage respuesta = await escena.AnularPorLaApiAsync(enviada.Id);
        AnulacionDeTransferenciaDto anulacion =
            await EscenaDeTransferencia.LeerAsync<AnulacionDeTransferenciaDto>(respuesta);

        await ExigirLaValoracionAsync(escena, escena.AlmacenB, cantidad: 0m, valor: 0m);
        await ExigirLaValoracionAsync(escena, escena.AlmacenA, cantidad: 5m, valor: 83.3333m);

        IReadOnlyList<MovimientoStock> filas = await escena.FilasDelLibroAsync(postgres, anulacion.Inverso.Id);

        filas.Count.ShouldBe(2);
        ExigirLaFila(filas[0], escena.AlmacenB, -5m, -83.3333m, Hoy);
        ExigirLaFila(filas[1], escena.AlmacenA, 5m, 83.3333m, Hoy);

        (await escena.LaTransferenciaAsync(postgres, enviada.Id)).Estado.ShouldBe(EstadoDeTransferencia.Anulada);

        (await escena.ElValorDeLaEmpresaAsync(postgres)).ShouldBe(83.3333m);

        await ExigirQueCuadraAsync(escena, existencias: 2);
    }

    /// <summary>
    /// Si el destino ya consumió lo que recibió, anular es un <c>422</c> <c>stock-insuficiente</c> y
    /// no escribe nada; cuando las unidades vuelven, la misma transferencia se anula.
    /// </summary>
    /// <remarks>
    /// <b>No hay camino propio para la anulación</b> (ADR-0046 §1, ADR-0053 §5): el inverso de una
    /// recibida es una salida del destino, y si las unidades ya no están, choca con la guarda de la
    /// existencia como cualquier salida. La transacción del filtro se deshace entera, número
    /// incluido.
    /// </remarks>
    [Fact]
    public async Task Si_el_destino_ya_lo_consumio_la_anulacion_es_stock_insuficiente_y_no_escribe_nada()
    {
        EscenaDeTransferencia escena = await MontarAsync(707, "TRF-H");

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 6m, 2m);

        TransferenciaDto enviada = await escena.EnviarAsync(postgres, 4m);

        using (HttpResponseMessage recepcion = await escena.RecibirPorLaApiAsync(enviada.Id, Hoy))
        {
            await EscenaDeTransferencia.LeerAsync<TransferenciaDto>(recepcion);
        }

        await escena.SalirAsync(postgres, escena.AlmacenB, escena.UbicacionB, 3m);

        using (HttpResponseMessage rechazo = await escena.AnularPorLaApiAsync(enviada.Id))
        {
            rechazo.StatusCode.ShouldBe(
                HttpStatusCode.UnprocessableContent,
                $"las unidades ya no están: es una regla, no un fallo. {await Escenario.Detalle(rechazo)}");

            (await TipoDelProblemaAsync(rechazo)).ShouldBe("/errors/stock-insuficiente");
        }

        (await escena.InversosDeAsync(postgres, enviada.Id)).ShouldBe(0);
        (await escena.ContadorAsync()).ShouldBe(1, "el número que tomó el inverso volvió a la serie con el rollback");
        (await escena.LaTransferenciaAsync(postgres, enviada.Id)).Estado.ShouldBe(EstadoDeTransferencia.Recibida);

        await ExigirLaValoracionAsync(escena, escena.AlmacenB, cantidad: 1m, valor: 2m);

        await ExigirQueCuadraAsync(escena, existencias: 2);

        // CUANDO LAS UNIDADES VUELVEN, la misma transferencia se anula, y su inverso toma el número
        // que le toca y no uno más.
        await escena.EntrarAsync(postgres, escena.AlmacenB, escena.UbicacionB, 3m, 2m);

        using HttpResponseMessage despues = await escena.AnularPorLaApiAsync(enviada.Id);

        (await EscenaDeTransferencia.LeerAsync<AnulacionDeTransferenciaDto>(despues)).Inverso.Numero.ShouldBe(2);
    }

    /// <summary>
    /// Una transferencia entre almacenes de dos empresas no existe: el almacén de otra empresa
    /// contesta como uno inventado, y la transferencia de otra empresa, también.
    /// </summary>
    /// <remarks>
    /// <b>«Como uno inventado» se afirma comparando</b>, no suponiendo: el mismo código, el mismo tipo
    /// de error y el mismo mensaje salvo el identificador (ADR-0053 §8). Un mensaje que dijera «es de
    /// otra empresa» confirmaría que el almacén existe.
    /// </remarks>
    [Fact]
    public async Task Una_transferencia_entre_dos_empresas_no_existe()
    {
        EscenaDeTransferencia una = await MontarAsync(708, "TRF-I");
        EscenaDeTransferencia otra = await MontarAsync(709, "TRF-J");

        var inventado = Guid.CreateVersion7();

        await using ElModuloDeInventario modulo = new(postgres, otra.EmpresaId);

        foreach ((Guid origen, Guid destino, Guid ubicacionOrigen, Guid ubicacionDestino, Guid ajeno) in
            new[]
            {
                (una.AlmacenA, otra.AlmacenB, una.UbicacionA, otra.UbicacionB, una.AlmacenA),
                (otra.AlmacenA, una.AlmacenB, otra.UbicacionA, una.UbicacionB, una.AlmacenB),
            })
        {
            Resultado<TransferenciaDto> deOtraEmpresa =
                await AbrirEntreAsync(modulo, otra, origen, destino, ubicacionOrigen, ubicacionDestino);

            Resultado<TransferenciaDto> inventada = await AbrirEntreAsync(
                modulo,
                otra,
                origen == ajeno ? inventado : origen,
                destino == ajeno ? inventado : destino,
                ubicacionOrigen,
                ubicacionDestino);

            deOtraEmpresa.EsCorrecto.ShouldBeFalse();
            inventada.EsCorrecto.ShouldBeFalse();

            deOtraEmpresa.Error!.Codigo.ShouldBe("transferencia-almacen-no-encontrado");
            deOtraEmpresa.Error.Codigo.ShouldBe(inventada.Error!.Codigo);
            deOtraEmpresa.Error.Tipo.ShouldBe(inventada.Error.Tipo);
            deOtraEmpresa.Error.Mensaje.Replace(ajeno.ToString(), "{id}", StringComparison.Ordinal).ShouldBe(
                inventada.Error.Mensaje.Replace(inventado.ToString(), "{id}", StringComparison.Ordinal));
        }

        await una.EntrarAsync(postgres, una.AlmacenA, una.UbicacionA, 3m, 1m);

        Guid deLaUna = await una.AbrirAsync(postgres, 1m);

        using (HttpResponseMessage ajena = await otra.EnviarPorLaApiAsync(deLaUna))
        using (HttpResponseMessage noExiste = await otra.EnviarPorLaApiAsync(Guid.CreateVersion7()))
        {
            ajena.StatusCode.ShouldBe(HttpStatusCode.NotFound, await Escenario.Detalle(ajena));
            noExiste.StatusCode.ShouldBe(HttpStatusCode.NotFound, await Escenario.Detalle(noExiste));

            (await TipoDelProblemaAsync(ajena)).ShouldBe("/errors/transferencia-no-encontrada");
            (await TipoDelProblemaAsync(noExiste)).ShouldBe("/errors/transferencia-no-encontrada");
        }

        (await una.LaTransferenciaAsync(postgres, deLaUna)).Estado.ShouldBe(
            EstadoDeTransferencia.Borrador, "la otra empresa no la ha tocado");

        (await una.ContadorAsync()).ShouldBe(0);
    }

    /// <summary>
    /// Sin la <c>Idempotency-Key</c>, enviar, recibir y anular son un <c>428</c> y no tocan nada; con
    /// ella, la misma transferencia sigue su camino.
    /// </summary>
    /// <remarks>
    /// <b>Las tres la exigen</b> (ADR-0053 §9): el envío y la anulación numeran, y la recepción
    /// bloquea la valoración del destino, y sin la transacción del filtro el cerrojo no duraría más
    /// que su sentencia.
    /// </remarks>
    [Fact]
    public async Task Sin_la_clave_las_tres_acciones_son_428_y_no_tocan_nada()
    {
        EscenaDeTransferencia escena = await MontarAsync(710, "TRF-K");

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 3m, 1m);

        Guid transferenciaId = await escena.AbrirAsync(postgres, 2m);

        await ExigirElSinClaveAsync(escena, transferenciaId, "envio", null);

        (await escena.LaTransferenciaAsync(postgres, transferenciaId)).Estado.ShouldBe(EstadoDeTransferencia.Borrador);
        (await escena.ContadorAsync()).ShouldBe(0);

        using (HttpResponseMessage envio = await escena.EnviarPorLaApiAsync(transferenciaId))
        {
            (await EscenaDeTransferencia.LeerAsync<TransferenciaDto>(envio)).Numero.ShouldBe(
                1, "el 428 no quemó nada: con la clave, el mismo borrador se lleva el primero");
        }

        await ExigirElSinClaveAsync(
            escena,
            transferenciaId,
            "recepcion",
            Cuerpo(new RecibirTransferenciaDto { FechaDeRecepcion = Hoy }));

        (await escena.LaTransferenciaAsync(postgres, transferenciaId)).Estado.ShouldBe(EstadoDeTransferencia.Enviada);

        await ExigirElSinClaveAsync(
            escena, transferenciaId, "anulacion", Cuerpo(new AnularTransferenciaDto("Sin clave")));

        (await escena.InversosDeAsync(postgres, transferenciaId)).ShouldBe(0);
        (await escena.ContadorAsync()).ShouldBe(1);
        (await escena.FilasDelLibroAsync(postgres, transferenciaId)).Count.ShouldBe(1);
    }

    /// <summary>
    /// Una recepción con fecha futura o anterior al envío es un <c>422</c> con su código, y no mueve
    /// nada; un envío con fecha futura, también.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Las dos guardas van antes que el ejercicio, porque no toman cerrojos (ADR-0053 §3), y la del
    /// envío, además, antes del numerador. Por eso la fecha futura no necesita un ejercicio que la
    /// contenga: el «mañana» del 31 de diciembre cae en un año sin ejercicio, y el código tiene que
    /// ser el de la fecha y no el del ejercicio.
    /// </para>
    /// <para>
    /// <b>Todo en junio del año pasado</b>, y no unos días antes de hoy, para que las fechas caigan
    /// en el ejercicio de la escena también en los primeros días de enero.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Las_fechas_imposibles_son_422_y_no_mueven_nada()
    {
        int anio = Hoy.Year - 1;
        EscenaDeTransferencia escena = await MontarAsync(711, "TRF-L", anio: anio);

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 3m, 1m, new DateOnly(anio, 6, 1));

        Guid manana = await escena.AbrirAsync(postgres, 1m, Hoy.AddDays(1));

        using (HttpResponseMessage futuro = await escena.EnviarPorLaApiAsync(manana))
        {
            futuro.StatusCode.ShouldBe(HttpStatusCode.UnprocessableContent, await Escenario.Detalle(futuro));
            (await TipoDelProblemaAsync(futuro)).ShouldBe("/errors/transferencia-con-fecha-futura");
        }

        (await escena.ContadorAsync()).ShouldBe(0);

        TransferenciaDto enviada = await escena.EnviarAsync(postgres, 2m, new DateOnly(anio, 6, 10));

        foreach ((DateOnly fecha, string tipo) in new[]
        {
            (Hoy.AddDays(1), "/errors/transferencia-con-fecha-futura"),
            (new DateOnly(anio, 6, 9), "/errors/transferencia-recepcion-antes-del-envio"),
        })
        {
            using HttpResponseMessage rechazo = await escena.RecibirPorLaApiAsync(enviada.Id, fecha);

            rechazo.StatusCode.ShouldBe(HttpStatusCode.UnprocessableContent, await Escenario.Detalle(rechazo));
            (await TipoDelProblemaAsync(rechazo)).ShouldBe(tipo);
        }

        (await escena.LaTransferenciaAsync(postgres, enviada.Id)).Estado.ShouldBe(EstadoDeTransferencia.Enviada);
        (await escena.FilasDelLibroAsync(postgres, enviada.Id)).Count.ShouldBe(1);
    }

    /// <summary>
    /// Tres líneas hacia el mismo almacén, dos de ellas al mismo hueco: cada una mueve dos veces, y el
    /// tránsito del destino es la suma de las tres.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Dos líneas a la misma existencia y tres a la misma valoración</b>, que es donde las dos
    /// sentencias de <c>ElTransito</c> agrupan: sin el <c>GROUP BY</c>, PostgreSQL toca una sola vez
    /// cada fila por sentencia, se queda con una de las líneas y el recuento de filas tocadas no lo
    /// ve, porque cuenta claves y no líneas. El tránsito saldría corto sin un solo error.
    /// </para>
    /// <para>
    /// <b>El coste del origen es redondo a propósito</b>: lo que se mira aquí es que no se pierda
    /// ninguna línea, y las tres cantidades distintas (3, 2 y 1) hacen que cualquier línea perdida
    /// deje una suma que no es la de las tres.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Varias_lineas_al_mismo_destino_mueven_dos_veces_cada_una_y_su_transito_se_suma()
    {
        EscenaDeTransferencia escena = await MontarAsync(719, "TRF-N");

        Guid otroHuecoDeB = (await LosMaestrosPorLaApi.CrearUbicacionAsync(
            escena.Cliente, escena.AlmacenB, "TRF-N-B2")).Id;

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 10m, 2m);

        Guid transferenciaId;

        await using (ElModuloDeInventario modulo = new(postgres, escena.EmpresaId))
        {
            Resultado<TransferenciaDto> alta = await escena.IntentarAbrirAsync(
                modulo,
                escena.AlmacenA,
                escena.AlmacenB,
                [
                    escena.Linea(escena.UbicacionA, escena.UbicacionB, 3m),
                    escena.Linea(escena.UbicacionA, escena.UbicacionB, 2m),
                    escena.Linea(escena.UbicacionA, otroHuecoDeB, 1m),
                ]);

            alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");
            transferenciaId = alta.Valor.Id;
        }

        using (HttpResponseMessage envio = await escena.EnviarPorLaApiAsync(transferenciaId))
        {
            (await EscenaDeTransferencia.LeerAsync<TransferenciaDto>(envio)).Lineas.ShouldBe(3);
        }

        (await escena.FilasDelLibroAsync(postgres, transferenciaId)).Select(fila => fila.CantidadEnUnidadBase)
            .ShouldBe([-3m, -2m, -1m], "una salida del origen por línea");

        await ExigirLaValoracionAsync(escena, escena.AlmacenA, cantidad: 4m, valor: 8m);
        await ExigirLaValoracionAsync(
            escena, escena.AlmacenB, cantidad: 0m, valor: 0m, enTransito: 6m, valorEnTransito: 12m);

        (await LaExistenciaEnAsync(escena, escena.UbicacionB)).EnTransito.ShouldBe(
            5m, "las dos líneas al mismo hueco, sumadas en su fila");
        (await LaExistenciaEnAsync(escena, otroHuecoDeB)).EnTransito.ShouldBe(1m);

        // DOS CLAVES EN VUELO Y UNA VALORACIÓN: el cuadre agrupa las líneas como la sentencia.
        await ExigirQueCuadraAsync(escena, existencias: 3, enTransito: 2, valoracionesEnTransito: 1);

        using (HttpResponseMessage recepcion = await escena.RecibirPorLaApiAsync(transferenciaId, Hoy))
        {
            await EscenaDeTransferencia.LeerAsync<TransferenciaDto>(recepcion);
        }

        IReadOnlyList<MovimientoStock> filas = await escena.FilasDelLibroAsync(postgres, transferenciaId);

        filas.Select(fila => fila.CantidadEnUnidadBase).ShouldBe(
            [-3m, -2m, -1m, 1m, 2m, 3m], "dos movimientos por línea, y son tres líneas");
        filas.Where(fila => fila.CantidadEnUnidadBase < 0m).ShouldAllBe(fila => fila.AlmacenId == escena.AlmacenA);
        filas.Where(fila => fila.CantidadEnUnidadBase > 0m).ShouldAllBe(fila => fila.AlmacenId == escena.AlmacenB);
        filas.ShouldAllBe(fila => fila.Valor.Cantidad == 2m * fila.CantidadEnUnidadBase);

        await ExigirLaValoracionAsync(escena, escena.AlmacenB, cantidad: 6m, valor: 12m);

        Existencia enElHueco = await LaExistenciaEnAsync(escena, escena.UbicacionB);
        Existencia enElOtro = await LaExistenciaEnAsync(escena, otroHuecoDeB);

        (enElHueco.Fisico, enElHueco.EnTransito).ShouldBe((5m, 0m));
        (enElOtro.Fisico, enElOtro.EnTransito).ShouldBe((1m, 0m));

        (await escena.ElValorDeLaEmpresaAsync(postgres)).ShouldBe(20m);

        await ExigirQueCuadraAsync(escena, existencias: 3);
    }

    /// <summary>
    /// Un número de serie sale, vuelve sin llegar, sale otra vez, llega y vuelve desde el destino, y en
    /// ningún momento está en dos sitios.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es el orden de las patas del ADR-0053 §7, ejercido entero.</b> La serie cuenta en el destino
    /// también mientras vuela (<c>ix_existencias_numero_de_serie_en_un_sitio</c> y el <c>CHECK</c> de
    /// <c>fisico + en_transito</c>), así que cada movimiento tiene que quitarla de un sitio antes de
    /// ponerla en otro. Al anular una enviada, baja el tránsito antes de sumar en el origen; al
    /// recibir, baja el tránsito antes de la entrada; al anular una recibida, sale del destino antes
    /// de volver al origen. Con cualquiera de los tres al revés, la base contesta con un <c>23505</c>
    /// o un <c>23514</c>, y el caso con un <c>500</c>.
    /// </para>
    /// <para>
    /// Los artículos sin serie no notan el orden, y por eso este caso es de serie.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Una_serie_va_vuelve_llega_y_vuelve_sin_estar_nunca_en_dos_sitios()
    {
        const string Serie = "SN-VIAJE-1";

        EscenaDeTransferencia escena = await MontarAsync(720, "TRF-O", trazabilidad: "PorNumeroSerie");

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 1m, 50m, serie: Serie);

        TransferenciaDto primera = await escena.EnviarAsync(postgres, 1m, serie: Serie);

        await ExigirDondeEstaLaSerieAsync(escena, escena.AlmacenB, fisico: 0m, enTransito: 1m);

        // LA SERIE EN VUELO, CUADRADA: la línea lleva el número, y el cuadre lo traduce a la fila.
        await ExigirQueCuadraAsync(escena, existencias: 2, enTransito: 1, valoracionesEnTransito: 1);

        using (HttpResponseMessage vuelta = await escena.AnularPorLaApiAsync(primera.Id))
        {
            await EscenaDeTransferencia.LeerAsync<AnulacionDeTransferenciaDto>(vuelta);
        }

        await ExigirDondeEstaLaSerieAsync(escena, escena.AlmacenA, fisico: 1m, enTransito: 0m);

        TransferenciaDto segunda = await escena.EnviarAsync(postgres, 1m, serie: Serie);

        using (HttpResponseMessage llegada = await escena.RecibirPorLaApiAsync(segunda.Id, Hoy))
        {
            await EscenaDeTransferencia.LeerAsync<TransferenciaDto>(llegada);
        }

        await ExigirDondeEstaLaSerieAsync(escena, escena.AlmacenB, fisico: 1m, enTransito: 0m);

        using (HttpResponseMessage vuelta = await escena.AnularPorLaApiAsync(segunda.Id))
        {
            await EscenaDeTransferencia.LeerAsync<AnulacionDeTransferenciaDto>(vuelta);
        }

        await ExigirDondeEstaLaSerieAsync(escena, escena.AlmacenA, fisico: 1m, enTransito: 0m);

        (await escena.ElValorDeLaEmpresaAsync(postgres)).ShouldBe(50m);

        await ExigirQueCuadraAsync(escena, existencias: 2);
    }

    /// <summary>
    /// Dos empresas con las mismas claves inventadas envían a la vez: el tránsito de cada una se suma
    /// en su fila, y ninguna toca la de la otra.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es el filtro de la empresa en las dos sentencias de <c>ElTransito</c></b>, que son SQL crudo
    /// y no llevan el filtro global de EF Core (R8). Con los identificadores de fuera distintos, como
    /// los da la API, quitar el filtro no se vería: ninguna otra fila casaría con la clave. Por eso
    /// las dos empresas comparten almacén, hueco y artículo, y por eso van sin la API, que no deja
    /// inventarlos.
    /// </para>
    /// <para>
    /// <b>Sin el filtro, el segundo envío no saldría mal en silencio</b>: tocaría dos filas para una
    /// clave, y la sentencia, que cuenta lo que toca, lanzaría. El caso afirma las dos mitades: que
    /// el segundo envío pasa y que el tránsito del primero sigue siendo el suyo.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Dos_empresas_con_las_mismas_claves_no_se_mezclan_el_transito()
    {
        var claves = ClavesDeUnaTransferencia.Inventadas();
        Guid[] empresas = [Guid.CreateVersion7(), Guid.CreateVersion7()];
        decimal[] cantidades = [4m, 3m];

        foreach (Guid empresaId in empresas)
        {
            await ElLibro.EntrarEnElOrigenAsync(postgres, empresaId, claves, 10m, 2m, Hoy);
        }

        for (int indice = 0; indice < empresas.Length; indice++)
        {
            await ElLibro.EnviarSinLaApiAsync(postgres, empresas[indice], claves, cantidades[indice], Hoy);
        }

        for (int indice = 0; indice < empresas.Length; indice++)
        {
            await using InventarioDbContext contexto = postgres.AbrirInventario(empresas[indice]);

            Valoracion destino = await contexto.Valoraciones.AsNoTracking()
                .SingleAsync(fila => fila.AlmacenId == claves.AlmacenDestinoId);

            destino.EnTransito.ShouldBe(cantidades[indice], $"la empresa {indice + 1} ve solo lo suyo en vuelo");
            destino.ValorEnTransito.Cantidad.ShouldBe(2m * cantidades[indice]);

            Existencia enElDestino = await contexto.Existencias.AsNoTracking()
                .SingleAsync(fila => fila.AlmacenId == claves.AlmacenDestinoId);

            enElDestino.EnTransito.ShouldBe(cantidades[indice]);

            // Y EL CUADRE DE CADA UNA, con las mismas claves en la otra: si leyera las líneas de
            // las dos, la clave esperaría lo que vuela en las dos y no lo de la suya.
            CuadreDeLasExistencias cuadre = await LasExistencias.CuadrarAsync(postgres, empresas[indice]);

            (cuadre.ExistenciasEnTransitoComparadas, cuadre.ValoracionesEnTransitoComparadas).ShouldBe((1L, 1L));
            cuadre.Descuadres.ShouldBeEmpty($"la empresa {indice + 1} cuadra su tránsito con sus líneas");
        }
    }

    private static async Task CerrarPorLaApiAsync(EscenaDeTransferencia escena)
    {
        using HttpResponseMessage cierre = await escena.CerrarElEjercicioAsync();

        cierre.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(cierre));
    }

    private static Task<string?> TipoDelProblemaAsync(HttpResponseMessage respuesta) =>
        EscenaDeTransferencia.TipoDelProblemaAsync(respuesta);

    private static JsonContent Cuerpo<T>(T cuerpo) => JsonContent.Create(cuerpo);

    private static void ExigirLaFila(
        MovimientoStock fila, Guid almacenId, decimal cantidad, decimal valor, DateOnly fecha)
    {
        fila.DocumentoOrigenTipo.ShouldBe(TipoDeDocumentoOrigen.Transferencia);
        fila.AlmacenId.ShouldBe(almacenId);
        fila.CantidadEnUnidadBase.ShouldBe(cantidad);
        fila.Valor.Cantidad.ShouldBe(valor);
        fila.FechaDeOperacion.ShouldBe(fecha);
    }

    private static Task<Resultado<TransferenciaDto>> AbrirEntreAsync(
        ElModuloDeInventario modulo,
        EscenaDeTransferencia escena,
        Guid origen,
        Guid destino,
        Guid ubicacionOrigen,
        Guid ubicacionDestino) =>
        modulo.AltaDeTransferencia.EjecutarAsync(
            new AbrirTransferenciaDto(
                escena.SerieDeTransferencias.Id,
                origen,
                destino,
                Hoy,
                [new LineaDeTransferenciaDto(
                    ubicacionOrigen, ubicacionDestino, escena.ArticuloId, 1m, escena.UnidadId, 1m)]),
            CancellationToken.None);

    private static async Task ExigirElSinClaveAsync(
        EscenaDeTransferencia escena, Guid transferenciaId, string accion, HttpContent? cuerpo)
    {
        using HttpResponseMessage sinClave = await escena.Cliente.PostAsync(
            $"{EscenaDeTransferencia.Transferencias}/{transferenciaId}/{accion}", cuerpo);

        sinClave.StatusCode.ShouldBe(HttpStatusCode.PreconditionRequired, await Escenario.Detalle(sinClave));

        (await TipoDelProblemaAsync(sinClave)).ShouldBe("/errors/" + ErroresDeIdempotencia.CodigoDeObligatoria);
    }

    private async Task<EscenaDeTransferencia> MontarAsync(
        int semilla, string codigo, int? anio = null, string trazabilidad = "Ninguna")
    {
        EscenaDeTransferencia escena =
            await EscenaDeTransferencia.MontarAsync(_api, semilla, codigo, semilla, trazabilidad, anio);

        _clientes.Add(escena.Cliente);

        return escena;
    }

    /// <summary>A: 4 a 2,50 y 3 a 3,00, que son 7 por 19,00. B: 5 a 4,00, que son 20,00.</summary>
    private async Task LaEscenaDeSiempreAsync(EscenaDeTransferencia escena)
    {
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 4m, 2.50m);
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 3m, 3.00m);
        await escena.EntrarAsync(postgres, escena.AlmacenB, escena.UbicacionB, 5m, 4.00m);
    }

    private async Task ExigirLaValoracionAsync(
        EscenaDeTransferencia escena,
        Guid almacenId,
        decimal cantidad,
        decimal valor,
        decimal enTransito = 0m,
        decimal valorEnTransito = 0m)
    {
        Valoracion valoracion = await escena.LaValoracionDeAsync(postgres, almacenId);

        valoracion.Cantidad.ShouldBe(cantidad);
        valoracion.Valor.Cantidad.ShouldBe(valor);
        valoracion.EnTransito.ShouldBe(enTransito);
        valoracion.ValorEnTransito.Cantidad.ShouldBe(valorEnTransito);
    }

    private async Task<Existencia> LaExistenciaDeAsync(EscenaDeTransferencia escena, Guid almacenId) =>
        (await LasExistencias.VivasAsync(postgres, escena.EmpresaId))
            .Where(fila => fila.AlmacenId == almacenId)
            .ShouldHaveSingleItem();

    private async Task<Existencia> LaExistenciaEnAsync(EscenaDeTransferencia escena, Guid ubicacionId) =>
        (await LasExistencias.VivasAsync(postgres, escena.EmpresaId))
            .Where(fila => fila.UbicacionId == ubicacionId)
            .ShouldHaveSingleItem();

    // La única fila de la serie que la tiene, en la estantería o en vuelo: si hubiera dos, la serie
    // estaría en dos sitios a la vez.
    private async Task ExigirDondeEstaLaSerieAsync(
        EscenaDeTransferencia escena, Guid almacenId, decimal fisico, decimal enTransito)
    {
        Existencia donde = (await LasExistencias.VivasAsync(postgres, escena.EmpresaId))
            .Where(fila => fila.Fisico + fila.EnTransito != 0m)
            .ShouldHaveSingleItem("una serie está en un solo sitio, también mientras vuela");

        donde.NumeroDeSerieId.ShouldNotBeNull();
        donde.AlmacenId.ShouldBe(almacenId);
        (donde.Fisico, donde.EnTransito).ShouldBe((fisico, enTransito));
    }

    /// <summary>
    /// Cuadra la empresa y exige cuántas claves comparó: las del libro, y las que tienen algo en
    /// vuelo, en la existencia y en la valoración (ADR-0053 §11).
    /// </summary>
    /// <remarks>
    /// Lo que vuela es cero en casi todos los casos, porque casi todos cuadran con la transferencia
    /// ya recibida o anulada. Los que cuadran con algo en vuelo lo dicen.
    /// </remarks>
    private async Task ExigirQueCuadraAsync(
        EscenaDeTransferencia escena, long existencias, long enTransito = 0, long valoracionesEnTransito = 0)
    {
        CuadreDeLasExistencias cuadre = await LasExistencias.CuadrarAsync(postgres, escena.EmpresaId);

        cuadre.ExistenciasComparadas.ShouldBe(existencias, "sin claves que comparar, el cuadre sale limpio por no mirar");
        cuadre.ExistenciasEnTransitoComparadas.ShouldBe(enTransito);
        cuadre.ValoracionesEnTransitoComparadas.ShouldBe(valoracionesEnTransito);
        cuadre.Descuadres.ShouldBeEmpty();
    }
}
