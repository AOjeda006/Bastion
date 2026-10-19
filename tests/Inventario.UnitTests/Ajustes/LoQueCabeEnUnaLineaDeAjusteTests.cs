using System.Globalization;
using Bastion.Inventario.Domain.Ajustes;
using Shouldly;

namespace Bastion.Inventario.UnitTests.Ajustes;

/// <summary>
/// Lo que cabe en una línea de ajuste: la cantidad, la cantidad en la unidad base y el coste, cada
/// uno en su columna.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hasta el ítem 2.17 lo único que miraba el dominio era el cero</b>, y lo demás lo decía la
/// columna al guardar: siete decimales se redondeaban a otra cantidad, y trece cifras enteras o un
/// coste de quince eran un <c>500</c>. Lo que el borde rechaza con <c>ajuste-cantidad-no-valida</c> y
/// <c>ajuste-coste-no-valido</c> también lo rechaza el dominio, y aquí se ve la segunda mitad.
/// </para>
/// <para>
/// <b>Cada rechazo lleva su pareja</b>: lo que está justo en el borde entra.
/// </para>
/// </remarks>
public sealed class LoQueCabeEnUnaLineaDeAjusteTests
{
    private static readonly DateTimeOffset s_momento = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("0")]
    [InlineData("0.0000001")]
    [InlineData("1000000000000")]
    [InlineData("-1000000000000")]
    public void Una_cantidad_que_no_cabe_en_su_columna_no_entra(string escrita)
    {
        decimal cantidad = Leer(escrita);
        Ajuste ajuste = UnAjuste();

        LineaDeAjuste.LaCantidadCabe(cantidad).ShouldBeFalse();
        Should.Throw<ArgumentOutOfRangeException>(() => ConLinea(ajuste, cantidad, 1m, null))
            .ParamName.ShouldBe("cantidadIntroducida");
        ajuste.Lineas.ShouldBeEmpty("una línea rechazada no se queda a medias en el documento");
    }

    [Theory]
    [InlineData("999999999999.999999")]
    [InlineData("-999999999999.999999")]
    [InlineData("0.000001")]
    [InlineData("-0.000001")]
    public void La_cantidad_en_el_borde_de_su_columna_entra(string escrita)
    {
        decimal cantidad = Leer(escrita);
        Ajuste ajuste = UnAjuste();

        LineaDeAjuste.LaCantidadCabe(cantidad).ShouldBeTrue();
        ConLinea(ajuste, cantidad, 1m, null);
        ajuste.Lineas.ShouldHaveSingleItem().CantidadIntroducida.ShouldBe(cantidad);
    }

    /// <summary>
    /// La cantidad cabe, y lo que mueve en la unidad base no: se redondea a cero o se sale.
    /// </summary>
    /// <remarks>
    /// Una décima de una unidad que vale una millonésima son diez millonésimas de nada, y redondean a
    /// cero: la fila del libro diría que movió algo y sumaría nada. Y cien mil millones de cajas de
    /// doce no caben en doce cifras enteras.
    /// </remarks>
    [Theory]
    [InlineData("0.1", "0.000001")]
    [InlineData("-0.1", "0.000001")]
    [InlineData("100000000000", "12")]
    [InlineData("-100000000000", "12")]
    public void Lo_que_mueve_en_la_unidad_base_tampoco_se_queda_en_cero_ni_se_sale(string escrita, string factor)
    {
        decimal cantidad = Leer(escrita);
        Ajuste ajuste = UnAjuste();

        LineaDeAjuste.LaCantidadCabe(cantidad).ShouldBeTrue("lo escrito cabe: lo que no cabe es la base");
        LineaDeAjuste.SePuedeAjustar(cantidad, Leer(factor)).ShouldBeFalse();
        Should.Throw<ArgumentOutOfRangeException>(() => ConLinea(ajuste, cantidad, Leer(factor), null))
            .ParamName.ShouldBe("cantidadIntroducida");
        ajuste.Lineas.ShouldBeEmpty();
    }

    /// <summary>Media millonésima redondea alejándose del cero, y 83 333 333 333 cajas de doce caben.</summary>
    [Theory]
    [InlineData("0.5", "0.000001")]
    [InlineData("-0.5", "0.000001")]
    [InlineData("83333333333", "12")]
    [InlineData("-83333333333", "12")]
    public void Lo_que_mueve_en_la_unidad_base_en_su_borde_entra(string escrita, string factor)
    {
        decimal cantidad = Leer(escrita);
        Ajuste ajuste = UnAjuste();

        LineaDeAjuste.SePuedeAjustar(cantidad, Leer(factor)).ShouldBeTrue();
        ConLinea(ajuste, cantidad, Leer(factor), null);
        ajuste.Lineas.ShouldHaveSingleItem().FactorAUnidadBase.ShouldBe(Leer(factor));
    }

    /// <summary>
    /// Un coste que, a la escala del importe, no cabe en catorce cifras enteras no entra; el que
    /// redondea justo por debajo, sí.
    /// </summary>
    [Theory]
    [InlineData("100000000000000", false)]
    [InlineData("99999999999999.99995", false)]
    [InlineData("99999999999999.99994", true)]
    public void Un_coste_que_no_cabe_en_su_columna_no_entra(string escrito, bool cabe)
    {
        decimal coste = Leer(escrito);
        Ajuste ajuste = UnAjuste();

        LineaDeAjuste.ElCosteCabe(coste).ShouldBe(cabe);

        if (cabe)
        {
            ConLinea(ajuste, 1m, 1m, coste);
            ajuste.Lineas.ShouldHaveSingleItem().CosteUnitario.ShouldBe(99999999999999.9999m);
        }
        else
        {
            Should.Throw<ArgumentException>(() => ConLinea(ajuste, 1m, 1m, coste)).ParamName.ShouldBe("costeUnitario");
            ajuste.Lineas.ShouldBeEmpty();
        }
    }

    private static decimal Leer(string escrito) => decimal.Parse(escrito, CultureInfo.InvariantCulture);

    private static Ajuste UnAjuste() => Ajuste.Abrir(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        new DateOnly(2026, 3, 14),
        "Recuento de marzo",
        "EUR",
        s_momento);

    private static void ConLinea(Ajuste ajuste, decimal cantidad, decimal factor, decimal? coste) =>
        ajuste.AnadirLinea(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            cantidad,
            Guid.CreateVersion7(),
            factor,
            cantidad < 0m ? null : coste,
            s_momento);
}
