using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.Inventario.Domain.Valoraciones;
using Shouldly;

namespace Bastion.Inventario.UnitTests.Valoraciones;

/// <summary>
/// Los casos dorados del precio medio ponderado, con sus cifras escritas (ADR-0046 §5).
/// </summary>
/// <remarks>
/// <para>
/// <b>Cada paso es un documento</b>, salvo en los dos casos que miran lo que pasa dentro de uno. El
/// saldo que sigue es el de antes más lo que la fila dice, que es lo que la sentencia que suma hace
/// en la tabla.
/// </para>
/// <para>
/// <b>Las cifras son fronteras y no muestras.</b> Un 10 a 2 € seguido de una salida de 5 sale igual
/// congelando el precio de antes o el de después, con tope o sin él, y redondeando en cualquier
/// sitio. Lo que distingue esas decisiones es un tercio: 1000 € en 300 unidades, donde el precio
/// medio no es exacto y el redondeo se nota en la última cifra. Y donde un caso depende de una cifra
/// que el redondeo decide, la cifra lleva al lado su testigo: la otra cuenta que habría salido.
/// </para>
/// </remarks>
public sealed class LosCasosDoradosDelPrecioMedioTests
{
    private static readonly ClaveDeValoracion s_clave = new(
        Guid.Parse("0197f000-0000-7000-8000-000000000a01"), Guid.Parse("0197f000-0000-7000-8000-000000000a02"));

    private static readonly ClaveDeValoracion s_otra = new(
        Guid.Parse("0197f000-0000-7000-8000-000000000a03"), Guid.Parse("0197f000-0000-7000-8000-000000000a02"));

    private static readonly ElPrecioMedioPonderado s_valoracion = new();

    /// <summary>La secuencia del criterio, fila a fila.</summary>
    /// <remarks>
    /// Es la que nombra el criterio del 2.8: la entrada que cambia el medio (5 a 3,50 € lo lleva de 2
    /// a 2,50), la salida posterior que ya no lo mueve, la entrada sin coste que toma el vigente y
    /// la salida que vacía la clave.
    /// </remarks>
    [Fact]
    public void La_secuencia_del_criterio_da_sus_cifras_fila_a_fila()
    {
        var saldo = SaldoValorado.Vacio("EUR");

        (LineaValorada fila, saldo) = Mover(saldo, 10m, coste: 2m);
        fila.ShouldBe(Fila(20m, 2m));

        (fila, saldo) = Mover(saldo, 5m, coste: 3.50m);
        fila.ShouldBe(Fila(17.5m, 2.5m));

        (fila, saldo) = Mover(saldo, -4m);
        fila.ShouldBe(Fila(-10m, 2.5m));
        saldo.PrecioMedio.ShouldBe(Precio(2.5m), "la salida posterior ya no mueve el precio medio");

        (fila, saldo) = Mover(saldo, 2m);
        fila.ShouldBe(Fila(5m, 2.5m), "una entrada sin coste toma el precio medio vigente");

        (fila, saldo) = Mover(saldo, -13m);
        fila.ShouldBe(Fila(-32.5m, 2.5m));
        saldo.ShouldBe(SaldoValorado.Vacio("EUR"));
    }

    /// <summary>
    /// La salida se valora con el precio de antes, y el que se deduce después puede cambiar en la
    /// sexta decimal.
    /// </summary>
    /// <remarks>
    /// <b>Esta es la fila que distingue congelar el de antes de congelar el de después</b>: 1000 €
    /// en 300 unidades es 3,333333, y lo que queda tras restar 333,3333 es 666,6667 en 200, que es
    /// 3,333334. Una salida que congelara el de después diría 3,333334 en su fila, y aquí sale rojo.
    /// </remarks>
    [Fact]
    public void Sacar_un_tercio_congela_el_precio_de_antes_y_el_que_se_deduce_cambia_en_la_sexta_decimal()
    {
        SaldoValorado saldo = new(300m, Importe.De(1000m, "EUR"));

        (LineaValorada fila, saldo) = Mover(saldo, -100m);
        fila.ShouldBe(Fila(-333.3333m, 3.333333m));
        saldo.PrecioMedio.ShouldBe(Precio(3.333334m));

        (fila, saldo) = Mover(saldo, -200m);
        fila.ShouldBe(Fila(-666.6667m, 3.333334m));
        saldo.ShouldBe(SaldoValorado.Vacio("EUR"));
    }

