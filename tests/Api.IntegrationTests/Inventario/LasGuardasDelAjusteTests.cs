using System.Net;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Las guardas de los tres casos de uso del ajuste que ningún caso veía (epílogo del 2.11): los
/// estados de confirmar y de anular, el documento que no existe, el motivo, la serie y el ejercicio
/// de la anulación, y los maestros del alta.
/// </summary>
/// <remarks>
/// <para>
/// <b>Salen de la tanda de mutaciones del epílogo</b>, de la 264 a la 301, que quitó una a una las
/// guardas de <c>AbrirAjuste</c>, <c>ConfirmarAjuste</c> y <c>AnularAjuste</c>. Las que salieron
/// verdes son las que el dominio repite: sin la del caso de uso el documento no se estropeaba, pero
/// el borde contestaba un <c>500</c> donde tenía que ir un <c>409</c>, un <c>404</c> o un <c>400</c>,
/// o el código de otra guarda. Por eso estos casos miran el código y el tipo, no solo que la acción
/// falle. Son los mismos tres que la transferencia tiene en <c>LasGuardasDeLaTransferenciaTests</c>.
/// </para>
/// <para>
/// <b>Cada rechazo lleva su pareja</b>, el mismo documento que, ya en regla, pasa: una guarda que
/// contestara siempre que no daría el mismo rojo que la buena.
/// </para>
/// <para>
/// <b>Semillas: del 736 al 738</b>, empresas y maestros de instalación con el mismo número. El
/// reparto del bloque está en la cabecera de <c>LaTransferenciaTests</c>.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LasGuardasDelAjusteTests(PostgresConTodosLosModulos postgres) : IDisposable
{
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
    /// Confirmar lo que no es un borrador y anular lo que no está confirmado es <c>409</c>, y las
    /// dos cosas sobre un ajuste que no existe, <c>404</c>; en su estado, cada una pasa.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Un solo documento recorre los tres estados</b>, y en cada uno se intenta lo que no le toca:
    /// así cada rechazo tiene su pareja, la misma acción que pasa en cuanto el documento llega a su
    /// estado.
    /// </para>
    /// <para>
    /// <b>Las mutaciones 286 y 296</b> dejaban pasar el estado que no tocaba, y el dominio lanzaba en
    /// su lugar: nada se escribía, pero la respuesta era un <c>500</c>. <b>Las 285 y 295</b> decían
    /// del documento que no existe que estaba en otro estado, un <c>409</c> en vez del <c>404</c>.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Cada_accion_fuera_de_su_estado_es_409_y_en_el_suyo_pasa()
    {
        EscenaDeTransferencia escena = await MontarAsync(736, "AJG-A");

        Guid ajusteId;

        await using (ElModuloDeInventario modulo = new(postgres, escena.EmpresaId))
        {
            ajusteId = await escena.AbrirUnAjusteAsync(modulo, escena.AlmacenA, Entrada(escena, 3m));
        }

        await ExigirElProblemaAsync(
            escena.AnularElAjustePorLaApiAsync(ajusteId), HttpStatusCode.Conflict, "ajuste-no-esta-confirmado");

        using (HttpResponseMessage confirmacion = await escena.ConfirmarElAjustePorLaApiAsync(ajusteId))
        {
            (await EscenaDeTransferencia.LeerAsync<AjusteDto>(confirmacion)).Estado
                .ShouldBe(nameof(EstadoDeAjuste.Confirmado));
        }

        await ExigirElProblemaAsync(
            escena.ConfirmarElAjustePorLaApiAsync(ajusteId), HttpStatusCode.Conflict, "ajuste-no-esta-en-borrador");

        AnulacionDto par;

        using (HttpResponseMessage anulacion = await escena.AnularElAjustePorLaApiAsync(ajusteId))
        {
            par = await EscenaDeTransferencia.LeerAsync<AnulacionDto>(anulacion);
        }

        par.Original.Estado.ShouldBe(nameof(EstadoDeAjuste.Anulado));

        await ExigirElProblemaAsync(
            escena.AnularElAjustePorLaApiAsync(ajusteId), HttpStatusCode.Conflict, "ajuste-no-esta-confirmado");
        await ExigirElProblemaAsync(
            escena.ConfirmarElAjustePorLaApiAsync(ajusteId), HttpStatusCode.Conflict, "ajuste-no-esta-en-borrador");
        await ExigirElProblemaAsync(
            escena.ConfirmarElAjustePorLaApiAsync(par.Inverso.Id), HttpStatusCode.Conflict, "ajuste-no-esta-en-borrador");

        var inventado = Guid.CreateVersion7();

        await ExigirElProblemaAsync(
            escena.ConfirmarElAjustePorLaApiAsync(inventado), HttpStatusCode.NotFound, "ajuste-no-encontrado");
        await ExigirElProblemaAsync(
            escena.AnularElAjustePorLaApiAsync(inventado), HttpStatusCode.NotFound, "ajuste-no-encontrado");

        (await escena.ContadorAsync(escena.SerieDeAjustes.Id))
            .ShouldBe(2, "el original y su inverso: ningún rechazo gastó número");
        (await InversosDeAsync(escena, ajusteId)).ShouldBe(1);
        (await escena.FilasDelLibroAsync(postgres, ajusteId)).Count.ShouldBe(1);
        (await escena.FilasDelLibroAsync(postgres, par.Inverso.Id)).Count.ShouldBe(1);
    }

    /// <summary>
    /// Una anulación sin motivo o con uno más largo que su columna es <c>400</c>; con la serie del
    /// original cerrada, o con el ejercicio de hoy cerrado, es <c>409</c> con su código. Con
    /// trescientos caracteres justos, la serie abierta y el ejercicio abierto, pasa.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El motivo se pregunta al caso de uso</b>, que es donde vive la guarda: así el caso no
    /// depende de lo que el borde valide antes de llamarlo. Las mutaciones 293 y 294 la partían en sus
    /// dos mitades, el vacío y el largo, y en las dos el dominio lanzaba al construir el inverso.
    /// </para>
    /// <para>
    /// <b>La serie cerrada es otra que la de la escena</b>, para que el ejercicio se cierre después
    /// con la serie de la escena abierta: así lo único que para la última anulación es el ejercicio.
    /// Sin la guarda del número, la 299, el caso de uso leía el valor de un resultado fallido y
    /// contestaba un <c>500</c>. Sin la del ejercicio cerrado, la 297, el inverso se escribía.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task La_anulacion_pide_motivo_una_serie_que_numere_y_el_ejercicio_de_hoy_abierto()
    {
        EscenaDeTransferencia escena = await MontarAsync(737, "AJG-B");

        Guid primero = await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 5m, 2m);
        Guid segundo = await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 5m, 2m);

        SerieDto otra = await LosMaestrosPorLaApi.CrearSerieEnAsync(escena.Cliente, escena.Ejercicio.Id, "AJG-B-AJ2");
        Guid deLaOtra;

        await using (ElModuloDeInventario modulo = new(postgres, escena.EmpresaId))
        {
            deLaOtra = await escena.AbrirUnAjusteAsync(modulo, escena.AlmacenA, Entrada(escena, 5m), otra.Id);
        }

        using (HttpResponseMessage confirmado = await escena.ConfirmarElAjustePorLaApiAsync(deLaOtra))
        {
            confirmado.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(confirmado));
        }

        await using (ElModuloDeInventario modulo = new(postgres, escena.EmpresaId))
        {
            foreach (string motivo in new[] { string.Empty, "   ", new string('x', Ajuste.LargoDelMotivo + 1) })
            {
                Resultado<AnulacionDto> rechazo = await modulo.AnularAsync(primero, motivo);

                rechazo.EsCorrecto.ShouldBeFalse($"con un motivo de {motivo.Length} caracteres");
                rechazo.Error!.Codigo.ShouldBe("ajuste-motivo-no-valido");
                rechazo.Error!.Tipo.ShouldBe(TipoDeError.Validacion);
            }

            (await InversosDeAsync(escena, primero)).ShouldBe(0);

            Resultado<AnulacionDto> justa = await modulo.AnularAsync(primero, new string('x', Ajuste.LargoDelMotivo));

            justa.EsCorrecto.ShouldBeTrue($"«{justa.Error?.Codigo}»");
        }

        await escena.CerrarLaSerieAsync(postgres, otra.Id);

        await ExigirElProblemaAsync(
            escena.AnularElAjustePorLaApiAsync(deLaOtra), HttpStatusCode.Conflict, "serie-no-numera");

        (await ElEstadoDeAsync(escena, deLaOtra)).ShouldBe(EstadoDeAjuste.Confirmado);
        (await InversosDeAsync(escena, deLaOtra)).ShouldBe(0);
        (await escena.ContadorAsync(otra.Id)).ShouldBe(1, "el inverso que no nació no se quedó con su número");

        using (HttpResponseMessage cerrado = await escena.CerrarElEjercicioAsync())
        {
            cerrado.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(cerrado));
        }

        await ExigirElProblemaAsync(
            escena.AnularElAjustePorLaApiAsync(segundo), HttpStatusCode.Conflict, "ajuste-en-ejercicio-cerrado");

        (await ElEstadoDeAsync(escena, segundo)).ShouldBe(EstadoDeAjuste.Confirmado);
        (await InversosDeAsync(escena, segundo)).ShouldBe(0);
        (await escena.ContadorAsync(escena.SerieDeAjustes.Id))
            .ShouldBe(3, "las dos entradas y el inverso de la primera");
    }

    /// <summary>
    /// El alta rechaza, cada cosa con su código y su tipo, lo que no se puede ajustar: nada, una
    /// empresa que no opera, un hueco de otro almacén o inventado, un artículo o una unidad
    /// inventados, un hueco bloqueado, una línea que no casa con la marca del artículo, una unidad
    /// retirada y un artículo que no se almacena.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La primera alta pasa</b>, con la misma línea que llevan los rechazos, y al final solo hay
    /// dos ajustes, ella y la del lote: cada rechazo para por lo que dice y no deja un borrador a
    /// medias. Salen de las mutaciones 264, 265 y de la 275 a la 284. Las que cambian el tipo, de
    /// <c>Validacion</c> a <c>Conflicto</c>, solo las ve la aserción del tipo.
    /// </para>
    /// <para>
    /// <b>La marca, entre el hueco bloqueado y la unidad</b>, y con su propia pareja: el artículo
    /// pasa a ir por lote, la línea sin lote no abre y la misma línea con lote sí. Esa comprobación
    /// del alta es de cortesía, porque la confirmación vuelve a leer la marca con cerrojo; sin ella
    /// el borrador se abría y el rechazo llegaba después, al confirmar. La tabla de
    /// <c>CadaMarcaAdmiteSuLineaTests</c> no lo ve, porque prueba la regla y no que el alta la
    /// pregunte (la 284). La marca vuelve a <c>Ninguna</c> antes de la unidad, para que lo que sigue
    /// no dependa del orden de esas dos preguntas.
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
    public async Task El_alta_rechaza_cada_maestro_que_no_se_mueve_con_su_codigo()
    {
        EscenaDeTransferencia escena = await MontarAsync(738, "AJG-C");

        UbicacionDto otroHueco = await LosMaestrosPorLaApi.CrearUbicacionAsync(
            escena.Cliente, escena.AlmacenA, "AJG-C-A2");

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        LineaDeAjusteDto linea = Entrada(escena, 1m);

        Resultado<AjusteDto> buena = await IntentarAbrirAsync(escena, modulo, [linea]);

        buena.EsCorrecto.ShouldBeTrue($"«{buena.Error?.Codigo}»");

        ExigirElError(await IntentarAbrirAsync(escena, modulo, []), "ajuste-sin-lineas", TipoDeError.Validacion);

        await using (ElModuloDeInventario deNinguna = new(postgres, Guid.CreateVersion7()))
        {
            ExigirElError(
                await IntentarAbrirAsync(escena, deNinguna, [linea]),
                "empresa-activa-no-operativa",
                TipoDeError.Conflicto);
        }

        foreach (Guid hueco in new[] { escena.UbicacionB, Guid.CreateVersion7() })
        {
            ExigirElError(
                await IntentarAbrirAsync(escena, modulo, [linea with { UbicacionId = hueco }]),
                "ajuste-ubicacion-no-encontrada",
                TipoDeError.Validacion);
        }

        ExigirElError(
            await IntentarAbrirAsync(escena, modulo, [linea with { ArticuloId = Guid.CreateVersion7() }]),
            "ajuste-articulo-no-encontrado",
            TipoDeError.Validacion);

        ExigirElError(
            await IntentarAbrirAsync(escena, modulo, [linea with { UnidadIntroducidaId = Guid.CreateVersion7() }]),
            "ajuste-unidad-no-encontrada",
            TipoDeError.Validacion);

        using (HttpResponseMessage bloqueo = await escena.Cliente.SuprimirAsync(
            $"{LosMaestrosPorLaApi.Ubicaciones}/{otroHueco.Id}"))
        {
            bloqueo.IsSuccessStatusCode.ShouldBeTrue(await Escenario.Detalle(bloqueo));
        }

        ExigirElError(
            await IntentarAbrirAsync(escena, modulo, [linea with { UbicacionId = otroHueco.Id }]),
            "ajuste-ubicacion-bloqueada",
            TipoDeError.Conflicto);

        using (HttpResponseMessage porLote = await escena.CambiarElArticuloAsync("Bien", "PorLote"))
        {
            porLote.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(porLote));
        }

        ExigirElError(
            await IntentarAbrirAsync(escena, modulo, [linea]), "ajuste-trazabilidad-no-casa", TipoDeError.Conflicto);

        Resultado<AjusteDto> conLote = await IntentarAbrirAsync(escena, modulo, [linea with { CodigoDeLote = "L-01" }]);

        conLote.EsCorrecto.ShouldBeTrue($"«{conLote.Error?.Codigo}»");

        using (HttpResponseMessage sinMarca = await escena.CambiarElArticuloAsync("Bien", "Ninguna"))
        {
            sinMarca.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(sinMarca));
        }

        string unidad = $"{LosMaestrosPorLaApi.Unidades}/{escena.UnidadId}";

        using (HttpResponseMessage retirada = await escena.Cliente.AccionarAsync(
            unidad, $"{unidad}/retirada", HttpMethod.Post))
        {
            retirada.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(retirada));
        }

        ExigirElError(
            await IntentarAbrirAsync(escena, modulo, [linea]), "ajuste-unidad-retirada", TipoDeError.Conflicto);

        using (HttpResponseMessage servicio = await escena.CambiarElArticuloAsync("Servicio", "Ninguna"))
        {
            servicio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(servicio));
        }

        ExigirElError(
            await IntentarAbrirAsync(escena, modulo, [linea]), "ajuste-articulo-no-se-almacena", TipoDeError.Conflicto);

        await using InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId);

        (await contexto.Ajustes.CountAsync())
            .ShouldBe(2, "las dos altas buenas, y un alta rechazada no deja un borrador a medias");
    }

    private static LineaDeAjusteDto Entrada(EscenaDeTransferencia escena, decimal cantidad) =>
        new(escena.UbicacionA, escena.ArticuloId, cantidad, escena.UnidadId, 1m, 2m);

    private static Task<Resultado<AjusteDto>> IntentarAbrirAsync(
        EscenaDeTransferencia escena, ElModuloDeInventario modulo, IReadOnlyList<LineaDeAjusteDto> lineas) =>
        modulo.Alta.EjecutarAsync(
            new AbrirAjusteDto(
                escena.SerieDeAjustes.Id,
                escena.AlmacenA,
                EscenaDeTransferencia.Hoy,
                "Regularización de un recuento",
                lineas),
            CancellationToken.None);

    private static async Task ExigirElProblemaAsync(
        Task<HttpResponseMessage> peticion, HttpStatusCode estado, string codigo)
    {
        using HttpResponseMessage respuesta = await peticion;

        respuesta.StatusCode.ShouldBe(estado, await Escenario.Detalle(respuesta));
        (await EscenaDeTransferencia.TipoDelProblemaAsync(respuesta)).ShouldBe("/errors/" + codigo);
    }

    private static void ExigirElError(Resultado<AjusteDto> alta, string codigo, TipoDeError tipo)
    {
        alta.EsCorrecto.ShouldBeFalse($"tenía que salir «{codigo}»");
        alta.Error!.Codigo.ShouldBe(codigo);
        alta.Error!.Tipo.ShouldBe(tipo);
    }

    private async Task<EstadoDeAjuste> ElEstadoDeAsync(EscenaDeTransferencia escena, Guid ajusteId)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId);

        return (await contexto.Ajustes.AsNoTracking().SingleAsync(fila => fila.Id == ajusteId)).Estado;
    }

    private async Task<int> InversosDeAsync(EscenaDeTransferencia escena, Guid ajusteId)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId);

        return await contexto.Ajustes.CountAsync(fila => fila.AnulaAId == ajusteId);
    }

    private async Task<EscenaDeTransferencia> MontarAsync(int semilla, string codigo)
    {
        EscenaDeTransferencia escena = await EscenaDeTransferencia.MontarAsync(_api, semilla, codigo, semilla);

        _clientes.Add(escena.Cliente);

        return escena;
    }
}
