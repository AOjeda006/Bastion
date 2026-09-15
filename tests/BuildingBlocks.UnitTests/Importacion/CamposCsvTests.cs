using System.Globalization;
using Bastion.BuildingBlocks.Application.Importacion;
using Shouldly;

namespace Bastion.BuildingBlocks.UnitTests.Importacion;

/// <summary>Los importes y los sí o no del dialecto, y lo que parece uno sin serlo.</summary>
public sealed class CamposCsvTests
{
    [Theory]
    [InlineData("sí", true)]
    [InlineData("SÍ", true)]
    [InlineData("Si", true)]
    [InlineData("VERDADERO", true)]
    [InlineData("verdadero", true)]
    [InlineData("no", false)]
    [InlineData("NO", false)]
    [InlineData("FALSO", false)]
    [InlineData("", false)]
    public void Los_si_y_no_del_dialecto_se_leen(string campo, bool esperado)
    {
        CamposCsv.IntentarLeerSiNo(campo, out bool valor).ShouldBeTrue();
        valor.ShouldBe(esperado);
    }

    [Theory]
    [InlineData("s")]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("true")]
    [InlineData("x")]
    [InlineData(" sí")]
    public void Lo_que_parece_un_si_o_un_no_sin_serlo_no_se_lee(string campo)
    {
        CamposCsv.IntentarLeerSiNo(campo, out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData("1234", "1234")]
    [InlineData("1234,5", "1234.5")]
    [InlineData("1.234", "1234")]
    [InlineData("1.234,56", "1234.56")]
    [InlineData("12.345.678,9012", "12345678.9012")]
    [InlineData("-1,5", "-1.5")]
    [InlineData("99999999999999,9999", "99999999999999.9999")]
    [InlineData("99.999.999.999.999", "99999999999999")]
    public void Los_importes_a_la_espanola_se_leen_con_su_valor(string campo, string invariante)
    {
        decimal esperado = decimal.Parse(invariante, CultureInfo.InvariantCulture);

        CamposCsv.IntentarLeerImporte(campo, out decimal valor).ShouldBeTrue();
        valor.ShouldBe(esperado);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.5")]
    [InlineData("1,234.56")]
    [InlineData("1.23")]
    [InlineData("1.2345")]
    [InlineData("0.500")]
    [InlineData("1e3")]
    [InlineData("1.234,56 €")]
    [InlineData(" 12")]
    [InlineData("12,")]
    [InlineData(",5")]
    [InlineData("1,23456")]
    [InlineData("1,2,3")]
    [InlineData("+1")]
    [InlineData("--1")]
    [InlineData("١٢")]
    [InlineData("100000000000000")]
    public void Lo_que_no_es_un_importe_del_dialecto_no_se_adivina(string campo)
    {
        CamposCsv.IntentarLeerImporte(campo, out _).ShouldBeFalse();
    }

    /// <summary>
    /// El valor no depende de la cultura de quien ejecuta. Un lector que usara la cultura actual
    /// leería «1.234» como mil doscientos treinta y cuatro en una máquina en español y como uno con
    /// algo en una en inglés, y el test solo avisaría en la que no es la de su autor.
    /// </summary>
    [Theory]
    [InlineData("es-ES")]
    [InlineData("en-US")]
    [InlineData("de-CH")]
    [InlineData("")]
    public void El_valor_leido_no_depende_de_la_cultura_de_quien_ejecuta(string cultura)
    {
        CultureInfo antes = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultura);

            CamposCsv.IntentarLeerImporte("1.234,5", out decimal valor).ShouldBeTrue();
            valor.ShouldBe(1234.5m);
        }
        finally
        {
            CultureInfo.CurrentCulture = antes;
        }
    }
}