    /// <summary>Vaciar la clave de golpe se lleva todo el valor, y no lo que dice el redondeo.</summary>
    [Fact]
    public void Vaciar_la_clave_de_golpe_se_lleva_todo_el_valor_y_no_lo_que_dice_el_redondeo()
    {
        SaldoValorado saldo = new(300m, Importe.De(1000m, "EUR"));

        saldo.PrecioMedio!.Por(300m).ShouldBe(
            Importe.De(999.9999m, "EUR"),
            "si el precio por la cantidad diera ya 1000, el caso de abajo saldría verde sin que " +
            "la regla de vaciar la clave pintara nada");

        (LineaValorada fila, saldo) = Mover(saldo, -300m);
        fila.ShouldBe(Fila(-1000m, 3.333333m));
        saldo.ShouldBe(SaldoValorado.Vacio("EUR"));
    }

    /// <summary>Casi vaciar la clave resta como mucho lo que hay.</summary>
    /// <remarks>
    /// El precio medio redondeado hacia arriba (3,333334) por una cantidad que deja una millonésima
    /// da una diezmilésima más que el valor. El tope la corta, y la clave se queda con la millonésima
    /// y valor cero, que la tabla admite: con cantidad, el valor puede ser cero.
    /// </remarks>
    [Fact]
    public void Casi_vaciar_la_clave_resta_como_mucho_lo_que_hay()
    {
        SaldoValorado saldo = new(200m, Importe.De(666.6667m, "EUR"));

        saldo.PrecioMedio!.Por(199.999999m).ShouldBe(
            Importe.De(666.6668m, "EUR"),
            "si el precio por la cantidad no pasara del valor, el tope no tendría nada que cortar");

        (LineaValorada fila, saldo) = Mover(saldo, -199.999999m);
        fila.ShouldBe(Fila(-666.6667m, 3.333334m));
        saldo.Cantidad.ShouldBe(0.000001m);
        saldo.Valor.ShouldBe(Importe.Cero("EUR"));
        saldo.PrecioMedio.ShouldBe(Precio(0m));
    }

    /// <summary>Una clave de antes del 2.8 tiene cantidad y valor cero: su precio medio es cero.</summary>
    /// <remarks>
    /// Es lo que la migración deja (ADR-0046 §8). Una entrada sin coste se valora a cero en vez de
    /// rechazarse, y la primera entrada con coste empieza a darle precio.
    /// </remarks>
    [Fact]
    public void Una_entrada_sin_coste_en_una_clave_de_antes_del_2_8_se_valora_a_cero()
    {
        SaldoValorado saldo = new(10m, Importe.Cero("EUR"));

        (LineaValorada fila, saldo) = Mover(saldo, 5m);
        fila.ShouldBe(Fila(0m, 0m));

        (fila, _) = Mover(saldo, 5m, coste: 3m);
        fila.ShouldBe(Fila(15m, 0.75m));
    }

    /// <summary>Dentro de un documento, primero lo que sube y después lo que baja.</summary>
    /// <remarks>
    /// La salida va primero en las líneas y se valora después de la entrada. En el orden de las
    /// líneas encontraría la clave vacía, se valoraría a cero, y la clave se quedaría con 5
    /// unidades y 20 € en vez de 10.
    /// </remarks>
    [Fact]
    public void Dentro_de_un_documento_primero_sube_y_despues_baja()
    {
        IReadOnlyList<LineaValorada> filas = s_valoracion.Valorar(
            Saldos((s_clave, SaldoValorado.Vacio("EUR"))),
            [new LineaAValorar(s_clave, -5m), new LineaAValorar(s_clave, 10m, Precio(2m))],
            "EUR");

        filas.ShouldBe([Fila(-10m, 2m), Fila(20m, 2m)]);
    }

    /// <summary>
    /// La segunda línea de la misma clave valora contra lo que dejó la primera, y otra clave no se
    /// entera.
    /// </summary>
    [Fact]
    public void Cada_linea_valora_contra_lo_que_dejaron_las_anteriores_de_su_clave()
    {
        IReadOnlyList<LineaValorada> filas = s_valoracion.Valorar(
            Saldos((s_clave, SaldoValorado.Vacio("EUR")), (s_otra, new SaldoValorado(10m, Importe.De(10m, "EUR")))),
            [
                new LineaAValorar(s_clave, 10m, Precio(2m)),
                new LineaAValorar(s_clave, 10m, Precio(4m)),
                new LineaAValorar(s_otra, -4m),
                new LineaAValorar(s_clave, -5m),
                new LineaAValorar(s_clave, 5m),
            ],
            "EUR");

        filas.ShouldBe(
        [
            Fila(20m, 2m),
            Fila(40m, 3m),
            Fila(-4m, 1m),
            Fila(-15m, 3m),
            Fila(15m, 3m),
        ]);
    }

