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
    // Las que el catálogo conoce hoy. Escritas aquí y no leídas del propio catálogo: una lista
    // sacada de lo que se está comprobando siempre coincide consigo misma, y no notaría nada.
    private static readonly string[] s_delCatalogo = ["EUR", "USD", "GBP", "CHF", "JPY"];

    [Theory]
    [InlineData("EUR")]
    [InlineData("eur")]
    [InlineData("  EUR  ")]
    public void El_euro_se_conoce_venga_como_venga_escrito(string divisa)
    {
        CatalogoDeDivisas.EsConocida(divisa).ShouldBeTrue();
    }

    [Theory]
    [InlineData("KWD")]
    [InlineData("BHD")]
    [InlineData("XYZ")]
    public void Una_divisa_con_forma_valida_pero_sin_redondeo_conocido_no_se_conoce(string divisa)
    {
        // Con forma ISO correcta y todo: el problema no es cómo se escribe, es que nadie ha
        // decidido con cuántos decimales se redondea, y R6 no se puede cumplir a ojo.
        //
        // Hasta el 0.15 los ejemplos eran JPY y USD; entraron en el catálogo con su caso dorado,
        // así que este caso se mudó a dos que siguen fuera. El dinar kuwaití y el bareiní no son
        // un ejemplo cualquiera: redondean a TRES decimales, así que el día que alguien los añada
        // suponiendo dos, el caso dorado que tendrá que escribir aquí es justo el que lo delata.
        CatalogoDeDivisas.EsConocida(divisa).ShouldBeFalse();
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
        Should.NotThrow(() => CatalogoDeDivisas.EsConocida(divisa)).ShouldBeFalse();
    }

    [Fact]
    public void Preguntar_por_una_divisa_conocida_y_pedir_su_unidad_minima_dicen_lo_mismo()
    {
        // Las dos puertas leen el MISMO catálogo. Si no, una diría que sí y la otra lanzaría, y
        // el borde daría un 400 amable justo antes de un 500.
        CatalogoDeDivisas.EsConocida("EUR").ShouldBeTrue();
        CatalogoDeDivisas.UnidadMinima("EUR").ShouldBe(2);
    }

    [Theory]
    [InlineData("EUR", 2)]
    [InlineData("USD", 2)]
    [InlineData("GBP", 2)]
    [InlineData("CHF", 2)]
    [InlineData("JPY", 0)]
    public void Cada_divisa_del_catalogo_trae_su_caso_dorado(string divisa, int decimales)
    {
        // El caso dorado de cada una, que es el precio de entrar en el catálogo. El yen es el que
        // hay que mirar: es el que impide que esto se «simplifique» a un 2 constante, y el que
        // demuestra que la pregunta «¿cuántos decimales?» tiene respuestas distintas de verdad.
        CatalogoDeDivisas.UnidadMinima(divisa).ShouldBe(decimales);
    }

    [Fact]
    public void El_catalogo_no_esta_vacio_ni_es_de_una_sola_divisa()
    {
        // La afirmación de conjunto no vacío, aplicada al catálogo: la teoría de arriba pasaría
        // igual de verde con la mitad de las filas borradas, porque cada caso se comprueba solo.
        // Este recuento es lo único que nota que el catálogo ha encogido.
        int conocidas = s_delCatalogo.Count(CatalogoDeDivisas.EsConocida);

        conocidas.ShouldBe(5);
    }
}
