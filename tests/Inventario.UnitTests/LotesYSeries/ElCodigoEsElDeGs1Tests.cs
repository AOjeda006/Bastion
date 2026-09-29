using Bastion.Inventario.Domain.LotesYSeries;
using Shouldly;

namespace Bastion.Inventario.UnitTests.LotesYSeries;

/// <summary>
/// El código de un lote o de un número de serie es el de la etiqueta: de 1 a 20 caracteres del
/// conjunto 82 de GS1, con los extremos recortados y la caja intacta (ADR-0048 §2).
/// </summary>
/// <remarks>
/// <para>
/// <b>El conjunto va escrito aquí a mano, y no se saca del código que se prueba.</b> Es la figura
/// 7.11-1 de las <i>GS1 General Specifications</i>, y es lo que admiten el AI 10 y el AI 21. Si se
/// derivara de <see cref="CodigoGs1"/>, un carácter de más o de menos en el código saldría también en
/// la lista, y el caso se pondría verde contra lo mismo que tenía que vigilar.
/// </para>
/// <para>
/// <b>Y se recorre todo el ASCII imprimible en los dos sentidos</b>: lo que está en el conjunto entra
/// y lo que no está no entra. Contar cuántos entran y cuántos no es lo que impide que un recorrido
/// vacío salga verde.
/// </para>
/// </remarks>
public sealed class ElCodigoEsElDeGs1Tests
{
    /// <summary>El conjunto 82, carácter a carácter, en el orden de la tabla ASCII.</summary>
    private const string ConjuntoOchentaYDos =
        "!\"%&'()*+,-./0123456789:;<=>?ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz";

    [Fact]
    public void El_conjunto_escrito_aqui_tiene_ochenta_y_dos_caracteres_distintos()
    {
        ConjuntoOchentaYDos.Length.ShouldBe(82, "el nombre del conjunto es su tamaño");
        ConjuntoOchentaYDos.Distinct().Count().ShouldBe(82, "un carácter repetido haría de 81 uno de 82");
    }

    [Fact]
    public void Cada_caracter_ascii_imprimible_entra_si_y_solo_si_es_del_conjunto()
    {
        List<char> entran = [];
        List<char> noEntran = [];

        // Del 0x21 al 0x7E: el espacio (0x20) es un extremo que se recorta, y tiene su caso.
        for (char caracter = '!'; caracter <= '~'; caracter++)
        {
            string? leido = CodigoGs1.Normalizar(caracter.ToString());

            if (leido is null)
            {
                noEntran.Add(caracter);
            }
            else
            {
                leido.ShouldBe(caracter.ToString(), "un carácter válido se queda como está");
                entran.Add(caracter);
            }
        }

        new string([.. entran]).ShouldBe(ConjuntoOchentaYDos);
        new string([.. noEntran]).ShouldBe("#$@[\\]^`{|}~", "los doce imprimibles que GS1 deja fuera");
    }

    [Theory]
    [InlineData("LOTE-ñ")]
    [InlineData("é")]
    [InlineData("€12")]
    [InlineData("Ａ1")]
    public void Nada_fuera_del_ascii_entra(string codigo) =>
        CodigoGs1.Normalizar(codigo).ShouldBeNull(
            "el conjunto 82 es ASCII: una letra con tilde no se puede codificar en la etiqueta");

    [Fact]
    public void Tiene_de_uno_a_veinte_caracteres()
    {
        CodigoGs1.Normalizar("7").ShouldBe("7");
        CodigoGs1.Normalizar(new string('A', 20)).ShouldBe(new string('A', 20));

        CodigoGs1.Normalizar(new string('A', 21)).ShouldBeNull("el AI 10 y el AI 21 son X..20");
        CodigoGs1.Normalizar(string.Empty).ShouldBeNull();
        CodigoGs1.Normalizar("   ").ShouldBeNull("recortado, no queda nada");
        CodigoGs1.Normalizar(null).ShouldBeNull();
    }

    [Fact]
    public void Se_recortan_los_extremos_y_nada_mas()
    {
        CodigoGs1.Normalizar("  L-01 ").ShouldBe("L-01");

        // Veinte caracteres con espacios alrededor caben: el largo se mide ya recortado.
        CodigoGs1.Normalizar(" " + new string('9', 20) + " ").ShouldBe(new string('9', 20));

        CodigoGs1.Normalizar("L 01").ShouldBeNull(
            "el espacio no es del conjunto 82, así que uno de dentro se rechaza y no se quita");
    }

    [Fact]
    public void La_caja_se_conserva()
    {
        string? minuscula = CodigoGs1.Normalizar("a1");
        string? mayuscula = CodigoGs1.Normalizar("A1");

        minuscula.ShouldBe("a1");
        mayuscula.ShouldBe("A1");

        minuscula.ShouldNotBe(
            mayuscula,
            "GS1 distingue la caja: a1 y A1 son dos lotes del proveedor, y juntarlos mezclaría su " +
            "trazabilidad");
    }
}
