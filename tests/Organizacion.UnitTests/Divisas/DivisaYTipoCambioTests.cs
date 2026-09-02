using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.Organizacion.Domain.Divisas;
using Shouldly;

namespace Bastion.Organizacion.UnitTests.Divisas;

public sealed class DivisaYTipoCambioTests
{
    private static readonly DateTimeOffset s_momento = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly s_dia = new(2026, 9, 2);
    private static readonly Guid s_euro = Guid.Parse("2f6d5f4e-0000-4000-8000-0000000000e0");
    private static readonly Guid s_dolar = Guid.Parse("2f6d5f4e-0000-4000-8000-0000000000d0");

    [Fact]
    public void Una_divisa_nace_con_su_codigo_normalizado()
    {
        var divisa = Divisa.Crear(" eur ", " Euro ", s_momento);

        divisa.Codigo.ShouldBe("EUR");
        divisa.Nombre.ShouldBe("Euro");
        divisa.CreadoEn.ShouldBe(s_momento);
    }

    [Fact]
    public void Los_decimales_salen_del_catalogo_y_no_de_una_columna()
    {
        // La propiedad es CALCULADA. Si algún día se convirtiera en columna, este test seguiría
        // pasando —leería la columna— pero el yen dejaría de valer cero en cuanto alguien lo
        // editase. Por eso además se compara contra el catálogo, que es la única autoridad.
        var euro = Divisa.Crear("EUR", "Euro", s_momento);
        var yen = Divisa.Crear("JPY", "Yen japonés", s_momento);

        euro.Decimales.ShouldBe(2);
        yen.Decimales.ShouldBe(0);

        euro.Decimales.ShouldBe(CatalogoDeDivisas.UnidadMinima("EUR"));
        yen.Decimales.ShouldBe(CatalogoDeDivisas.UnidadMinima("JPY"));
    }

    [Fact]
    public void Una_divisa_cuyo_redondeo_no_se_conoce_no_puede_darse_de_alta()
    {
        // Esto es lo que impide que la tabla y el catálogo se separen. Sin ello, alguien daría de
        // alta el dinar kuwaití, la aplicación lo dejaría elegir, y la primera cuota calculada en
        // esa divisa reventaría con un `NotSupportedException` a mitad de una factura.
        ArgumentException fallo = Should.Throw<ArgumentException>(
            () => Divisa.Crear("KWD", "Dinar kuwaití", s_momento));

        fallo.Message.ShouldContain("KWD");
    }

    [Theory]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("E1R")]
    public void Lo_que_no_tiene_forma_ISO_4217_tampoco(string codigo)
    {
        Should.Throw<ArgumentException>(() => Divisa.Crear(codigo, "Lo que sea", s_momento));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void El_codigo_y_el_nombre_de_una_divisa_son_obligatorios(string vacio)
    {
        Should.Throw<ArgumentException>(() => Divisa.Crear(vacio, "Euro", s_momento));
        Should.Throw<ArgumentException>(() => Divisa.Crear("EUR", vacio, s_momento));
    }

    [Fact]
    public void Modificar_una_divisa_cambia_el_nombre_y_no_el_codigo()
    {
        var divisa = Divisa.Crear("EUR", "Euro", s_momento);

        divisa.Modificar("Euro (zona euro)");

        divisa.Nombre.ShouldBe("Euro (zona euro)");
        divisa.Codigo.ShouldBe("EUR");
    }

    [Fact]
    public void Un_tipo_de_cambio_lleva_las_dos_divisas_y_la_direccion_escrita()
    {
        // 1 EUR = 1,08 USD. Con una sola columna, «1,08» no diría si son dólares por euro o al
        // revés, y la respuesta dependería de qué empresa lo lea.
        var cambio = TipoCambio.Crear(s_euro, s_dolar, s_dia, 1.08m, s_momento);

        cambio.DivisaOrigenId.ShouldBe(s_euro);
        cambio.DivisaDestinoId.ShouldBe(s_dolar);
        cambio.Fecha.ShouldBe(s_dia);
        cambio.Tasa.ShouldBe(1.08m);
    }

    [Fact]
    public void La_tasa_se_redondea_a_los_seis_decimales_que_publica_el_BCE()
    {
        var cambio = TipoCambio.Crear(s_euro, s_dolar, s_dia, 1.0812345678m, s_momento);

        cambio.Tasa.ShouldBe(1.081235m);
    }

    [Fact]
    public void Una_divisa_contra_si_misma_no_es_un_tipo_de_cambio()
    {
        // Vale uno por definición. Admitir la fila abre la puerta a que valga otra cosa.
        Should.Throw<ArgumentException>(() => TipoCambio.Crear(s_euro, s_euro, s_dia, 1m, s_momento));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1.08)]
    public void Una_tasa_que_no_es_mayor_que_cero_no_se_guarda(decimal tasa)
    {
        // El cero es el peligroso: no falla al guardarse y convierte cualquier importe en nada.
        Should.Throw<ArgumentOutOfRangeException>(
            () => TipoCambio.Crear(s_euro, s_dolar, s_dia, tasa, s_momento));
    }

    [Fact]
    public void Un_tipo_de_cambio_sin_divisa_no_existe()
    {
        Should.Throw<ArgumentException>(
            () => TipoCambio.Crear(Guid.Empty, s_dolar, s_dia, 1.08m, s_momento));

        Should.Throw<ArgumentException>(
            () => TipoCambio.Crear(s_euro, Guid.Empty, s_dia, 1.08m, s_momento));
    }

    [Fact]
    public void Corregir_la_tasa_no_cambia_ni_las_divisas_ni_la_fecha()
    {
        var cambio = TipoCambio.Crear(s_euro, s_dolar, s_dia, 1.08m, s_momento);

        cambio.Modificar(1.09m);

        cambio.Tasa.ShouldBe(1.09m);
        cambio.Fecha.ShouldBe(s_dia);
        cambio.DivisaOrigenId.ShouldBe(s_euro);
        cambio.DivisaDestinoId.ShouldBe(s_dolar);
    }
}
