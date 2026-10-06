using Bastion.Inventario.Domain.Recuentos;
using Shouldly;
using static Bastion.Inventario.UnitTests.Recuentos.ElRecuentoDeLaPrueba;

namespace Bastion.Inventario.UnitTests.Recuentos;

/// <summary>
/// Las líneas del recuento: la precarga, la línea sin contar, lo que se cuenta y las claves que se
/// añaden o se quitan (ADR-0055 §5 y §6).
/// </summary>
public sealed class LasLineasDelRecuentoTests
{
    [Fact]
    public void La_precarga_entra_sin_contar_y_numerada_por_ubicacion_articulo_lote_y_serie()
    {
        // Desordenada a propósito: el número lo reparte la precarga, no quien la entrega.
        Guid primera = Menor(Estanteria, OtraEstanteria);
        Guid segunda = Mayor(Estanteria, OtraEstanteria);
        Guid antes = Menor(Tornillos, Tuercas);
        Guid despues = Mayor(Tornillos, Tuercas);

        Recuento recuento = Abrir(
            Precargada(segunda, antes),
            Precargada(primera, despues, lote: "L2"),
            Precargada(primera, despues, lote: "L1"),
            Precargada(primera, antes));

        recuento.Lineas.Select(linea => (linea.Numero, linea.UbicacionId, linea.ArticuloId, linea.CodigoDeLote))
            .ShouldBe(
            [
                (1, primera, antes, (string?)null),
                (2, primera, despues, "L1"),
                (3, primera, despues, "L2"),
                (4, segunda, antes, null),
            ]);

        recuento.Lineas.ShouldAllBe(linea => linea.Origen == OrigenDeLaLinea.Precargada);
        recuento.Lineas.ShouldAllBe(linea => linea.Contado == null && linea.TeoricoAlContar == null);
        recuento.Lineas.ShouldAllBe(linea => linea.UnidadBaseId == Unidades);
        recuento.LineasSinContar().Count.ShouldBe(4);
    }

    [Fact]
    public void Una_clave_repetida_en_la_precarga_no_entra()
    {
        Should.Throw<ArgumentException>(() => Abrir(
            Precargada(Estanteria, Tornillos, lote: "L1"),
            Precargada(Estanteria, Tornillos, lote: " L1 ")));
    }