    /// <summary>El inverso de una salida devuelve su valor exacto, y el par suma cero.</summary>
    /// <remarks>
    /// Tras la salida, el precio medio vigente es 3,333334, y 100 por él son 333,3334. Un inverso
    /// que se valorara al vigente devolvería una diezmilésima de más, y el par no sumaría cero.
    /// </remarks>
    [Fact]
    public void El_inverso_de_una_salida_devuelve_su_valor_exacto_y_el_par_suma_cero()
    {
        SaldoValorado saldo = new(300m, Importe.De(1000m, "EUR"));

        (LineaValorada salida, saldo) = Mover(saldo, -100m);

        saldo.PrecioMedio!.Por(100m).ShouldBe(
            Importe.De(333.3334m, "EUR"),
            "si el vigente por la cantidad diera lo mismo que la salida, el caso no distinguiría " +
            "compensar el valor de valorar otra vez");

        (LineaValorada inverso, saldo) = Mover(saldo, 100m, compensa: 333.3333m);

        inverso.ShouldBe(Fila(333.3333m, 3.333333m));
        (salida.Valor + inverso.Valor).ShouldBe(Importe.Cero("EUR"));
        saldo.ShouldBe(new SaldoValorado(300m, Importe.De(1000m, "EUR")));
    }

    /// <summary>El inverso de una entrada resta su valor exacto, y deja el precio de las demás.</summary>
    [Fact]
    public void El_inverso_de_una_entrada_resta_su_valor_exacto_y_deja_el_precio_de_las_demas()
    {
        var saldo = SaldoValorado.Vacio("EUR");

        (LineaValorada entrada, saldo) = Mover(saldo, 10m, coste: 2m);
        (_, saldo) = Mover(saldo, 5m, coste: 3.50m);

        (LineaValorada inverso, saldo) = Mover(saldo, -10m, compensa: -entrada.Valor.Cantidad);

        inverso.ShouldBe(Fila(-20m, 2.5m));
        saldo.ShouldBe(new SaldoValorado(5m, Importe.De(17.5m, "EUR")));
        saldo.PrecioMedio.ShouldBe(Precio(3.5m));
    }

    /// <summary>
    /// El inverso de una entrada ya repartida se lleva como mucho lo que queda, y la diferencia se
    /// ve.
    /// </summary>
    /// <remarks>
    /// Entran 10 a 10 € y 90 gratis, el medio pasa a 1 €, y una salida de 50 se lleva 50 €. La
    /// entrada trajo 100 € y solo quedan 50: el inverso resta 50, y la línea dice lo que compensaba
    /// (100) y lo que restó (50), las dos cifras escritas (ADR-0046 §6).
    /// </remarks>
    [Fact]
    public void El_inverso_de_una_entrada_ya_repartida_se_lleva_como_mucho_lo_que_queda()
    {
        var saldo = SaldoValorado.Vacio("EUR");

        (LineaValorada entrada, saldo) = Mover(saldo, 10m, coste: 10m);
        (_, saldo) = Mover(saldo, 90m, coste: 0m);
        (_, saldo) = Mover(saldo, -50m);

        (LineaValorada inverso, saldo) = Mover(saldo, -10m, compensa: -entrada.Valor.Cantidad);

        inverso.ShouldBe(Fila(-50m, 1m));
        entrada.Valor.ShouldBe(Importe.De(100m, "EUR"));
        saldo.ShouldBe(new SaldoValorado(40m, Importe.Cero("EUR")));
    }

    /// <summary>Un documento de una línea sobre una clave, y el saldo que deja.</summary>
    private static (LineaValorada Fila, SaldoValorado Queda) Mover(
        SaldoValorado saldo, decimal cantidad, decimal? coste = null, decimal? compensa = null)
    {
        LineaValorada fila = s_valoracion.Valorar(
            Saldos((s_clave, saldo)),
            [
                new LineaAValorar(
                    s_clave,
                    cantidad,
                    coste is null ? null : Precio(coste.Value),
                    compensa is null ? null : Importe.De(compensa.Value, "EUR")),
            ],
            "EUR").ShouldHaveSingleItem();

        return (fila, new SaldoValorado(saldo.Cantidad + cantidad, saldo.Valor + fila.Valor));
    }

    private static Dictionary<ClaveDeValoracion, SaldoValorado> Saldos(
        params (ClaveDeValoracion Clave, SaldoValorado Saldo)[] saldos) =>
        saldos.ToDictionary(uno => uno.Clave, uno => uno.Saldo);

    private static LineaValorada Fila(decimal valor, decimal precioMedio) =>
        new(Importe.De(valor, "EUR"), Precio(precioMedio));

    private static PrecioUnitario Precio(decimal cantidad) => PrecioUnitario.De(cantidad, "EUR");
}
