using System.Net;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Lineas;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Cada rechazo del alta de la transferencia dice en qué línea está, y en qué campo o en qué punta
/// (ADR-0068 §4 y §6).
/// </summary>
/// <remarks>
/// <para>
/// <b>Como en el ajuste desde el 2.17</b>: lo que falla en el cuerpo va a <c>errors</c>, con la ruta
/// de zod, <c>lineas[i].campo</c> y el índice desde cero, para que el formulario lo pinte en la celda
/// que hay que corregir; lo que no se mueve va a <c>actual</c>, con la línea desde uno. Las
/// peticiones llevan dos líneas cuando la que falla puede ser la segunda, para que un índice corrido
/// se vea.
/// </para>
/// <para>
/// <b>Las dos ubicaciones son dos campos</b>, y el número de la línea no dice cuál de las dos celdas
/// pintar: la que no existe en el almacén de su punta va a su campo, y la bloqueada lleva la punta
/// en <c>actual</c>.
/// </para>
/// <para>
/// <b>Lo que no cabe en su columna es un <c>400</c></b> en la cantidad de su línea: lo escrito, antes
/// de los maestros, y lo que mueve en la unidad base, con el factor ya puesto. Hasta el 2.19 lo
/// segundo era un <c>500</c> al guardar, o una línea que no movía nada.
/// </para>
/// <para>
/// <b>Cada rechazo lleva su pareja</b>, el mismo documento que, en regla o en el borde, pasa.
/// </para>
/// <para>
/// <b>Semillas: del 907 al 910</b>, empresas y maestros con el mismo número. El reparto del bloque
/// del 2.19 está en la cabecera de <c>ElFactorDeLaTransferenciaTests</c>.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LasLineasDeLaTransferenciaTests(PostgresConTodosLosModulos postgres) : IDisposable
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

    [Fact]
    public async Task Cada_maestro_que_falla_nombra_su_linea_y_la_ubicacion_su_punta()
    {
        EscenaDeTransferencia escena = await MontarAsync(907, "LTR-A");

        UbicacionDto otroDeA = await LosMaestrosPorLaApi.CrearUbicacionAsync(escena.Cliente, escena.AlmacenA, "LTR-A-A2");
        UbicacionDto otroDeB = await LosMaestrosPorLaApi.CrearUbicacionAsync(escena.Cliente, escena.AlmacenB, "LTR-A-B2");

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        LineaDeTransferenciaDto linea = escena.Linea(escena.UbicacionA, escena.UbicacionB, 1m);

        Resultado<TransferenciaDto> buena = await IntentarAbrirAsync(escena, modulo, [linea]);

        buena.EsCorrecto.ShouldBeTrue($"«{buena.Error?.Codigo}»");

        // LA DE ENFRENTE TAMPOCO EXISTE EN ESTE ALMACÉN, y va al campo de su punta.
        foreach (Guid hueco in new[] { escena.UbicacionB, Guid.CreateVersion7() })
        {
            ExigirElError(
                await IntentarAbrirAsync(escena, modulo, [linea, linea with { UbicacionOrigenId = hueco }]),
                "transferencia-ubicacion-no-encontrada",
                TipoDeError.Validacion,
                campo: "lineas[1].ubicacionOrigenId");
        }

        foreach (Guid hueco in new[] { escena.UbicacionA, Guid.CreateVersion7() })
        {
            ExigirElError(
                await IntentarAbrirAsync(escena, modulo, [linea, linea with { UbicacionDestinoId = hueco }]),
                "transferencia-ubicacion-no-encontrada",
                TipoDeError.Validacion,
                campo: "lineas[1].ubicacionDestinoId");
        }

        ExigirElError(
            await IntentarAbrirAsync(escena, modulo, [linea, linea with { ArticuloId = Guid.CreateVersion7() }]),
            "transferencia-articulo-no-encontrado",
            TipoDeError.Validacion,
            campo: "lineas[1].articuloId");

        ExigirElError(
            await IntentarAbrirAsync(
                escena, modulo, [linea, linea with { UnidadIntroducidaId = Guid.CreateVersion7() }]),
            "transferencia-unidad-no-encontrada",
            TipoDeError.Validacion,
            campo: "lineas[1].unidadIntroducidaId");

        foreach (UbicacionDto hueco in new[] { otroDeA, otroDeB })
        {
            using HttpResponseMessage bloqueo = await escena.Cliente.SuprimirAsync(
                $"{LosMaestrosPorLaApi.Ubicaciones}/{hueco.Id}");

            bloqueo.IsSuccessStatusCode.ShouldBeTrue(await Escenario.Detalle(bloqueo));
        }

        ExigirElError(
            await IntentarAbrirAsync(escena, modulo, [linea, linea with { UbicacionOrigenId = otroDeA.Id }]),
            "transferencia-ubicacion-bloqueada",
            TipoDeError.Conflicto,
            actual: new UbicacionDeLaLineaDto(2, UbicacionDeLaLineaDto.Origen));

        ExigirElError(
            await IntentarAbrirAsync(escena, modulo, [linea, linea with { UbicacionDestinoId = otroDeB.Id }]),
            "transferencia-ubicacion-bloqueada",
            TipoDeError.Conflicto,
            actual: new UbicacionDeLaLineaDto(2, UbicacionDeLaLineaDto.Destino));

        using (HttpResponseMessage porLote = await escena.CambiarElArticuloAsync("Bien", "PorLote"))
        {
            porLote.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(porLote));
        }

        ExigirElError(
            await IntentarAbrirAsync(escena, modulo, [linea with { CodigoDeLote = "L-00" }, linea]),
            "transferencia-trazabilidad-no-casa",
            TipoDeError.Conflicto,
            actual: new LineaQueFallaDto(2));

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
            await IntentarAbrirAsync(escena, modulo, [linea]),
            "transferencia-unidad-retirada",
            TipoDeError.Conflicto,
            actual: new LineaQueFallaDto(1));

        using (HttpResponseMessage servicio = await escena.CambiarElArticuloAsync("Servicio", "Ninguna"))
        {
            servicio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(servicio));
        }

        ExigirElError(
            await IntentarAbrirAsync(escena, modulo, [linea]),
            "transferencia-articulo-no-se-almacena",
            TipoDeError.Conflicto,
            actual: new LineaQueFallaDto(1));

        (await TransferenciasDeAsync(escena)).ShouldBe(1, "un alta rechazada no deja un borrador a medias");
    }

    [Fact]
    public async Task Lo_que_no_cabe_en_su_columna_es_400_en_la_cantidad_de_su_linea()
    {
        EscenaDeTransferencia escena = await MontarAsync(908, "LTR-B");

        Guid millonesima = await LosMaestrosPorLaApi.CrearUnidadQueConvierteAAsync(
            escena.Cliente, "K908M", 0.000001m, escena.UnidadId);
        Guid caja = await LosMaestrosPorLaApi.CrearUnidadQueConvierteAAsync(
            escena.Cliente, "K908", 12m, escena.UnidadId);

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        LineaDeTransferenciaDto linea = escena.Linea(escena.UbicacionA, escena.UbicacionB, 1m);

        // LO ESCRITO: el cero y lo negativo no llevan nada, siete decimales se redondearían a otra
        // cantidad, y un billón no cabe en doce cifras enteras.
        foreach (decimal cantidad in new[] { 0m, -1m, 0.0000001m, 1_000_000_000_000m })
        {
            ExigirElError(
                await IntentarAbrirAsync(escena, modulo, [linea, linea with { CantidadIntroducida = cantidad }]),
                "transferencia-cantidad-no-valida",
                TipoDeError.Validacion,
                campo: "lineas[1].cantidadIntroducida");
        }

        // LO QUE MUEVE EN LA UNIDAD BASE: una décima de millonésima redondea a cero, y cien mil
        // millones de cajas de doce se salen. Lo escrito cabe; el factor lo pone el servidor, así que
        // lo que se corrige es la cantidad.
        foreach ((decimal cantidad, Guid unidadId) in new[] { (0.1m, millonesima), (100_000_000_000m, caja) })
        {
            ExigirElError(
                await IntentarAbrirAsync(
                    escena, modulo, [linea with { CantidadIntroducida = cantidad, UnidadIntroducidaId = unidadId }]),
                "transferencia-cantidad-no-valida",
                TipoDeError.Validacion,
                campo: "lineas[0].cantidadIntroducida");
        }

        (await TransferenciasDeAsync(escena)).ShouldBe(0, "un alta rechazada no deja borrador");

        // LAS PAREJAS, en el borde de cada columna.
        foreach ((decimal cantidad, Guid unidadId) in new[]
        {
            (999_999_999_999.999999m, escena.UnidadId),
            (0.000001m, escena.UnidadId),
            (0.5m, millonesima),
            (83_333_333_333m, caja),
        })
        {
            Resultado<TransferenciaDto> alta = await IntentarAbrirAsync(
                escena, modulo, [linea with { CantidadIntroducida = cantidad, UnidadIntroducidaId = unidadId }]);

            alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»: {cantidad} cabe");
        }

        (await TransferenciasDeAsync(escena)).ShouldBe(4);
    }

    [Fact]
    public async Task La_forma_de_la_serie_va_a_su_campo_y_la_unitaria_se_mira_con_el_factor()
    {
        EscenaDeTransferencia escena = await MontarAsync(909, "LTR-C", "PorNumeroSerie");

        Guid deDos = await LosMaestrosPorLaApi.CrearUnidadQueConvierteAAsync(
            escena.Cliente, "K909D", 2m, escena.UnidadId);
        Guid media = await LosMaestrosPorLaApi.CrearUnidadQueConvierteAAsync(
            escena.Cliente, "K909M", 0.5m, escena.UnidadId);

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        LineaDeTransferenciaDto linea = escena.Linea(escena.UbicacionA, escena.UbicacionB, 1m, serie: "SN-01");

        ExigirElError(
            await IntentarAbrirAsync(escena, modulo, [linea, linea with { NumeroDeSerie = "SN 02" }]),
            "transferencia-numero-de-serie-no-valido",
            TipoDeError.Validacion,
            campo: "lineas[1].numeroDeSerie");

        ExigirElError(
            await IntentarAbrirAsync(escena, modulo, [linea, linea with { NumeroDeSerie = " SN-01 " }]),
            "transferencia-serie-repetida",
            TipoDeError.Validacion,
            campo: "lineas[1].numeroDeSerie");

        // TECLEA UNA Y MUEVE DOS: la primera pasada de la forma no la ve, porque todavía no hay
        // factor. Sin la segunda, este borrador se abriría.
        foreach ((decimal cantidad, Guid unidadId) in new[] { (2m, escena.UnidadId), (1m, deDos) })
        {
            ExigirElError(
                await IntentarAbrirAsync(
                    escena, modulo, [linea with { CantidadIntroducida = cantidad, UnidadIntroducidaId = unidadId }]),
                "transferencia-serie-no-unitaria",
                TipoDeError.Validacion,
                campo: "lineas[0].cantidadIntroducida");
        }

        (await TransferenciasDeAsync(escena)).ShouldBe(0, "un alta rechazada no deja borrador");

        // LA PAREJA: teclea dos y mueve una. Si la primera pasada mirara la serie con factor 1, esta
        // tampoco se abriría.
        Resultado<TransferenciaDto> alta = await IntentarAbrirAsync(
            escena, modulo, [linea with { CantidadIntroducida = 2m, UnidadIntroducidaId = media }]);

        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»: dos medias son una unidad base");
    }

    [Fact]
    public async Task Un_lote_que_no_es_de_gs1_va_al_campo_de_su_linea()
    {
        EscenaDeTransferencia escena = await MontarAsync(910, "LTR-D", "PorLote");

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        LineaDeTransferenciaDto linea = escena.Linea(escena.UbicacionA, escena.UbicacionB, 1m, lote: "L-01");

        ExigirElError(
            await IntentarAbrirAsync(escena, modulo, [linea, linea with { CodigoDeLote = "L 02" }]),
            "transferencia-lote-no-valido",
            TipoDeError.Validacion,
            campo: "lineas[1].codigoDeLote");

        (await TransferenciasDeAsync(escena)).ShouldBe(0, "un alta rechazada no deja borrador");

        Resultado<TransferenciaDto> alta = await IntentarAbrirAsync(
            escena, modulo, [linea, linea with { CodigoDeLote = "L-02" }]);

        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");
    }

    private static Task<Resultado<TransferenciaDto>> IntentarAbrirAsync(
        EscenaDeTransferencia escena,
        ElModuloDeInventario modulo,
        IReadOnlyList<LineaDeTransferenciaDto> lineas) =>
        escena.IntentarAbrirAsync(modulo, escena.AlmacenA, escena.AlmacenB, lineas);

    private static void ExigirElError(
        Resultado<TransferenciaDto> alta, string codigo, TipoDeError tipo, string? campo = null, object? actual = null)
    {
        alta.EsCorrecto.ShouldBeFalse($"tenía que salir «{codigo}»");

        ErrorDeOperacion error = alta.Error.ShouldNotBeNull();

        error.Codigo.ShouldBe(codigo, error.Mensaje);
        error.Tipo.ShouldBe(tipo);

        if (campo is not null)
        {
            error.Campos.Keys.ShouldBe([campo], "el campo de la línea que falla, desde cero (ADR-0068 §6)");
        }

        if (actual is not null)
        {
            error.Actual.ShouldBe(actual, "la línea que falla, desde uno, y su punta si es una ubicación");
        }
    }

    private async Task<int> TransferenciasDeAsync(EscenaDeTransferencia escena)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId);

        return await contexto.Transferencias.CountAsync();
    }

    private async Task<EscenaDeTransferencia> MontarAsync(int semilla, string codigo, string trazabilidad = "Ninguna")
    {
        EscenaDeTransferencia escena =
            await EscenaDeTransferencia.MontarAsync(_api, semilla, codigo, semilla, trazabilidad);

        _clientes.Add(escena.Cliente);

        return escena;
    }
}
