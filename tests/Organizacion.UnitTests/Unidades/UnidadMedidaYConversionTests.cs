using Bastion.Organizacion.Domain.Unidades;
using Shouldly;

namespace Bastion.Organizacion.UnitTests.Unidades;

public sealed class UnidadMedidaYConversionTests
{
    private static readonly DateTimeOffset s_momento = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid s_caja = Guid.Parse("2f6d5f4e-0000-4000-8000-0000000000c0");
    private static readonly Guid s_unidad = Guid.Parse("2f6d5f4e-0000-4000-8000-0000000000a0");

    [Fact]
    public void Una_unidad_nace_con_su_codigo_normalizado()
    {
        var unidad = UnidadMedida.Crear(" kg ", " Kilogramo ", 3, s_momento);

        unidad.Codigo.ShouldBe("KG");
        unidad.Nombre.ShouldBe("Kilogramo");
        unidad.Decimales.ShouldBe(3);
        unidad.CreadoEn.ShouldBe(s_momento);
    }

    [Fact]
    public void Una_unidad_que_no_se_parte_lleva_cero_decimales()
    {
        // No existe media unidad de un tornillo, y el cero es la forma de decirlo una vez en vez
        // de tener que acordarse en cada línea de albarán.
        UnidadMedida.Crear("UD", "Unidad", 0, s_momento).Decimales.ShouldBe(0);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void Unos_decimales_fuera_de_rango_no_se_guardan(int decimales)
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => UnidadMedida.Crear("KG", "Kilogramo", decimales, s_momento));
    }

    [Fact]
    public void Modificar_una_unidad_cambia_el_nombre_y_no_los_decimales()
    {
        // Bajarlos de tres a cero convertiría cada existencia de 1,250 kg ya registrada en un
        // número que la propia unidad dice que no puede existir. La garantía es que `Modificar`
        // no los recibe; esto lo deja escrito como comportamiento.
        var unidad = UnidadMedida.Crear("KG", "Kilogramo", 3, s_momento);

        unidad.Modificar("Kilogramos");

        unidad.Nombre.ShouldBe("Kilogramos");
        unidad.Decimales.ShouldBe(3);
        unidad.Codigo.ShouldBe("KG");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void El_codigo_y_el_nombre_de_una_unidad_son_obligatorios(string vacio)
    {
        Should.Throw<ArgumentException>(() => UnidadMedida.Crear(vacio, "Kilogramo", 3, s_momento));
        Should.Throw<ArgumentException>(() => UnidadMedida.Crear("KG", vacio, 3, s_momento));
    }

    [Fact]
    public void Una_conversion_lleva_la_direccion_escrita()
    {
        // Una caja son doce unidades: 12 de destino por UNA de origen.
        var conversion = ConversionUM.Crear(s_caja, s_unidad, 12m, s_momento);

        conversion.UnidadOrigenId.ShouldBe(s_caja);
        conversion.UnidadDestinoId.ShouldBe(s_unidad);
        conversion.Factor.ShouldBe(12m);
    }

    [Fact]
    public void El_factor_se_redondea_a_seis_decimales()
    {
        var conversion = ConversionUM.Crear(s_caja, s_unidad, 0.0833333333m, s_momento);

        conversion.Factor.ShouldBe(0.083333m);
    }

    [Fact]
    public void Una_unidad_contra_si_misma_no_es_una_conversion()
    {
        Should.Throw<ArgumentException>(() => ConversionUM.Crear(s_caja, s_caja, 1m, s_momento));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-12)]
    public void Un_factor_que_no_es_mayor_que_cero_no_se_guarda(decimal factor)
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => ConversionUM.Crear(s_caja, s_unidad, factor, s_momento));
    }

    [Fact]
    public void Una_conversion_sin_unidad_no_existe()
    {
        Should.Throw<ArgumentException>(
            () => ConversionUM.Crear(Guid.Empty, s_unidad, 12m, s_momento));

        Should.Throw<ArgumentException>(
            () => ConversionUM.Crear(s_caja, Guid.Empty, 12m, s_momento));
    }

    [Fact]
    public void Ir_y_volver_con_el_inverso_redondeado_NO_devuelve_la_cantidad_de_partida()
    {
        // Este test no comprueba una regla: documenta POR QUÉ la conversión inversa no se calcula
        // sola. 1/12 no cabe en seis decimales, así que doce unidades convertidas a cajas y de
        // vuelta a unidades no dan doce. Quien necesite la vuelta la da de alta con su factor.
        var ida = ConversionUM.Crear(s_caja, s_unidad, 12m, s_momento);
        var vuelta = ConversionUM.Crear(s_unidad, s_caja, 1m / 12m, s_momento);

        decimal cajas = 12m * vuelta.Factor;
        decimal unidades = cajas * ida.Factor;

        unidades.ShouldNotBe(12m);
        vuelta.Factor.ShouldBe(0.083333m);
    }
}
