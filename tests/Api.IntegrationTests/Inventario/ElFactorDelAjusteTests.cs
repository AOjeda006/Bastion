using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Organizacion.Contracts.Unidades;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// El factor de cada línea del ajuste lo pone el servidor al abrir el borrador, y lo que se guarda
/// es lo que manda después (ADR-0065 §2 y §4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Hasta el ítem 2.17 el factor lo tecleaba el cliente</b>, y una línea podía decir que una caja
/// tenía doce unidades o tres, según quién la escribiera. Ahora la línea dice su unidad, y el alta
/// busca la conversión de esa unidad a la base de su artículo: el par exacto, sin la inversa y sin
/// encadenar. Sin ella, un <c>422</c>; con ella retirada, un <c>409</c>. Los dos nombran la primera
/// línea que falla, por su orden, con las dos unidades del par.
/// </para>
/// <para>
/// <b>Y el factor se copia, no se vuelve a preguntar.</b> Confirmar usa el que se guardó al abrir,
/// y el inverso de una anulación copia el del original: una conversión que cambia o se retira
/// después no mueve lo que ya se dijo. Si se volviera a preguntar, anular un documento con su
/// conversión retirada sería imposible, y con su factor cambiado dejaría el libro descuadrado.
/// </para>
/// <para>
/// <b>Cada rechazo lleva su pareja</b>: la misma petición, con la conversión en regla, pasa.
/// </para>
/// <para>
/// <b>Semillas: del 876 al 898, el bloque del 2.17.</b> La 876 y la 877 son de
/// <c>LaFormaDeLosCodigosDeLaLineaTests</c>, empresa y maestros de instalación; de la 878 a la 883,
/// de este fichero, empresas y maestros con el mismo número. De la 884 a la 886, de
/// <c>ElBorradorDescartadoTests</c>, igual. Del 887 al 898, libres. Las unidades
/// nuevas llevan el código <c>K</c> y su semilla, que no repite nadie: las del artículo son
/// <c>W</c>.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElFactorDelAjusteTests(PostgresConTodosLosModulos postgres) : IDisposable
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
    public async Task La_linea_en_la_unidad_base_lleva_factor_uno_y_la_otra_el_de_su_conversion()
    {
        EscenaDeTransferencia escena = await MontarAsync(878, "FAJ-A");

        Guid caja = await LosMaestrosPorLaApi.CrearUnidadQueConvierteAAsync(
            escena.Cliente, "K878", 12m, escena.UnidadId);

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        Resultado<AjusteDto> alta = await IntentarAbrirAsync(
            escena, modulo, [Entrada(escena, 5m, escena.UnidadId), Entrada(escena, 3m, caja)]);

        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");

        // EN LA MISMA PETICIÓN, para que se vea que el factor es de cada línea y no del documento.
        Ajuste guardado = await ElAjusteAsync(escena, alta.Valor.Id);

        guardado.Lineas.Select(linea => linea.FactorAUnidadBase).ShouldBe(
            [1m, 12m], "la base no pregunta y vale 1; la caja vale lo que dice su conversión");

        (await modulo.ConfirmarAsync(alta.Valor.Id)).EsCorrecto.ShouldBeTrue();

        (await escena.FilasDelLibroAsync(postgres, alta.Valor.Id))
            .Select(fila => fila.CantidadEnUnidadBase)
            .ShouldBe([5m, 36m], "el libro cuenta en unidad base: cinco sueltas y tres cajas de doce");
    }

    [Fact]
    public async Task Una_unidad_sin_conversion_a_la_base_es_422_aunque_la_haya_al_reves()
    {
        EscenaDeTransferencia escena = await MontarAsync(879, "FAJ-B");

        // AL REVÉS: de la base a la unidad nueva, que es la fila que NO sirve. El alta pide el par
        // exacto; si mirara la inversa, esta línea pasaría con el factor que no es.
        Guid alReves = await LosMaestrosPorLaApi.CrearUnidadAsync(escena.Cliente, "K879");
        await LosMaestrosPorLaApi.CrearConversionAsync(escena.Cliente, escena.UnidadId, alReves, 4m);

        Guid sinNada = await LosMaestrosPorLaApi.CrearUnidadAsync(escena.Cliente, "K879S");

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        LineaDeAjusteDto[] lineas =
        [
            Entrada(escena, 5m, escena.UnidadId),
            Entrada(escena, 3m, alReves),
            Entrada(escena, 1m, sinNada),
        ];

        // DOS LÍNEAS QUE FALLAN, y se nombra la primera por su orden.
        ExigirElError(
            await IntentarAbrirAsync(escena, modulo, lineas),
            "ajuste-sin-conversion",
            TipoDeError.ReglaDeNegocio,
            new ConversionDeLaLineaDto(2, alReves, escena.UnidadId));

        // CON LA SUYA, la segunda pasa y la que falla es la tercera.
        await LosMaestrosPorLaApi.CrearConversionAsync(escena.Cliente, alReves, escena.UnidadId, 0.25m);

        ExigirElError(
            await IntentarAbrirAsync(escena, modulo, lineas),
            "ajuste-sin-conversion",
            TipoDeError.ReglaDeNegocio,
            new ConversionDeLaLineaDto(3, sinNada, escena.UnidadId));

        (await AjustesDeAsync(escena)).ShouldBe(0, "un alta rechazada no deja borrador");

        // LA PAREJA: sin la tercera, el documento abre, con el factor de la fila en su sentido.
        Resultado<AjusteDto> alta = await IntentarAbrirAsync(escena, modulo, lineas[..2]);

        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");

        (await ElAjusteAsync(escena, alta.Valor.Id)).Lineas.Select(linea => linea.FactorAUnidadBase)
            .ShouldBe([1m, 0.25m], "el de la unidad a la base, no el 4 de la base a la unidad");
    }

    [Fact]
    public async Task Una_conversion_retirada_es_409_y_antes_de_retirarla_la_misma_linea_pasa()
    {
        EscenaDeTransferencia escena = await MontarAsync(880, "FAJ-C");

        Guid caja = await LosMaestrosPorLaApi.CrearUnidadAsync(escena.Cliente, "K880");
        ConversionUmDto conversion =
            await LosMaestrosPorLaApi.CrearConversionAsync(escena.Cliente, caja, escena.UnidadId, 6m);

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        LineaDeAjusteDto linea = Entrada(escena, 1m, caja);

        Resultado<AjusteDto> antes = await IntentarAbrirAsync(escena, modulo, [linea]);

        antes.EsCorrecto.ShouldBeTrue($"«{antes.Error?.Codigo}»");

        await RetirarAsync(escena, conversion);

        // RETIRADA NO ES AUSENTE: el par sigue existiendo y el código lo dice, para que quien
        // teclea sepa que lo que tiene que hacer es reincorporarla y no darla de alta otra vez.
        ExigirElError(
            await IntentarAbrirAsync(escena, modulo, [linea]),
            "ajuste-conversion-retirada",
            TipoDeError.Conflicto,
            new ConversionDeLaLineaDto(1, caja, escena.UnidadId));

        (await AjustesDeAsync(escena)).ShouldBe(1, "solo el de antes de retirarla");
    }

    [Fact]
    public async Task Confirmar_usa_el_factor_que_se_guardo_aunque_la_conversion_cambie_o_se_retire()
    {
        EscenaDeTransferencia escena = await MontarAsync(881, "FAJ-D");

        Guid caja = await LosMaestrosPorLaApi.CrearUnidadAsync(escena.Cliente, "K881");
        ConversionUmDto conversion =
            await LosMaestrosPorLaApi.CrearConversionAsync(escena.Cliente, caja, escena.UnidadId, 12m);

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        Guid conCambio = await escena.AbrirUnAjusteAsync(modulo, escena.AlmacenA, Entrada(escena, 2m, caja));
        Guid conRetirada = await escena.AbrirUnAjusteAsync(modulo, escena.AlmacenA, Entrada(escena, 3m, caja));

        await CambiarElFactorAsync(escena, conversion, 10m);

        (await modulo.ConfirmarAsync(conCambio)).EsCorrecto.ShouldBeTrue();

        (await escena.FilasDelLibroAsync(postgres, conCambio)).ShouldHaveSingleItem().CantidadEnUnidadBase
            .ShouldBe(24m, "dos cajas de las doce con que se abrió, no de las diez de ahora");

        await RetirarAsync(escena, conversion);

        Resultado<AjusteDto> confirmado = await modulo.ConfirmarAsync(conRetirada);

        confirmado.EsCorrecto.ShouldBeTrue($"«{confirmado.Error?.Codigo}»: confirmar no vuelve a preguntar");

        (await escena.FilasDelLibroAsync(postgres, conRetirada)).ShouldHaveSingleItem().CantidadEnUnidadBase
            .ShouldBe(36m, "tres cajas de doce: la conversión retirada no se mira");
    }

    [Fact]
    public async Task El_inverso_copia_el_factor_del_original_aunque_su_conversion_se_haya_retirado()
    {
        EscenaDeTransferencia escena = await MontarAsync(882, "FAJ-E");

        Guid caja = await LosMaestrosPorLaApi.CrearUnidadAsync(escena.Cliente, "K882");
        ConversionUmDto conversion =
            await LosMaestrosPorLaApi.CrearConversionAsync(escena.Cliente, caja, escena.UnidadId, 12m);

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        Guid ajusteId = await UnoConfirmadoAsync(escena, modulo, Entrada(escena, 3m, caja));

        await RetirarAsync(escena, conversion);

        await ExigirElInversoAsync(escena, modulo, ajusteId, caja);
    }

    [Fact]
    public async Task El_inverso_copia_el_factor_del_original_aunque_su_conversion_haya_cambiado()
    {
        EscenaDeTransferencia escena = await MontarAsync(883, "FAJ-F");

        Guid caja = await LosMaestrosPorLaApi.CrearUnidadAsync(escena.Cliente, "K883");
        ConversionUmDto conversion =
            await LosMaestrosPorLaApi.CrearConversionAsync(escena.Cliente, caja, escena.UnidadId, 12m);

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        Guid ajusteId = await UnoConfirmadoAsync(escena, modulo, Entrada(escena, 3m, caja));

        await CambiarElFactorAsync(escena, conversion, 10m);

        await ExigirElInversoAsync(escena, modulo, ajusteId, caja);
    }

    /// <summary>
    /// Anula el ajuste de tres cajas de doce y exige que el inverso lleve el mismo factor y saque
    /// del libro lo mismo que entró.
    /// </summary>
    private async Task ExigirElInversoAsync(
        EscenaDeTransferencia escena, ElModuloDeInventario modulo, Guid ajusteId, Guid caja)
    {
        Resultado<AnulacionDto> anulacion = await modulo.AnularAsync(ajusteId, "Se contó mal");

        anulacion.EsCorrecto.ShouldBeTrue($"«{anulacion.Error?.Codigo}»: anular no vuelve a preguntar");

        Ajuste inverso = await ElAjusteAsync(escena, anulacion.Valor.Inverso.Id);
        LineaDeAjuste inversa = inverso.Lineas.ShouldHaveSingleItem();

        inversa.UnidadIntroducidaId.ShouldBe(caja);
        inversa.CantidadIntroducida.ShouldBe(-3m);
        inversa.FactorAUnidadBase.ShouldBe(12m, "el del original, no el de la conversión de ahora");

        (await escena.FilasDelLibroAsync(postgres, anulacion.Valor.Inverso.Id))
            .ShouldHaveSingleItem()
            .CantidadEnUnidadBase
            .ShouldBe(-36m, "sale lo que entró, y el saldo vuelve a cero");
    }

    private static LineaDeAjusteDto Entrada(EscenaDeTransferencia escena, decimal cantidad, Guid unidadId) =>
        new(escena.UbicacionA, escena.ArticuloId, cantidad, unidadId, 2m);

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

    private static async Task<Guid> UnoConfirmadoAsync(
        EscenaDeTransferencia escena, ElModuloDeInventario modulo, LineaDeAjusteDto linea)
    {
        Guid ajusteId = await escena.AbrirUnAjusteAsync(modulo, escena.AlmacenA, linea);

        Resultado<AjusteDto> confirmado = await modulo.ConfirmarAsync(ajusteId);

        confirmado.EsCorrecto.ShouldBeTrue($"«{confirmado.Error?.Codigo}»");

        return ajusteId;
    }

    private static void ExigirElError(
        Resultado<AjusteDto> alta, string codigo, TipoDeError tipo, ConversionDeLaLineaDto actual)
    {
        alta.EsCorrecto.ShouldBeFalse($"tenía que salir «{codigo}»");

        ErrorDeOperacion error = alta.Error.ShouldNotBeNull();

        error.Codigo.ShouldBe(codigo, error.Mensaje);
        error.Tipo.ShouldBe(tipo);
        error.Actual.ShouldBe(actual, "la línea que falla, desde uno, y las dos unidades del par que no está");
    }

    private static async Task RetirarAsync(EscenaDeTransferencia escena, ConversionUmDto conversion)
    {
        string recurso = $"{LosMaestrosPorLaApi.Conversiones}/{conversion.Id}";

        using HttpResponseMessage retirada = await escena.Cliente.AccionarAsync(
            recurso, $"{recurso}/retirada", HttpMethod.Post);

        retirada.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(retirada));
    }

    private static async Task CambiarElFactorAsync(
        EscenaDeTransferencia escena, ConversionUmDto conversion, decimal factor)
    {
        using HttpResponseMessage cambio = await escena.Cliente.ModificarAsync(
            $"{LosMaestrosPorLaApi.Conversiones}/{conversion.Id}", new ModificarConversionUmDto { Factor = factor });

        cambio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(cambio));
        (await cambio.Content.ReadFromJsonAsync<ConversionUmDto>())!.Factor.ShouldBe(factor);
    }

    private async Task<Ajuste> ElAjusteAsync(EscenaDeTransferencia escena, Guid ajusteId)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId);

        return await contexto.Ajustes.AsNoTracking().SingleAsync(fila => fila.Id == ajusteId);
    }

    private async Task<int> AjustesDeAsync(EscenaDeTransferencia escena)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId);

        return await contexto.Ajustes.CountAsync();
    }

    private async Task<EscenaDeTransferencia> MontarAsync(int semilla, string codigo)
    {
        EscenaDeTransferencia escena = await EscenaDeTransferencia.MontarAsync(_api, semilla, codigo, semilla);

        _clientes.Add(escena.Cliente);

        return escena;
    }
}
