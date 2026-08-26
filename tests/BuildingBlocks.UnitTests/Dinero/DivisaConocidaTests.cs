using Bastion.BuildingBlocks.Domain.Dinero;
using Shouldly;

namespace Bastion.BuildingBlocks.UnitTests.Dinero;

/// <summary>
/// <c>UnidadMinima</c> lanza cuando no conoce la divisa, y eso está bien donde está: dentro, una
/// divisa sin redondeo conocido es un fallo de programación. Pero el borde necesita PREGUNTAR sin
/// que le lancen, para poder decir «divisaBase: no se conoce el redondeo» en el campo que toca en
/// vez de devolver un 500.
/// </summary>
public sealed class DivisaConocidaTests
{
    [Theory]
    [InlineData("EUR")]
    [InlineData("eur")]
    [InlineData("  EUR  ")]
    public void El_euro_se_conoce_venga_como_venga_escrito(string divisa)
    {
        Divisas.EsConocida(divisa).ShouldBeTrue();
    }

    [Theory]
    [InlineData("JPY")]
    [InlineData("USD")]
    public void Una_divisa_con_forma_valida_pero_sin_redondeo_conocido_no_se_conoce(string divisa)
    {
        // Con forma ISO correcta y todo: el problema no es cómo se escribe, es que nadie ha
        // decidido con cuántos decimales se redondea, y R6 no se puede cumplir a ojo.
        Divisas.EsConocida(divisa).ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("E1R")]
    public void Lo_que_ni_siquiera_tiene_forma_de_divisa_tampoco_se_conoce_y_no_lanza(string divisa)
    {
        // Preguntar no lanza NUNCA, ni con basura. Si lanzara con la forma y devolviera false
        // con el catálogo, el borde tendría que envolverlo en un try igualmente y la pregunta
        // no habría servido de nada.
        Should.NotThrow(() => Divisas.EsConocida(divisa)).ShouldBeFalse();
    }

    [Fact]
    public void Preguntar_por_una_divisa_conocida_y_pedir_su_unidad_minima_dicen_lo_mismo()
    {
        // Las dos puertas leen el MISMO catálogo. Si no, una diría que sí y la otra lanzaría, y
        // el borde daría un 400 amable justo antes de un 500.
        Divisas.EsConocida("EUR").ShouldBeTrue();
        Divisas.UnidadMinima("EUR").ShouldBe(2);
    }
}
