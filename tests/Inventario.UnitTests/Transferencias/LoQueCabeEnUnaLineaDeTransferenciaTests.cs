using System.Globalization;
using Bastion.Inventario.Domain.Transferencias;
using Shouldly;

namespace Bastion.Inventario.UnitTests.Transferencias;

/// <summary>
/// Lo que cabe en una línea de transferencia: la cantidad y la cantidad en la unidad base, cada una
/// en su columna (ADR-0068 §4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Hasta el ítem 2.19 lo único que miraba el dominio era el cero</b>, como en el ajuste hasta el
/// 2.17, y lo demás lo decía la columna al guardar: siete decimales se redondeaban a otra cantidad, y
/// trece cifras enteras eran un <c>500</c>. Lo que el borde rechaza con
/// <c>transferencia-cantidad-no-valida</c> también lo rechaza el dominio, y aquí se ve la segunda
/// mitad.
/// </para>
/// <para>
/// <b>La regla mira el valor absoluto</b>, porque la línea del inverso lleva la cantidad negada. Que el
/// alta solo admita una positiva lo dice el documento, no la columna.
/// </para>
/// <para>
/// <b>Cada rechazo lleva su pareja</b>: lo que está justo en el borde entra.
/// </para>
/// </remarks>
public sealed class LoQueCabeEnUnaLineaDeTransferenciaTests
{
    private static readonly DateTimeOffset s_momento = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("0")]
    [InlineData("0.0000001")]
    [InlineData("1000000000000")]
    [InlineData("-1000000000000")]
    public void Una_cantidad_que_no_cabe_en_su_columna_no_cabe(string escrita) =>
        LineaDeTransferencia.LaCantidadCabe(Leer(escrita)).ShouldBeFalse();

    [Theory]
    [InlineData("0.0000001")]
    [InlineData("1000000000000")]
    [InlineData("1000000000000.5")]
    public void Una_cantidad_positiva_que_no_cabe_no_entra_en_el_documento(string escrita)
    {
        decimal cantidad = Leer(escrita);
        Transferencia transferencia = UnaTransferencia();

        Should.Throw<ArgumentOutOfRangeException>(() => ConLinea(transferencia, cantidad, 1m))
            .ParamName.ShouldBe("cantidadIntroducida");
        transferencia.Lineas.ShouldBeEmpty("una línea rechazada no se queda a medias en el documento");
    }

    [Theory]
    [InlineData("999999999999.999999")]
    [InlineData("-999999999999.999999")]
    [InlineData("0.000001")]
    [InlineData("-0.000001")]
    public void La_cantidad_en_el_borde_de_su_columna_cabe(string escrita) =>
        LineaDeTransferencia.LaCantidadCabe(Leer(escrita)).ShouldBeTrue();

    [Theory]
    [InlineData("999999999999.999999")]
    [InlineData("0.000001")]
    public void La_cantidad_en_el_borde_entra_en_el_documento(string escrita)
    {
        decimal cantidad = Leer(escrita);
        Transferencia transferencia = UnaTransferencia();

        ConLinea(transferencia, cantidad, 1m);
        transferencia.Lineas.ShouldHaveSingleItem().CantidadIntroducida.ShouldBe(cantidad);
    }

    /// <summary>
    /// La cantidad cabe, y lo que mueve en la unidad base no: se redondea a cero o se sale.
    /// </summary>
    /// <remarks>
    /// Una décima de una unidad que vale una millonésima redondea a cero, y las dos filas del libro
    /// no moverían nada. Y cien mil millones de cajas de doce no caben en doce cifras enteras.
    /// </remarks>
    [Theory]
    [InlineData("0.1", "0.000001")]
    [InlineData("100000000000", "12")]
    public void Lo_que_mueve_en_la_unidad_base_tampoco_se_queda_en_cero_ni_se_sale(string escrita, string factor)
    {
        decimal cantidad = Leer(escrita);
        Transferencia transferencia = UnaTransferencia();

        LineaDeTransferencia.LaCantidadCabe(cantidad).ShouldBeTrue("lo escrito cabe: lo que no cabe es la base");
        LineaDeTransferencia.SePuedeTransferir(cantidad, Leer(factor)).ShouldBeFalse();
        LineaDeTransferencia.SePuedeTransferir(-cantidad, Leer(factor)).ShouldBeFalse("la del inverso, igual");
        Should.Throw<ArgumentOutOfRangeException>(() => ConLinea(transferencia, cantidad, Leer(factor)))
            .ParamName.ShouldBe("cantidadIntroducida");
        transferencia.Lineas.ShouldBeEmpty();
    }

    /// <summary>Media millonésima redondea alejándose del cero, y 83 333 333 333 cajas de doce caben.</summary>
    [Theory]
    [InlineData("0.5", "0.000001")]
    [InlineData("83333333333", "12")]
    public void Lo_que_mueve_en_la_unidad_base_en_su_borde_entra(string escrita, string factor)
    {
        decimal cantidad = Leer(escrita);
        Transferencia transferencia = UnaTransferencia();

        LineaDeTransferencia.SePuedeTransferir(cantidad, Leer(factor)).ShouldBeTrue();
        LineaDeTransferencia.SePuedeTransferir(-cantidad, Leer(factor)).ShouldBeTrue("la del inverso, igual");
        ConLinea(transferencia, cantidad, Leer(factor));
        transferencia.Lineas.ShouldHaveSingleItem().FactorAUnidadBase.ShouldBe(Leer(factor));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void Sin_un_factor_positivo_no_se_puede_transferir(string factor) =>
        LineaDeTransferencia.SePuedeTransferir(1m, Leer(factor)).ShouldBeFalse();

    private static decimal Leer(string escrito) => decimal.Parse(escrito, CultureInfo.InvariantCulture);

    private static Transferencia UnaTransferencia() => Transferencia.Abrir(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        new DateOnly(2026, 3, 14),
        "EUR",
        s_momento);

    private static void ConLinea(Transferencia transferencia, decimal cantidad, decimal factor) =>
        transferencia.AnadirLinea(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            cantidad,
            Guid.CreateVersion7(),
            factor,
            s_momento);
}
