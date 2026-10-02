using Bastion.Catalogo.Domain.Catalogo;
using Shouldly;

namespace Bastion.Catalogo.UnitTests.Catalogo;

/// <summary>
/// El código de barras de un artículo: su nivel, y cuántas unidades base lleva cada nivel (ADR-0051 §3).
/// </summary>
/// <remarks>
/// La base vale una unidad, y la caja y el palé, de dos en adelante. Que el motor lo guarde también
/// lo prueba la migración contra PostgreSQL; aquí se prueba que no se puede construir otra cosa.
/// </remarks>
public sealed class ElCodigoDeBarrasTests
{
    private static readonly Guid s_empresa = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid s_articulo = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Gtin s_gtin = Gtin.De("4006381333931");
    private static readonly DateTimeOffset s_momento = new(2026, 10, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void El_enumerado_tiene_exactamente_los_tres_niveles()
    {
        // Un cuarto nivel —la capa del palé, el expositor— cambia el CHECK, la pantalla y los dos
        // diccionarios. Aquí se ve al compilar la suite.
        Enum.GetNames<NivelDeGtin>().ShouldBe(["Base", "Caja", "Palet"], Case.Sensitive);
    }

    [Fact]
    public void Nace_con_lo_que_se_le_da_y_con_su_propio_identificador()
    {
        var caja = CodigoBarras.Nuevo(s_empresa, s_articulo, s_gtin, NivelDeGtin.Caja, 12, s_momento);

        caja.Id.ShouldNotBe(Guid.Empty);
        caja.Id.Version.ShouldBe(7, "la clave es un uuid v7, y el GTIN no es clave de nada");
        caja.EmpresaId.ShouldBe(s_empresa);
        caja.ArticuloId.ShouldBe(s_articulo);
        caja.Gtin.ShouldBe(s_gtin);
        caja.Nivel.ShouldBe(NivelDeGtin.Caja);
        caja.Unidades.ShouldBe(12);
        caja.CreadoEn.ShouldBe(s_momento);
    }

    [Theory]
    [InlineData(NivelDeGtin.Base, 1)]
    [InlineData(NivelDeGtin.Caja, 2)]
    [InlineData(NivelDeGtin.Caja, 6)]
    [InlineData(NivelDeGtin.Palet, 2)]
    [InlineData(NivelDeGtin.Palet, 960)]
    public void La_base_lleva_una_unidad_y_la_caja_y_el_palet_dos_o_mas(NivelDeGtin nivel, int unidades)
    {
        CodigoBarras.Nuevo(s_empresa, s_articulo, s_gtin, nivel, unidades, s_momento).Unidades.ShouldBe(unidades);
    }

    [Theory]
    [InlineData(NivelDeGtin.Base, 0)]
    [InlineData(NivelDeGtin.Base, 2)]
    [InlineData(NivelDeGtin.Caja, 1)]
    [InlineData(NivelDeGtin.Caja, 0)]
    [InlineData(NivelDeGtin.Caja, -6)]
    [InlineData(NivelDeGtin.Palet, 1)]
    public void Unas_unidades_que_no_son_las_de_su_nivel_lanzan(NivelDeGtin nivel, int unidades)
    {
        // Una caja de una unidad es la unidad con otro código: o es la base, o está mal tecleada.
        Should.Throw<ArgumentOutOfRangeException>(
            () => CodigoBarras.Nuevo(s_empresa, s_articulo, s_gtin, nivel, unidades, s_momento));
    }

    [Fact]
    public void Sin_empresa_sin_articulo_o_sin_GTIN_lanza()
    {
        Should.Throw<ArgumentException>(
            () => CodigoBarras.Nuevo(Guid.Empty, s_articulo, s_gtin, NivelDeGtin.Base, 1, s_momento));
        Should.Throw<ArgumentException>(
            () => CodigoBarras.Nuevo(s_empresa, Guid.Empty, s_gtin, NivelDeGtin.Base, 1, s_momento));
        Should.Throw<ArgumentNullException>(
            () => CodigoBarras.Nuevo(s_empresa, s_articulo, null!, NivelDeGtin.Base, 1, s_momento));
    }

    [Theory]
    [InlineData(99)]
    [InlineData(-1)]
    public void Un_nivel_inventado_lanza_por_el_nivel_aunque_sus_unidades_valgan_para_una_caja(int nivel)
    {
        // Con dos unidades, la regla de las unidades lo dejaría pasar: lo que lanza es el nivel.
        Should.Throw<ArgumentOutOfRangeException>(
                () => CodigoBarras.Nuevo(s_empresa, s_articulo, s_gtin, (NivelDeGtin)nivel, 2, s_momento))
            .ParamName.ShouldBe("nivel");
    }
}
