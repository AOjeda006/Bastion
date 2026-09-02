using Bastion.BuildingBlocks.Domain.Dinero;
using Shouldly;

namespace Bastion.BuildingBlocks.UnitTests.Dinero;

public sealed class ImporteTests
{
    [Fact]
    public void De_ConDivisaVacia_Lanza() =>
        Should.Throw<ArgumentException>(() => Importe.De(1m, "   "));

    [Theory]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("EU1")]
    public void De_ConDivisaQueNoEsUnCodigoIso4217_Lanza(string divisa) =>
        Should.Throw<ArgumentException>(() => Importe.De(1m, divisa));

    [Fact]
    public void De_ConDivisaEnMinusculas_LaNormalizaAMayusculas() =>
        Importe.De(1m, "eur").Divisa.ShouldBe("EUR");

    // La escala de un importe es numeric(18,4) (R6). El tipo la impone al construirse: lo que
    // no cabe en cuatro decimales no se guarda "tal cual" para redondearse luego en el motor
    // de la base de datos, donde el modo de redondeo ya no es el nuestro.
    [Fact]
    public void De_ConMasDeCuatroDecimales_ReduceALaEscalaDeImporte() =>
        Importe.De(0.00005m, "EUR").Cantidad.ShouldBe(0.0001m);

    // AwayFromZero de verdad: en negativo se aleja del cero, no "hacia abajo". Un abono de
    // -0,00005 vale -0,0001, no -0,0000.
    [Fact]
    public void De_ConCantidadNegativaEnElPuntoMedio_SeAlejaDelCero() =>
        Importe.De(-0.00005m, "EUR").Cantidad.ShouldBe(-0.0001m);

    [Fact]
    public void Igualdad_ConLaMismaCantidadYDivisa_SonElMismoValor() =>
        Importe.De(12.5m, "EUR").ShouldBe(Importe.De(12.5000m, "EUR"));

    [Fact]
    public void Igualdad_ConDistintaDivisa_NoSonElMismoValor() =>
        Importe.De(12.5m, "EUR").ShouldNotBe(Importe.De(12.5m, "USD"));

    // Sumar NO redondea: dos importes ya están en escala 4, y su suma también. Si la suma
    // redondease, cada acumulación metería un error que R6 quiere concentrar en un solo sitio.
    [Fact]
    public void Suma_ConLaMismaDivisa_NoIntroduceRedondeo() =>
        (Importe.De(0.0001m, "EUR") + Importe.De(0.0002m, "EUR")).ShouldBe(Importe.De(0.0003m, "EUR"));

    // Operar entre divisas distintas LANZA: no convierte ni deja pasar. Una conversión
    // necesita un tipo de cambio con fecha, y adivinarlo aquí es inventar dinero.
    [Fact]
    public void Suma_ConDivisasDistintas_Lanza() =>
        Should.Throw<InvalidOperationException>(() => Importe.De(1m, "EUR") + Importe.De(1m, "USD"));

    // El caso que separa la regla escrita del valor por omisión de .NET.
    // 1,25 x 10% = 0,125, que cae JUSTO en el punto medio de la unidad mínima del euro.
    // MidpointRounding.AwayFromZero -> 0,13. El de .NET (ToEven, "del banquero") -> 0,12.
    [Fact]
    public void Cuota_EnElPuntoMedioDeLaUnidadMinima_RedondeaAlejandoseDelCero()
    {
        Importe cuota = Importe.De(1.25m, "EUR").Cuota(0.10m);

        cuota.Cantidad.ShouldBe(0.13m);
        Math.Round(0.125m, 2).ShouldBe(0.12m); // lo que habría dado el modo por omisión
    }

    // Sin unidad mínima conocida no se redondea "a dos por si acaso": un valor por omisión
    // aquí esconde el hueco. Se lanza, y la divisa se añade cuando llegue con su test.
    //
    // El ejemplo era USD hasta el 0.15, cuando el dólar entró en el catálogo con su caso dorado.
    // Ahora es el dinar kuwaití, que sigue fuera y además redondea a TRES decimales: si algún día
    // entra suponiéndole dos, el caso dorado que habrá que escribir es el que lo delata.
    [Fact]
    public void Cuota_ConUnaDivisaSinUnidadMinimaConocida_Lanza() =>
        Should.Throw<NotSupportedException>(() => Importe.De(100m, "KWD").Cuota(0.21m));

    [Fact]
    public void Cero_EsElNeutroDeLaSuma() =>
        (Importe.Cero("EUR") + Importe.De(3.5m, "EUR")).ShouldBe(Importe.De(3.5m, "EUR"));
}
