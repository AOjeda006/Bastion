using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.UnitTests.Valoraciones;
using Shouldly;

namespace Bastion.Inventario.UnitTests.LotesYSeries;

/// <summary>
/// La línea del ajuste lleva el código de su lote o de su serie, y la fila del libro, el
/// identificador que la confirmación resolvió (ADR-0048 §2, §3 y §5).
/// </summary>
/// <remarks>
/// <para>
/// <b>El borrador guarda el texto y el libro, la fila.</b> Lo que escribió el usuario es un código,
/// y lo que identifica un lote es su artículo y su código juntos. La confirmación los resuelve en la
/// base y el dominio los recibe ya resueltos, como recibe el número y la valoración: no hay forma de
/// confirmar un documento que nombra un lote sin decirle qué fila es.
/// </para>
/// <para>
/// <b>Las reglas de la serie son dos, y ninguna la ve el motor por sí solo.</b> Una línea con serie
/// mueve una unidad base, y una serie sale una sola vez por documento: el índice único se comprueba
/// fila a fila, y mover la misma serie dos veces en una sentencia chocaría o no según el orden en que
/// el motor recorriera las filas.
/// </para>
/// </remarks>
public sealed class LaLineaLlevaSuLoteOSuSerieTests
{
    private static readonly DateTimeOffset s_momento = new(2026, 9, 29, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly s_dia = new(2026, 9, 28);

    [Fact]
    public void Una_linea_con_lote_lo_guarda_recortado_y_sin_serie()
    {
        Ajuste ajuste = UnAjuste();

        ConLinea(ajuste, Guid.CreateVersion7(), 5m, lote: "  L-01 ");

        ajuste.Lineas[0].CodigoDeLote.ShouldBe("L-01");
        ajuste.Lineas[0].NumeroDeSerie.ShouldBeNull();
    }

    [Theory]
    [InlineData("L 01")]
    [InlineData("")]
    [InlineData("LOTE-DE-VEINTIUNO-XXX")]
    public void Un_lote_que_no_es_de_gs1_lanza(string lote) =>
        Should.Throw<ArgumentException>(() => ConLinea(UnAjuste(), Guid.CreateVersion7(), 5m, lote: lote));

    [Fact]
    public void Una_serie_que_no_es_de_gs1_lanza() =>
        Should.Throw<ArgumentException>(
            () => ConLinea(UnAjuste(), Guid.CreateVersion7(), 1m, serie: "SN#1"));

    [Fact]
    public void Lote_y_serie_a_la_vez_lanza() =>
        Should.Throw<ArgumentException>(
                () => ConLinea(UnAjuste(), Guid.CreateVersion7(), 1m, lote: "L-01", serie: "SN-1"))
            .Message.ShouldContain("ADR-0048");

    [Theory]
    [InlineData(1, 1)]
    [InlineData(-1, 1)]
    [InlineData(0.25, 4)]
    [InlineData(-0.5, 2)]
    public void Una_serie_mueve_una_unidad_base_arriba_o_abajo(decimal cantidad, decimal factor)
    {
        Ajuste ajuste = UnAjuste();

        ConLinea(ajuste, Guid.CreateVersion7(), cantidad, factor, serie: "SN-1");

        ajuste.Lineas[0].NumeroDeSerie.ShouldBe("SN-1");
        ajuste.Lineas[0].CodigoDeLote.ShouldBeNull();
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(1, 2)]
    [InlineData(0.5, 1)]
    [InlineData(-3, 1)]
    public void Una_serie_que_mueve_otra_cosa_lanza(decimal cantidad, decimal factor) =>
        Should.Throw<ArgumentException>(
            () => ConLinea(UnAjuste(), Guid.CreateVersion7(), cantidad, factor, serie: "SN-1"));

    [Fact]
    public void La_misma_serie_del_mismo_articulo_sale_una_sola_vez_por_documento()
    {
        Ajuste ajuste = UnAjuste();
        var articulo = Guid.CreateVersion7();

        ConLinea(ajuste, articulo, -1m, serie: "SN-1");

        // De otra estantería y con el signo contrario: es la reubicación, que es otro documento.
        Should.Throw<InvalidOperationException>(() => ConLinea(ajuste, articulo, 1m, serie: "SN-1"))
            .Message.ShouldContain("ADR-0048");

        ajuste.Lineas.Count.ShouldBe(1, "la línea rechazada no se queda en el documento");
    }

    [Fact]
    public void La_misma_serie_de_otro_articulo_y_el_mismo_lote_dos_veces_si_entran()
    {
        Ajuste ajuste = UnAjuste();
        var articulo = Guid.CreateVersion7();

        ConLinea(ajuste, articulo, 1m, serie: "SN-1");
        ConLinea(ajuste, Guid.CreateVersion7(), 1m, serie: "SN-1");

        var conLote = Guid.CreateVersion7();

        ConLinea(ajuste, conLote, 5m, lote: "L-01");
        ConLinea(ajuste, conLote, -2m, lote: "L-01");

        ajuste.Lineas.Count.ShouldBe(4);
    }

    [Fact]
    public void Lo_que_el_documento_nombra_sale_sin_repetir_y_ordenado()
    {
        Ajuste ajuste = UnAjuste();
        Guid primero = new("00000000-0000-7000-8000-000000000001");
        Guid segundo = new("00000000-0000-7000-8000-000000000002");

        ConLinea(ajuste, segundo, 5m, lote: "B");
        ConLinea(ajuste, primero, 5m, lote: "b");
        ConLinea(ajuste, primero, 5m, lote: "A");
        ConLinea(ajuste, segundo, 3m, lote: "B");
        ConLinea(ajuste, primero, 1m, serie: "SN-2");
        ConLinea(ajuste, segundo, 1m, serie: "SN-1");
        ConLinea(ajuste, Guid.CreateVersion7(), 5m);

        ajuste.LotesQueNombra().ShouldBe(
        [
            new CodigoDeUnArticulo(primero, "A"),
            new CodigoDeUnArticulo(primero, "b"),
            new CodigoDeUnArticulo(segundo, "B"),
        ]);

        ajuste.SeriesQueNombra().ShouldBe(
        [
            new CodigoDeUnArticulo(primero, "SN-2"),
            new CodigoDeUnArticulo(segundo, "SN-1"),
        ]);
    }

    [Fact]
    public void Confirmar_pone_en_cada_fila_el_lote_o_la_serie_que_se_resolvio()
    {
        Ajuste ajuste = UnAjuste();
        var conLote = Guid.CreateVersion7();
        var conSerie = Guid.CreateVersion7();

        ConLinea(ajuste, conLote, 5m, lote: "L-01");
        ConLinea(ajuste, conSerie, 1m, serie: "SN-1");
        ConLinea(ajuste, Guid.CreateVersion7(), 2m);

        var lote = Guid.CreateVersion7();
        var serie = Guid.CreateVersion7();

        IReadOnlyList<MovimientoStock> filas = ajuste.Confirmar(
            3,
            Confirmado(ajuste),
            LaValoracion.DesdeCero(ajuste),
            new LotesYSeriesResueltos(
                new Dictionary<CodigoDeUnArticulo, Guid> { [new(conLote, "L-01")] = lote },
                new Dictionary<CodigoDeUnArticulo, Guid> { [new(conSerie, "SN-1")] = serie }),
            s_momento);

        filas.Select(fila => (fila.LoteId, fila.NumeroDeSerieId)).ShouldBe(
        [
            (lote, (Guid?)null),
            (null, serie),
            (null, null),
        ]);
    }

    [Fact]
    public void Sin_resolver_un_codigo_no_se_confirma_y_el_documento_sigue_en_borrador()
    {
        Ajuste ajuste = UnAjuste();

        ConLinea(ajuste, Guid.CreateVersion7(), 5m, lote: "L-01");

        Should.Throw<ArgumentException>(() => ajuste.Confirmar(
            3, Confirmado(ajuste), LaValoracion.DesdeCero(ajuste), LotesYSeriesResueltos.Ninguno, s_momento));

        ajuste.Estado.ShouldBe(EstadoDeAjuste.Borrador, "la guarda va antes de transitar");
        ajuste.Numero.ShouldBeNull();
        ajuste.Lineas[0].Valor.ShouldBeNull("ninguna línea anota su valor si el documento no se confirma");
    }

    [Fact]
    public void El_inverso_copia_el_lote_y_la_serie()
    {
        Ajuste ajuste = UnAjuste();
        var conLote = Guid.CreateVersion7();
        var conSerie = Guid.CreateVersion7();

        ConLinea(ajuste, conLote, 5m, lote: "L-01");
        ConLinea(ajuste, conSerie, 1m, serie: "SN-1");

        ajuste.Confirmar(
            3,
            Confirmado(ajuste),
            LaValoracion.DesdeCero(ajuste),
            new LotesYSeriesResueltos(
                new Dictionary<CodigoDeUnArticulo, Guid> { [new(conLote, "L-01")] = Guid.CreateVersion7() },
                new Dictionary<CodigoDeUnArticulo, Guid> { [new(conSerie, "SN-1")] = Guid.CreateVersion7() }),
            s_momento);

        Ajuste inverso = ajuste.CrearInverso(s_dia.AddDays(1), "Recuento mal hecho", s_momento);

        inverso.Lineas.Select(linea => (linea.CodigoDeLote, linea.NumeroDeSerie)).ShouldBe(
        [
            ("L-01", (string?)null),
            (null, "SN-1"),
        ]);

        inverso.LotesQueNombra().ShouldBe(ajuste.LotesQueNombra(), "resuelven a las mismas filas");
        inverso.SeriesQueNombra().ShouldBe(ajuste.SeriesQueNombra());
    }

    [Fact]
    public void Una_fila_del_libro_no_lleva_lote_y_serie_a_la_vez() =>
        Should.Throw<ArgumentException>(() => UnaFila(1m, Guid.CreateVersion7(), Guid.CreateVersion7()));

    [Fact]
    public void Una_fila_del_libro_con_serie_mueve_una_unidad_base()
    {
        Should.Throw<ArgumentException>(() => UnaFila(2m, loteId: null, Guid.CreateVersion7()));

        UnaFila(-1m, loteId: null, Guid.CreateVersion7()).CantidadEnUnidadBase.ShouldBe(-1m);
        UnaFila(7m, Guid.CreateVersion7(), numeroDeSerieId: null).CantidadEnUnidadBase.ShouldBe(7m);
    }

    private static MovimientoStock UnaFila(decimal cantidad, Guid? loteId, Guid? numeroDeSerieId) =>
        MovimientoStock.Registrar(
            Guid.CreateVersion7(),
            s_dia,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            loteId,
            numeroDeSerieId,
            cantidad,
            Guid.CreateVersion7(),
            1m,
            "EUR",
            null,
            Importe.Cero("EUR"),
            PrecioUnitario.De(0m, "EUR"),
            TipoDeDocumentoOrigen.Ajuste,
            Guid.CreateVersion7(),
            s_momento);

    private static Ajuste UnAjuste() => Ajuste.Abrir(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        s_dia,
        "Recuento de septiembre",
        "EUR",
        s_momento);

    private static void ConLinea(
        Ajuste ajuste,
        Guid articulo,
        decimal cantidad,
        decimal factor = 1m,
        string? lote = null,
        string? serie = null) =>
        ajuste.AnadirLinea(
            Guid.CreateVersion7(),
            articulo,
            cantidad,
            Guid.CreateVersion7(),
            factor,
            cantidad > 0m ? 2.50m : null,
            s_momento,
            codigoDeLote: lote,
            numeroDeSerie: serie);

    private static AjusteConfirmado Confirmado(Ajuste ajuste) => new(
        ajuste.Id, ajuste.EmpresaId, ajuste.AlmacenId, ajuste.FechaDeOperacion, ajuste.Lineas.Count);
}
