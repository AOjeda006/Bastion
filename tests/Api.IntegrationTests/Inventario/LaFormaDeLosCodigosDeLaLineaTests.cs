using System.Globalization;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Un lote o una serie mal escritos se contestan al abrir el borrador, con su <c>type</c>, y no
/// llegan al dominio (ADR-0048 §2 y §3).
/// </summary>
/// <remarks>
/// <para>
/// <b>Son las mismas reglas que lanza el dominio, dichas antes.</b> Ahí son invariantes, y una
/// excepción no lleva <c>type</c>: si el alta dejara de comprobarlas, el cuerpo mal escrito saldría
/// como un fallo sin nombre. Hasta el ítem 2.9 ningún caso lo veía: las mutaciones 87 y 88, que
/// quitaban la serie unitaria y la repetida del alta, salieron verdes en los dos carriles, porque
/// el dominio sí tiene sus casos.
/// </para>
/// <para>
/// <b>El resto del documento es válido</b>: la escena tiene su almacén, su serie y su artículo con
/// la marca que casa. El único rechazo posible es el de la forma, y el caso exige su código.
/// </para>
/// <para>
/// <b>Y desde el 2.17 cada rechazo nombra su campo</b> (ADR-0065 §3): la clave de <c>errors</c> es
/// la ruta de zod, <c>lineas[i].campo</c>, con el índice desde cero, para que el formulario pinte el
/// error en la celda que hay que corregir. La serie unitaria se mira entonces con el factor que pone
/// el servidor, en una segunda pasada, y su caso propio lleva la pareja que pasa.
/// </para>
/// <para>
/// <b>Semillas: las empresas, del 610 al 613; los maestros de instalación, del 614 al 617.</b> Del
/// 600 al 607 y el 620 son de <c>Organizacion.IntegrationTests</c>, y del 596 al 599, de
/// <c>ElCuadreMiraElLoteYLaSerieTests</c>. <b>Y la 876 y la 877</b>, empresa y maestros, del bloque
/// del 2.17, que está en la cabecera de <c>ElFactorDelAjusteTests</c>.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaFormaDeLosCodigosDeLaLineaTests(PostgresConTodosLosModulos postgres) : IDisposable
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
    public async Task Un_lote_que_no_es_de_gs1_no_abre_el_borrador()
    {
        EscenaTrazable escena = await MontarAsync(610, "FCA", 614, "PorLote");

        await ExigirElRechazoAsync(
            escena,
            [
                escena.Entrada(escena.UbicacionA1, 1m, lote: "L-01"),
                escena.Entrada(escena.UbicacionA1, 1m, lote: "L 02"),
            ],
            "ajuste-lote-no-valido",
            "línea 2",
            "lineas[1].codigoDeLote");
    }

    [Fact]
    public async Task Un_numero_de_serie_que_no_es_de_gs1_no_abre_el_borrador()
    {
        EscenaTrazable escena = await MontarAsync(611, "FCB", 615, "PorNumeroSerie");

        await ExigirElRechazoAsync(
            escena,
            [escena.Entrada(escena.UbicacionA1, 1m, serie: "SN 01")],
            "ajuste-numero-de-serie-no-valido",
            "línea 1",
            "lineas[0].numeroDeSerie");
    }

    [Fact]
    public async Task Una_serie_que_mueve_dos_unidades_no_abre_el_borrador()
    {
        EscenaTrazable escena = await MontarAsync(612, "FCC", 616, "PorNumeroSerie");

        await ExigirElRechazoAsync(
            escena,
            [escena.Entrada(escena.UbicacionA1, 2m, serie: "SN-01")],
            "ajuste-serie-no-unitaria",
            "línea 1",
            "lineas[0].cantidadIntroducida");
    }

    [Fact]
    public async Task La_misma_serie_en_dos_lineas_no_abre_el_borrador_aunque_sea_para_moverla()
    {
        EscenaTrazable escena = await MontarAsync(613, "FCD", 617, "PorNumeroSerie");

        // SACARLA DE UN HUECO Y METERLA EN OTRO es la reubicación, que es otro documento. En uno solo,
        // el índice que la tiene en un sitio chocaría o no según el orden en que el motor recorriera
        // las filas.
        await ExigirElRechazoAsync(
            escena,
            [
                escena.Salida(escena.UbicacionA1, 1m, serie: "SN-01"),
                escena.Entrada(escena.UbicacionA2, 1m, serie: " SN-01 "),
            ],
            "ajuste-serie-repetida",
            "línea 2",
            "lineas[1].numeroDeSerie");
    }

    [Fact]
    public async Task La_serie_unitaria_se_mira_con_el_factor_de_la_conversion_y_no_con_lo_tecleado()
    {
        EscenaTrazable escena = await MontarAsync(876, "FCE", 877, "PorNumeroSerie");

        Guid deDos = await LosMaestrosPorLaApi.CrearUnidadQueConvierteAAsync(
            escena.Cliente, "K877D", 2m, escena.UnidadId);
        Guid media = await LosMaestrosPorLaApi.CrearUnidadQueConvierteAAsync(
            escena.Cliente, "K877M", 0.5m, escena.UnidadId);

        // TECLEA UNA Y MUEVE DOS: la primera pasada de la forma no la ve, porque todavía no hay
        // factor. Sin la segunda, este borrador se abriría.
        await ExigirElRechazoAsync(
            escena,
            [escena.Entrada(escena.UbicacionA1, 1m, serie: "SN-01") with { UnidadIntroducidaId = deDos }],
            "ajuste-serie-no-unitaria",
            "línea 1",
            "lineas[0].cantidadIntroducida");

        // LA PAREJA: teclea dos y mueve una. Si la primera pasada mirara la serie con factor 1, esta
        // tampoco se abriría.
        Resultado<AjusteDto> alta = await IntentarAbrirAsync(
            escena, [escena.Entrada(escena.UbicacionA1, 2m, serie: "SN-01") with { UnidadIntroducidaId = media }]);

        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»: dos medias son una unidad base");
    }

    private async Task<EscenaTrazable> MontarAsync(int semilla, string codigo, int maestro, string trazabilidad)
    {
        EscenaTrazable escena = await EscenaTrazable.MontarAsync(_api, semilla, codigo, maestro, trazabilidad);

        _clientes.Add(escena.Cliente);

        return escena;
    }

    private async Task<Resultado<AjusteDto>> IntentarAbrirAsync(
        EscenaTrazable escena, IReadOnlyList<LineaDeAjusteDto> lineas)
    {
        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        return await modulo.Alta.EjecutarAsync(
            new AbrirAjusteDto(
                escena.Serie.Id, escena.AlmacenA, EscenaTrazable.Hoy, "Regularización de un recuento", lineas),
            CancellationToken.None);
    }

    private async Task ExigirElRechazoAsync(
        EscenaTrazable escena, IReadOnlyList<LineaDeAjusteDto> lineas, string codigo, string cualLinea, string campo)
    {
        Resultado<AjusteDto> alta = await IntentarAbrirAsync(escena, lineas);

        alta.EsCorrecto.ShouldBeFalse("el cuerpo está mal escrito y el alta tenía que decirlo");

        ErrorDeOperacion error = alta.Error.ShouldNotBeNull();

        error.Codigo.ShouldBe(codigo, error.Mensaje);
        error.Tipo.ShouldBe(TipoDeError.Validacion, "es un 400: lo que falla es el cuerpo, no la ficha");
        error.Mensaje.ShouldContain(cualLinea);
        error.Campos.Keys.ShouldBe([campo], "la celda que hay que corregir, con el índice de zod (ADR-0065 §3)");

        long ajustes = await ElLibro.EscalarAsync<long>(
            postgres,
            string.Format(
                CultureInfo.InvariantCulture,
                "SELECT count(*) FROM inventario.ajustes WHERE empresa_id = '{0}'",
                escena.EmpresaId));

        ajustes.ShouldBe(0, "un alta rechazada no deja borrador");
    }
}
