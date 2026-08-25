using Bastion.BuildingBlocks.Domain.Dinero;
using Shouldly;

namespace Bastion.BuildingBlocks.UnitTests.Dinero;

// R6 pide DOS escalas, no una: numeric(18,4) para importes y numeric(18,6) para precios
// unitarios. Son tipos distintos justamente para que el compilador impida confundirlos, y
// para que la reducción de escala tenga UN sitio con nombre: `PrecioUnitario.Por`.
public sealed class PrecioUnitarioTests
{
    [Fact]
    public void De_ConMasDeSeisDecimales_ReduceALaEscalaDePrecioUnitario() =>
        PrecioUnitario.De(0.1234565m, "EUR").Cantidad.ShouldBe(0.123457m);

    [Fact]
    public void De_ConDivisaQueNoEsUnCodigoIso4217_Lanza() =>
        Should.Throw<ArgumentException>(() => PrecioUnitario.De(1m, "€"));

    // Multiplicar un unitario por una cantidad NO devuelve otro unitario: devuelve un importe,
    // y por tanto baja de escala 6 a escala 4. Ese salto ocurre aquí y en ningún otro sitio.
    [Fact]
    public void Por_DevuelveUnImporteEnLaEscalaDeImporte()
    {
        Importe linea = PrecioUnitario.De(0.1234565m, "EUR").Por(3m);

        // 0,123457 x 3 = 0,370371 -> escala 4 -> 0,3704
        linea.Cantidad.ShouldBe(0.3704m);
        linea.Divisa.ShouldBe("EUR");
    }

    // La reducción es UN redondeo, sobre el producto exacto: no se redondea el unitario a 4
    // y luego se multiplica, porque eso multiplicaría también el error.
    [Fact]
    public void Por_RedondeaElProductoExactoYNoElFactor()
    {
        Importe linea = PrecioUnitario.De(0.000004m, "EUR").Por(1000m);

        // Si se hubiera reducido el unitario a escala 4 primero (0,0000) la línea valdría 0.
        linea.Cantidad.ShouldBe(0.004m);
    }

    [Fact]
    public void Por_ConCantidadNegativa_ConservaElSigno() =>
        PrecioUnitario.De(2.5m, "EUR").Por(-2m).Cantidad.ShouldBe(-5m);
}
