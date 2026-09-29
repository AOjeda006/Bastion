using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.UnitTests.Valoraciones;
using Shouldly;

namespace Bastion.Inventario.UnitTests.Ajustes;

/// <summary>
/// La divisa va en la cabecera del ajuste y en cada fila del libro, y el coste solo lo lleva una
/// línea que sube (ADR-0046 §7).
/// </summary>
/// <remarks>
/// <b>Lo que el borde rechaza con <c>ajuste-coste-no-valido</c> también lo rechaza el dominio</b>,
/// y aquí se ve la segunda mitad. Si solo lo rechazara el borde, cualquier otro camino que abriera
/// un ajuste podría escribir una salida con un coste que nadie usaría.
/// </remarks>
public sealed class ElCosteYLaDivisaDelAjusteTests
{
    private static readonly DateTimeOffset s_momento = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Una línea que baja no lleva coste, y ninguna lo lleva negativo.</summary>
    [Fact]
    public void Una_linea_que_baja_no_lleva_coste_y_ninguna_lo_lleva_negativo()
    {
        Ajuste ajuste = UnAjuste();

        Should.Throw<ArgumentException>(() => ConLinea(ajuste, -2m, 1.50m)).ParamName.ShouldBe("costeUnitario");
        Should.Throw<ArgumentException>(() => ConLinea(ajuste, 2m, -1.50m)).ParamName.ShouldBe("costeUnitario");
        ajuste.Lineas.ShouldBeEmpty("una línea rechazada no se queda a medias en el documento");

        ConLinea(ajuste, -2m, null);
        ConLinea(ajuste, 2m, null);
        ConLinea(ajuste, 2m, 0m);

        ajuste.Lineas.Select(linea => linea.CosteUnitario).ShouldBe(
            [null, null, 0m],
            "sin coste valen las dos, y un coste de cero es una muestra o un regalo");
    }

    /// <summary>El coste se guarda a la escala del importe, redondeado alejándose del cero (R6).</summary>
    [Fact]
    public void El_coste_se_guarda_a_la_escala_del_importe()
    {
        Ajuste ajuste = UnAjuste();

        ConLinea(ajuste, 1m, 1.23465m);

        ajuste.Lineas.ShouldHaveSingleItem().CosteUnitario.ShouldBe(
            1.2347m, "el redondeo del banquero, el de .NET por omisión, daría 1,2346");
    }

    /// <summary>
    /// La divisa se normaliza al abrir, y cada fila del libro la lleva: con el coste la entrada, y
    /// sin él la salida.
    /// </summary>
    [Fact]
    public void Cada_fila_del_libro_lleva_la_divisa_del_ajuste_y_la_salida_ninguna_coste()
    {
        Ajuste ajuste = UnAjuste("usd");
        ConLinea(ajuste, 3m, 2.50m);
        ConLinea(ajuste, -1m, null);

        ajuste.Divisa.ShouldBe("USD");

        IReadOnlyList<MovimientoStock> filas = ajuste.Confirmar(1, Confirmado(ajuste), LaValoracion.DesdeCero(ajuste), LotesYSeriesResueltos.Ninguno, s_momento);

        filas.Select(fila => fila.Divisa).ShouldBe(["USD", "USD"]);
        filas[0].CosteUnitario.ShouldBe(Importe.De(2.50m, "USD"));
        filas[1].CosteUnitario.ShouldBeNull();
    }

    /// <summary>Una divisa que no tiene forma de ISO 4217 no abre el documento.</summary>
    [Fact]
    public void Una_divisa_sin_forma_no_abre_el_ajuste()
    {
        Should.Throw<ArgumentException>(() => UnAjuste("euro"));
    }

    /// <summary>El libro no admite un coste en otra divisa que la de su fila.</summary>
    [Fact]
    public void Una_fila_del_libro_no_mezcla_divisas()
    {
        Should.Throw<ArgumentException>(() => MovimientoStock.Registrar(
            Guid.CreateVersion7(),
            new DateOnly(2026, 3, 14),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            null,
            null,
            1m,
            Guid.CreateVersion7(),
            1m,
            "EUR",
            Importe.De(2.50m, "USD"),
            Importe.De(2.50m, "EUR"),
            PrecioUnitario.De(2.50m, "EUR"),
            TipoDeDocumentoOrigen.Ajuste,
            Guid.CreateVersion7(),
            s_momento)).ParamName.ShouldBe("costeUnitario");
    }

    private static Ajuste UnAjuste(string divisa = "EUR") => Ajuste.Abrir(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        new DateOnly(2026, 3, 14),
        "Recuento de marzo",
        divisa,
        s_momento);

    private static void ConLinea(Ajuste ajuste, decimal cantidad, decimal? coste) =>
        ajuste.AnadirLinea(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            cantidad,
            Guid.CreateVersion7(),
            1m,
            coste,
            s_momento);

    private static AjusteConfirmado Confirmado(Ajuste ajuste) => new(
        ajuste.Id, ajuste.EmpresaId, ajuste.AlmacenId, ajuste.FechaDeOperacion, ajuste.Lineas.Count);
}
