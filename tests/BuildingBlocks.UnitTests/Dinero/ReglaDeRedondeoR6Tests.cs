using Bastion.BuildingBlocks.Domain.Dinero;
using Shouldly;

namespace Bastion.BuildingBlocks.UnitTests.Dinero;

// Caso dorado de R6: "el redondeo se aplica por base imponible y tipo impositivo, no línea a
// línea ni al total".
//
// Un test cuyas tres estrategias coinciden no prueba nada, así que el caso está construido
// para que las tres den un número DISTINTO. Factura de ejemplo, todo en euros:
//
//   grupo | tipo |    líneas             | base    | cuota exacta | cuota R6
//   ------+------+-----------------------+---------+--------------+---------
//     A   | 21 % | 3 x (4,008 x 3 uds)   | 36,0720 |    7,5751200 |    7,58
//     B   | 10 % | 3 x (4,030 x 2 uds)   | 24,1800 |    2,4180000 |    2,42
//
//   R6 (una vez por par base/tipo) .............. 7,58 + 2,42 = 10,00   <-- la regla
//   línea a línea (redondear cada línea) ........ 7,59 + 2,43 = 10,02
//   al final (acumular exacto y redondear una vez)  9,99312   =  9,99
//
// Tres euros con dos decimales, tres respuestas distintas. Elegir mal no es un matiz: es la
// diferencia entre cuadrar con la AEAT y no cuadrar.
//
// El SERVICIO de cálculo de impuestos no vive aquí — §12 lo pone en su propio módulo de
// dominio. Lo que el bloque común aporta es la primitiva `Importe.Cuota`, que redondea UNA
// vez, y este caso dorado como referencia que ese servicio tendrá que reproducir.
public sealed class ReglaDeRedondeoR6Tests
{
    private const string Euro = "EUR";

    private static readonly LineaDeEjemplo[] s_factura =
    [
        new(PrecioUnitario.De(4.008m, Euro), 3m, 0.21m),
        new(PrecioUnitario.De(4.008m, Euro), 3m, 0.21m),
        new(PrecioUnitario.De(4.008m, Euro), 3m, 0.21m),
        new(PrecioUnitario.De(4.030m, Euro), 2m, 0.10m),
        new(PrecioUnitario.De(4.030m, Euro), 2m, 0.10m),
        new(PrecioUnitario.De(4.030m, Euro), 2m, 0.10m),
    ];

    [Fact]
    public void LasBasesImponiblesPorTipoSonLasDelCasoDorado()
    {
        BaseImponibleDe(0.21m).ShouldBe(Importe.De(36.0720m, Euro));
        BaseImponibleDe(0.10m).ShouldBe(Importe.De(24.1800m, Euro));
    }

    [Fact]
    public void SegunR6_SeRedondeaUnaVezPorParDeBaseYTipo()
    {
        BaseImponibleDe(0.21m).Cuota(0.21m).ShouldBe(Importe.De(7.58m, Euro));
        BaseImponibleDe(0.10m).Cuota(0.10m).ShouldBe(Importe.De(2.42m, Euro));

        CuotaSegunR6().ShouldBe(Importe.De(10.00m, Euro));
    }

    // El test que da sentido al anterior: si las tres estrategias dieran lo mismo, no habría
    // regla que documentar ni que probar.
    [Fact]
    public void LasTresEstrategiasDanResultadosDistintosYLaBuenaEsLaDeR6()
    {
        Importe segunR6 = CuotaSegunR6();
        Importe lineaALinea = CuotaLineaALinea();
        Importe alFinal = CuotaRedondeadaSoloAlFinal();

        segunR6.Cantidad.ShouldBe(10.00m);
        lineaALinea.Cantidad.ShouldBe(10.02m);
        alFinal.Cantidad.ShouldBe(9.99m);

        segunR6.ShouldNotBe(lineaALinea);
        segunR6.ShouldNotBe(alFinal);
        lineaALinea.ShouldNotBe(alFinal);
    }

    private static Importe BaseImponibleDe(decimal tipo) =>
        s_factura.Where(linea => linea.Tipo == tipo)
                 .Select(linea => linea.BaseImponible)
                 .Aggregate(Importe.Cero(Euro), (acumulado, siguiente) => acumulado + siguiente);

    // La regla: agrupar por tipo, sumar las bases sin redondear, y redondear UNA vez por grupo.
    private static Importe CuotaSegunR6() =>
        s_factura.Select(linea => linea.Tipo)
                 .Distinct()
                 .Select(tipo => BaseImponibleDe(tipo).Cuota(tipo))
                 .Aggregate(Importe.Cero(Euro), (acumulado, siguiente) => acumulado + siguiente);

    // Estrategia descartada 1: redondear la cuota de cada línea. Acumula el error hacia arriba.
    private static Importe CuotaLineaALinea() =>
        s_factura.Select(linea => linea.BaseImponible.Cuota(linea.Tipo))
                 .Aggregate(Importe.Cero(Euro), (acumulado, siguiente) => acumulado + siguiente);

    // Estrategia descartada 2: acumular el producto exacto de todas las líneas y redondear una
    // sola vez al final. Se escribe con decimales crudos a propósito: el dominio no ofrece esta
    // operación, precisamente porque no es la regla.
    private static Importe CuotaRedondeadaSoloAlFinal()
    {
        decimal exacto = s_factura.Sum(linea => linea.BaseImponible.Cantidad * linea.Tipo);

        return Importe.De(Math.Round(exacto, 2, MidpointRounding.AwayFromZero), Euro);
    }

    private sealed record LineaDeEjemplo(PrecioUnitario Precio, decimal Cantidad, decimal Tipo)
    {
        public Importe BaseImponible => Precio.Por(Cantidad);
    }
}
