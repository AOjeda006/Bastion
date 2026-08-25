using Bastion.BuildingBlocks.Domain.Direcciones;
using Shouldly;

namespace Bastion.Organizacion.UnitTests.Direcciones;

/// <summary>
/// R17: la dirección se guarda en campos estructurados, nunca en dos líneas de texto libre.
/// Los topes de longitud no son estéticos: son los del <em>SEPA Credit Transfer Rulebook</em>,
/// que retira el formato no estructurado el 15 de noviembre de 2026.
/// </summary>
public sealed class DireccionEstructuradaR17Tests
{
    private static Direccion Valida() => Direccion.De(
        calle: "Gran Vía",
        numero: "31",
        codigoPostal: "28013",
        poblacion: "Madrid",
        subdivision: "Madrid",
        pais: "es");

    [Fact]
    public void Una_direccion_conserva_cada_campo_por_separado()
    {
        Direccion direccion = Valida();

        direccion.Calle.ShouldBe("Gran Vía");
        direccion.Numero.ShouldBe("31");
        direccion.CodigoPostal.ShouldBe("28013");
        direccion.Poblacion.ShouldBe("Madrid");
        direccion.Subdivision.ShouldBe("Madrid");
    }

    [Fact]
    public void El_pais_se_normaliza_a_mayusculas_porque_es_ISO_3166_1_alfa_2()
    {
        Valida().Pais.ShouldBe("ES");
    }

    [Fact]
    public void La_linea_unica_es_una_funcion_que_compone_no_un_campo_que_se_guarda()
    {
        Valida().EnUnaLinea().ShouldBe("Gran Vía 31, 28013 Madrid, Madrid, ES");
    }

    [Fact]
    public void La_linea_unica_no_deja_separadores_huerfanos_cuando_faltan_los_campos_opcionales()
    {
        var sinNumeroNiSubdivision = Direccion.De(
            calle: "Rúa do Franco",
            numero: null,
            codigoPostal: "15702",
            poblacion: "Santiago de Compostela",
            subdivision: null,
            pais: "ES");

        sinNumeroNiSubdivision.EnUnaLinea()
            .ShouldBe("Rúa do Franco, 15702 Santiago de Compostela, ES");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void La_calle_es_obligatoria(string? calle)
    {
        Should.Throw<ArgumentException>(() => Direccion.De(
            calle!, "31", "28013", "Madrid", "Madrid", "ES"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void El_codigo_postal_es_obligatorio(string codigoPostal)
    {
        Should.Throw<ArgumentException>(() => Direccion.De(
            "Gran Vía", "31", codigoPostal, "Madrid", "Madrid", "ES"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void La_poblacion_es_obligatoria(string poblacion)
    {
        Should.Throw<ArgumentException>(() => Direccion.De(
            "Gran Vía", "31", "28013", poblacion, "Madrid", "ES"));
    }

    [Theory]
    [InlineData("E")]
    [InlineData("ESP")]
    [InlineData("E5")]
    [InlineData("")]
    public void El_pais_tiene_que_ser_un_codigo_de_dos_letras(string pais)
    {
        Should.Throw<ArgumentException>(() => Direccion.De(
            "Gran Vía", "31", "28013", "Madrid", "Madrid", pais));
    }

    [Fact]
    public void Los_campos_opcionales_vacios_se_guardan_como_nulos_y_no_como_cadena_en_blanco()
    {
        var direccion = Direccion.De(
            "Gran Vía", "   ", "28013", "Madrid", "   ", "ES");

        direccion.Numero.ShouldBeNull();
        direccion.Subdivision.ShouldBeNull();
    }

    [Fact]
    public void Los_topes_son_los_del_rulebook_de_SEPA_y_no_una_estimacion()
    {
        // Estos seis numeros NO son opinion nuestra: son las longitudes del SEPA Credit
        // Transfer Rulebook para la direccion estructurada. Si alguien los cambia "porque
        // cabia mas", este test lo para: una direccion que no cabe en el rulebook es una
        // transferencia que no se cursa a partir del 15-nov-2026.
        Direccion.LongitudMaximaDeCalle.ShouldBe(70);          // StreetName
        Direccion.LongitudMaximaDeNumero.ShouldBe(16);         // BuildingNumber
        Direccion.LongitudMaximaDeCodigoPostal.ShouldBe(16);   // PostCode
        Direccion.LongitudMaximaDePoblacion.ShouldBe(35);      // TownName
        Direccion.LongitudMaximaDeSubdivision.ShouldBe(35);    // CountrySubDivision
        Direccion.LongitudDelPais.ShouldBe(2);                 // Country (ISO 3166-1 alfa-2)
    }

    [Fact]
    public void Una_calle_mas_larga_que_el_tope_de_SEPA_no_se_acepta()
    {
        string demasiadoLarga = new('a', Direccion.LongitudMaximaDeCalle + 1);

        Should.Throw<ArgumentException>(() => Direccion.De(
            demasiadoLarga, "31", "28013", "Madrid", "Madrid", "ES"));
    }

    [Fact]
    public void Dos_direcciones_con_los_mismos_campos_son_la_misma_direccion()
    {
        Valida().ShouldBe(Valida());
    }
}
