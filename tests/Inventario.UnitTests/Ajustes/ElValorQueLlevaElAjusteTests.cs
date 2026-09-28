using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Inventario.UnitTests.Valoraciones;
using Shouldly;

namespace Bastion.Inventario.UnitTests.Ajustes;

/// <summary>
/// Lo que el ajuste hace con su valoración (ADR-0046 §6 y §10): qué pregunta, qué se queda cada
/// línea y cada fila del libro, y qué copia el inverso.
/// </summary>
/// <remarks>
/// <para>
/// <b>El cálculo no está aquí</b>, está en <c>LosCasosDoradosDelPrecioMedioTests</c>. Aquí se mira
/// la costura: que el documento pregunte por la clave y la cantidad que el libro va a sumar, que se
/// quede lo que le contestan sin retocarlo, y que no se deje confirmar con una contestación que no
/// casa.
/// </para>
/// <para>
/// <b>La afirmación que decide es que el par suma cero en valor, clave a clave</b>, igual que en
/// cantidad. Un inverso que copiara el coste en vez del valor volvería a valorar la salida al
/// precio medio de hoy, que no es el de ayer, y la valoración se quedaría con un resto sin unidades.
/// </para>
/// </remarks>
public sealed class ElValorQueLlevaElAjusteTests
{
    private static readonly DateTimeOffset s_momento = new(2026, 3, 14, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly s_dia = new(2026, 3, 14);

    /// <summary>
    /// Lo que el documento pregunta: la clave de su almacén, la cantidad en unidad base y el coste
    /// en su divisa, o nada si baja.
    /// </summary>
    /// <remarks>
    /// <b>La cantidad es la base y no la tecleada</b>, porque es la que suma el libro. Con el factor
    /// doce, una valoración que preguntara por la tecleada valoraría tres unidades donde entran
    /// treinta y seis.
    /// </remarks>
    [Fact]
    public void Pregunta_por_su_almacen_la_cantidad_base_y_el_coste_en_su_divisa()
    {
        var almacenId = Guid.CreateVersion7();
        var queSube = Guid.CreateVersion7();
        var queBaja = Guid.CreateVersion7();

        Ajuste ajuste = UnAjuste(almacenId, "usd");
        ConLinea(ajuste, queSube, 3m, 12m, 2.50m);
        ConLinea(ajuste, queBaja, -2.5m, 1m, null);

        ajuste.LineasAValorar().ShouldBe(
        [
            new LineaAValorar(new ClaveDeValoracion(queSube, almacenId), 36m, PrecioUnitario.De(2.50m, "USD")),
            new LineaAValorar(new ClaveDeValoracion(queBaja, almacenId), -2.5m),
        ]);
    }

    /// <summary>Cada línea y cada fila del libro se quedan lo que les dieron, tal cual.</summary>
    [Fact]
    public void Cada_linea_y_cada_fila_se_quedan_el_valor_y_el_precio_que_les_dieron()
    {
        Ajuste ajuste = UnAjuste(Guid.CreateVersion7());
        ConLinea(ajuste, Guid.CreateVersion7(), 3m, 12m, 2.50m);
        ConLinea(ajuste, Guid.CreateVersion7(), -2.5m, 1m, null);

        IReadOnlyList<MovimientoStock> filas = ajuste.Confirmar(
            1,
            Confirmado(ajuste),
            [
                new LineaValorada(Importe.De(90m, "EUR"), PrecioUnitario.De(2.5m, "EUR")),
                new LineaValorada(Importe.De(-7.5m, "EUR"), PrecioUnitario.De(3m, "EUR")),
            ],
            s_momento);

        ajuste.Lineas.Select(linea => linea.Valor).ShouldBe([90m, -7.5m]);
        filas.Select(fila => fila.Valor).ShouldBe([Importe.De(90m, "EUR"), Importe.De(-7.5m, "EUR")]);
        filas.Select(fila => fila.PrecioMedio)
            .ShouldBe([PrecioUnitario.De(2.5m, "EUR"), PrecioUnitario.De(3m, "EUR")]);
    }

    /// <summary>
    /// Una valoración que no casa con el documento no lo confirma, y el documento sigue como estaba.
    /// </summary>
    /// <remarks>
    /// <b>Se comprueba antes de transitar</b>, y por eso el estado, el número y los valores de las
    /// líneas se afirman los tres: si se comprobara después, el documento se quedaría confirmado con
    /// la mitad de las líneas valoradas.
    /// </remarks>
    /// <param name="como">Qué tiene de malo la valoración.</param>
    [Theory]
    [InlineData("falta una")]
    [InlineData("sobra una")]
    [InlineData("una nula")]
    [InlineData("el valor en otra divisa")]
    [InlineData("el precio en otra divisa")]
    public void Una_valoracion_que_no_casa_no_confirma(string como)
    {
        Ajuste ajuste = UnAjuste(Guid.CreateVersion7());
        ConLinea(ajuste, Guid.CreateVersion7(), 3m, 1m, 2.50m);
        ConLinea(ajuste, Guid.CreateVersion7(), 1m, 1m, 2.50m);

        LineaValorada buena = new(Importe.De(7.5m, "EUR"), PrecioUnitario.De(2.5m, "EUR"));

        List<LineaValorada> valoracion = como switch
        {
            "falta una" => [buena],
            "sobra una" => [buena, buena, buena],
            "una nula" => [buena, null!],
            "el valor en otra divisa" => [buena, buena with { Valor = Importe.De(7.5m, "USD") }],
            _ => [buena, buena with { PrecioMedio = PrecioUnitario.De(2.5m, "USD") }],
        };

        Should.Throw<ArgumentException>(() => ajuste.Confirmar(1, Confirmado(ajuste), valoracion, s_momento))
            .ParamName.ShouldBe("valoracion");

        ajuste.Estado.ShouldBe(EstadoDeAjuste.Borrador);
        ajuste.Numero.ShouldBeNull();
        ajuste.Lineas.ShouldAllBe(linea => linea.Valor == null);
        ajuste.EventosPendientes.ShouldBeEmpty();
    }

    /// <summary>El par suma cero en valor en cada clave, y el inverso lleva el valor y no el coste.</summary>
    /// <remarks>
    /// <para>
    /// <b>La salida del original va contra un precio medio que no es el de su coste</b>: el artículo
    /// ya tenía existencias a 3,00, y el original no mueve su precio porque solo saca. Así, un inverso
    /// que volviera a valorar al precio de ese momento, o al coste de la línea, no sumaría cero.
    /// </para>
    /// <para>
    /// <b>Se afirma que hay dos claves y que el original movió valor en las dos</b>, por lo mismo que
    /// en cantidad (ADR-0020): sobre ninguna clave, o sobre valores a cero, sumar cero no dice nada.
    /// </para>
    /// </remarks>
    [Fact]
    public void El_par_suma_cero_en_valor_y_el_inverso_lleva_el_valor_y_no_el_coste()
    {
        var almacenId = Guid.CreateVersion7();
        var queSube = Guid.CreateVersion7();
        var queBaja = Guid.CreateVersion7();

        Ajuste previo = UnAjuste(almacenId);
        ConLinea(previo, queBaja, 10m, 1m, 3m);
        IReadOnlyList<MovimientoStock> delPrevio =
            previo.Confirmar(1, Confirmado(previo), LaValoracion.DesdeCero(previo), s_momento);

        Ajuste original = UnAjuste(almacenId);
        ConLinea(original, queSube, 3m, 12m, 2.50m);
        ConLinea(original, queBaja, -2.5m, 1m, null);
        IReadOnlyList<MovimientoStock> delOriginal =
            original.Confirmar(2, Confirmado(original), LaValoracion.Tras(delPrevio, original), s_momento);

        delOriginal.Select(fila => fila.Valor.Cantidad).ShouldBe(
            [90m, -7.5m], "36 unidades a 2,50 y 2,5 al precio medio de 3,00, con su signo");
        delOriginal[1].PrecioMedio.ShouldBe(
            PrecioUnitario.De(3m, "EUR"), "la salida congela el precio medio de antes de salir");

        Ajuste inverso = original.CrearInverso(s_dia.AddDays(1), "Me equivoqué", s_momento);

        inverso.Lineas.ShouldAllBe(
            linea => linea.CosteUnitario == null, "el inverso no copia el coste, copia el valor");
        inverso.Lineas.Select(linea => linea.ValorQueCompensa).ShouldBe([-90m, 7.5m]);

        IReadOnlyList<MovimientoStock> delInverso = inverso.Confirmar(
            3, Confirmado(inverso), LaValoracion.Tras(delPrevio.Concat(delOriginal), inverso), s_momento);

        var claves = delOriginal.Concat(delInverso)
            .GroupBy(fila => fila.ArticuloId)
            .Select(clave => new { clave.Key, Valor = clave.Sum(fila => fila.Valor.Cantidad) })
            .ToList();

        claves.Count.ShouldBe(2, "el par ha valorado dos artículos (ADR-0020)");

        foreach (var clave in claves)
        {
            clave.Valor.ShouldBe(
                0m, $"el par deja el artículo «{clave.Key}» valorado como estaba, y lo ha movido {clave.Valor}");
        }
    }

    /// <summary>
    /// Cada línea lleva el número en que se escribió, y la del inverso el de la suya: el inverso se
    /// escribe en el orden del original.
    /// </summary>
    /// <remarks>
    /// El número es el orden de valoración (ADR-0046 §3). Que el documento lo siga también después
    /// de leerlo de la base, que devuelve las filas en el orden que quiere, lo sostiene
    /// <c>LaValoracionDelAjusteTests</c>: aquí no hay base que las desordene.
    /// </remarks>
    [Fact]
    public void Cada_linea_lleva_el_numero_en_que_se_escribio_y_la_del_inverso_el_de_la_suya()
    {
        var almacenId = Guid.CreateVersion7();
        var articuloId = Guid.CreateVersion7();

        Ajuste original = UnAjuste(almacenId);
        ConLinea(original, articuloId, 4m, 1m, 2m);
        ConLinea(original, Guid.CreateVersion7(), 3m, 1m, 1m);
        ConLinea(original, articuloId, -1m, 1m, null);

        original.Lineas.Select(linea => (linea.Numero, linea.CantidadIntroducida))
            .ShouldBe([(1, 4m), (2, 3m), (3, -1m)]);

        original.Confirmar(1, Confirmado(original), LaValoracion.DesdeCero(original), s_momento);

        Ajuste inverso = original.CrearInverso(s_dia.AddDays(1), "Me equivoqué", s_momento);

        inverso.Lineas.Select(linea => (linea.Numero, linea.CantidadIntroducida)).ShouldBe(
            [(1, -4m), (2, -3m), (3, 1m)], "cada una con el número de la que compensa");
    }

    /// <summary>
    /// Una fila del libro no lleva un valor de signo contrario a su cantidad, ni en otra divisa, ni
    /// un precio medio negativo.
    /// </summary>
    /// <remarks>
    /// Son las guardas de la última puerta: el documento ya comprueba la divisa, pero la fila se
    /// puede registrar sin documento, y la tabla de la valoración suma lo que la fila diga.
    /// </remarks>
    /// <param name="cantidad">La cantidad tecleada, con signo.</param>
    /// <param name="valor">El valor de la fila.</param>
    /// <param name="divisaDelValor">En qué divisa viene el valor.</param>
    /// <param name="precio">El precio medio.</param>
    /// <param name="divisaDelPrecio">En qué divisa viene el precio.</param>
    /// <param name="parametro">El parámetro que el rechazo tiene que nombrar.</param>
    [Theory]
    [InlineData(1d, 2.5d, "USD", 2.5d, "EUR", "valor")]
    [InlineData(1d, 2.5d, "EUR", 2.5d, "USD", "valor")]
    [InlineData(1d, -2.5d, "EUR", 2.5d, "EUR", "valor")]
    [InlineData(-1d, 2.5d, "EUR", 2.5d, "EUR", "valor")]
    [InlineData(1d, 2.5d, "EUR", -2.5d, "EUR", "precioMedio")]
    public void Una_fila_del_libro_no_lleva_un_valor_que_no_es_el_suyo(
        double cantidad,
        double valor,
        string divisaDelValor,
        double precio,
        string divisaDelPrecio,
        string parametro)
    {
        Should.Throw<ArgumentException>(() => MovimientoStock.Registrar(
            Guid.CreateVersion7(),
            s_dia,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            (decimal)cantidad,
            Guid.CreateVersion7(),
            1m,
            "EUR",
            null,
            Importe.De((decimal)valor, divisaDelValor),
            PrecioUnitario.De((decimal)precio, divisaDelPrecio),
            TipoDeDocumentoOrigen.Ajuste,
            Guid.CreateVersion7(),
            s_momento)).ParamName.ShouldBe(parametro);
    }

    private static Ajuste UnAjuste(Guid almacenId, string divisa = "EUR") => Ajuste.Abrir(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        almacenId,
        s_dia,
        "Recuento de marzo",
        divisa,
        s_momento);

    private static void ConLinea(
        Ajuste ajuste, Guid articuloId, decimal cantidad, decimal factor, decimal? coste) =>
        ajuste.AnadirLinea(
            Guid.CreateVersion7(),
            articuloId,
            cantidad,
            Guid.CreateVersion7(),
            factor,
            coste,
            s_momento);

    private static AjusteConfirmado Confirmado(Ajuste ajuste) => new(
        ajuste.Id, ajuste.EmpresaId, ajuste.AlmacenId, ajuste.FechaDeOperacion, ajuste.Lineas.Count);
}