    [Fact]
    public void Contar_cero_es_contar_y_la_linea_deja_de_estar_sin_contar()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos), Precargada(Estanteria, Tuercas));
        LineaDeRecuento tornillos = LaDe(recuento, Tornillos);

        recuento.Contar(tornillos.Id, 0m, 7m);

        tornillos.Contado.ShouldBe(0m, "un cero contado es un dato, no la ausencia de uno (ADR-0055 §5)");
        tornillos.TeoricoAlContar.ShouldBe(7m);
        recuento.LineasSinContar().ShouldBe([LaDe(recuento, Tuercas)]);
    }

    [Fact]
    public void Volver_a_contar_una_linea_pisa_lo_contado_y_el_teorico_de_entonces()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos));
        LineaDeRecuento linea = recuento.Lineas[0];

        recuento.Contar(linea.Id, 4m, 7m);
        recuento.Contar(linea.Id, 6m, 8m);

        linea.Contado.ShouldBe(6m);
        linea.TeoricoAlContar.ShouldBe(8m);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("0.0000001")]
    [InlineData("1000000000000")]
    public void Lo_que_no_cabe_en_una_cantidad_no_se_cuenta(string contado)
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos));
        decimal cantidad = decimal.Parse(contado, System.Globalization.CultureInfo.InvariantCulture);

        LineaDeRecuento.SePuedeContar(cantidad, esUnaSerie: false).ShouldBeFalse();
        Should.Throw<ArgumentOutOfRangeException>(() => recuento.Contar(recuento.Lineas[0].Id, cantidad, 0m));
        recuento.Lineas[0].Contado.ShouldBeNull();
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("1", true)]
    [InlineData("1.000000", true)]
    [InlineData("2", false)]
    [InlineData("0.5", false)]
    public void Un_numero_de_serie_se_cuenta_en_cero_o_en_uno(string contado, bool cabe)
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Taladros, serie: "SN-1"));
        decimal cantidad = decimal.Parse(contado, System.Globalization.CultureInfo.InvariantCulture);
        Guid linea = recuento.Lineas[0].Id;

        LineaDeRecuento.SePuedeContar(cantidad, esUnaSerie: true).ShouldBe(cabe);

        if (cabe)
        {
            recuento.Contar(linea, cantidad, 1m);
            recuento.Lineas[0].Contado.ShouldBe(cantidad);
        }
        else
        {
            Should.Throw<ArgumentOutOfRangeException>(() => recuento.Contar(linea, cantidad, 1m));
        }
    }

    [Fact]
    public void Contar_una_linea_que_no_es_suya_no_se_puede()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos));

        Should.Throw<InvalidOperationException>(() => recuento.Contar(Guid.CreateVersion7(), 1m, 0m));
    }

    [Fact]
    public void Una_clave_anadida_va_detras_sin_contar_y_con_su_coste()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos), Precargada(Estanteria, Tuercas));

        LineaDeRecuento nueva = recuento.AnadirLinea(
            ClaveDelRecuento.De(OtraEstanteria, Taladros, numeroDeSerie: " SN-9 "),
            Unidades,
            costeUnitario: 12.34567m,
            Momento);

        nueva.Numero.ShouldBe(3);
        nueva.Origen.ShouldBe(OrigenDeLaLinea.Anadida);
        nueva.NumeroDeSerie.ShouldBe("SN-9", "el código se guarda como lo normaliza GS1");
        nueva.Contado.ShouldBeNull();

        nueva.CosteUnitario.ShouldBe(
            12.3457m, "se redondea como el de la línea del ajuste que lo llevará, para que digan lo mismo");

        recuento.Lineas.Count.ShouldBe(3);
    }

    [Fact]
    public void Tras_quitar_una_linea_la_siguiente_que_se_anade_no_repite_numero()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos), Precargada(Estanteria, Tuercas));

        recuento.QuitarLinea(recuento.Lineas[0].Id);
        LineaDeRecuento nueva = recuento.AnadirLinea(
            ClaveDelRecuento.De(OtraEstanteria, Taladros), Unidades, costeUnitario: null, Momento);

        nueva.Numero.ShouldBe(
            3, "con la 1 quitada quedan una y la 2: «cuántas hay más uno» daría otra 2, y el índice chocaría");
    }

    [Fact]
    public void Quitar_una_linea_deja_su_clave_fuera_del_recuento()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos), Precargada(Estanteria, Tuercas));

        recuento.QuitarLinea(LaDe(recuento, Tuercas).Id);

        recuento.Lineas.ShouldHaveSingleItem().ArticuloId.ShouldBe(Tornillos);
        recuento.LlevaLaClave(ClaveDelRecuento.De(Estanteria, Tuercas)).ShouldBeFalse();
    }

    [Fact]
    public void Quitar_una_linea_que_no_es_suya_no_se_puede()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos));

        Should.Throw<InvalidOperationException>(() => recuento.QuitarLinea(Guid.CreateVersion7()));
    }

    [Fact]
    public void Una_clave_que_ya_lleva_no_se_anade_otra_vez()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos, lote: "L1"));
        var repetida = ClaveDelRecuento.De(Estanteria, Tornillos, codigoDeLote: "L1 ");

        recuento.LlevaLaClave(repetida).ShouldBeTrue("la clave se compara normalizada");

        Should.Throw<InvalidOperationException>(
            () => recuento.AnadirLinea(repetida, Unidades, costeUnitario: null, Momento));
    }

    [Fact]
    public void Un_numero_de_serie_que_ya_lleva_en_otra_ubicacion_no_se_anade()
    {
        // La misma serie en dos ubicaciones haría que el ajuste la sacara de una y la metiera en otra,
        // y eso es la reubicación, que es otro documento (ADR-0048 §3).
        Recuento recuento = Abrir(Precargada(Estanteria, Taladros, serie: "SN-1"));
        var enOtroSitio = ClaveDelRecuento.De(OtraEstanteria, Taladros, numeroDeSerie: "SN-1");

        recuento.LlevaLaClave(enOtroSitio).ShouldBeFalse();
        recuento.LlevaLaSerie(enOtroSitio).ShouldBeTrue();

        Should.Throw<InvalidOperationException>(
            () => recuento.AnadirLinea(enOtroSitio, Unidades, costeUnitario: null, Momento));
    }

    [Fact]
    public void El_mismo_numero_de_serie_de_otro_articulo_es_otra_serie()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Taladros, serie: "SN-1"));
        var deOtro = ClaveDelRecuento.De(Estanteria, Tornillos, numeroDeSerie: "SN-1");

        recuento.LlevaLaSerie(deOtro).ShouldBeFalse();
        _ = recuento.AnadirLinea(deOtro, Unidades, costeUnitario: null, Momento);
    }

    [Fact]
    public void Un_coste_negativo_no_entra()
    {
        Recuento recuento = Abrir();

        Should.Throw<ArgumentException>(() => recuento.AnadirLinea(
            ClaveDelRecuento.De(Estanteria, Tornillos), Unidades, costeUnitario: -0.01m, Momento));
    }

    [Fact]
    public void Una_clave_lleva_lote_o_serie_y_codigos_de_gs1()
    {
        Should.Throw<ArgumentException>(
            () => ClaveDelRecuento.De(Estanteria, Tornillos, codigoDeLote: "L1", numeroDeSerie: "SN-1"));

        Should.Throw<ArgumentException>(() => ClaveDelRecuento.De(Estanteria, Tornillos, codigoDeLote: "L#1"));
        Should.Throw<ArgumentException>(() => ClaveDelRecuento.De(Estanteria, Tornillos, numeroDeSerie: " "));
        Should.Throw<ArgumentException>(() => ClaveDelRecuento.De(Guid.Empty, Tornillos));
        Should.Throw<ArgumentException>(() => ClaveDelRecuento.De(Estanteria, Guid.Empty));
    }

    private static Guid Menor(Guid una, Guid otra) => una.CompareTo(otra) < 0 ? una : otra;

    private static Guid Mayor(Guid una, Guid otra) => una.CompareTo(otra) < 0 ? otra : una;
}
