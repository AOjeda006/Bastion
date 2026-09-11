using Bastion.Catalogo.Application.Catalogo;
using Shouldly;

namespace Bastion.Catalogo.UnitTests.Catalogo;

/// <summary>
/// La precedencia completa: el artículo antes que la categoría, el antepasado más cercano antes
/// que el lejano, y el tramo de cantidad con la frontera dicha.
/// </summary>
/// <remarks>
/// <para>
/// <b>«Más cercana» y «más profunda» NO son lo mismo, y ésa es la trampa del ítem.</b> Si el
/// artículo cuelga de una categoría a profundidad 5, y hay líneas en una categoría a profundidad 3
/// y en otra a profundidad 7, gana la de 3 — y la de 7 <b>no compite siquiera</b>, porque no es
/// antepasada suya, sino algo que cuelga por otro lado del árbol. Una implementación que ordenara
/// por profundidad descendente pasa el caso fácil de dos líneas en la misma rama y falla ése, y el
/// fallo no tiene síntoma: devuelve un precio, sólo que el equivocado.
/// </para>
/// <para>
/// Que la regla <b>no pueda</b> equivocarse así es una decisión de diseño y no una comprobación:
/// <see cref="LineaCandidata.Nivel"/> es el salto desde el artículo, no la profundidad absoluta de
/// la categoría, y las candidatas salen de un ascenso, así que lo que no es antepasado no llega.
/// Los casos de abajo lo ejercen por los dos lados: el que no es antepasado no está en la lista, y
/// entre los que sí están gana el mínimo.
/// </para>
/// </remarks>
public sealed class ElAntepasadoMasCercanoGanaTests
{
    private static readonly Guid s_articulo = Guid.NewGuid();

    [Fact]
    public void La_linea_del_articulo_le_gana_a_la_de_la_categoria()
    {
        // La de la categoría está a cero saltos —es SU categoría, lo más cercano que hay— y aun
        // así pierde: un precio pactado para una referencia concreta es más específico que
        // cualquier cosa dicha de una rama.
        LineaCandidata deArticulo = DeArticulo(cantidadDesde: 0m, precio: 10m);
        LineaCandidata deCategoria = DeCategoria(nivel: 0, cantidadDesde: 0m, precio: 7m);

        ElAntepasadoMasCercanoGana.Elegir([deCategoria, deArticulo], cantidad: 1m)
            .ShouldBe(deArticulo);

        // Y en el otro orden, porque si el desempate dependiera del orden de llegada esto saldría
        // verde la mitad de las veces y en producción dependería del plan de ejecución.
        ElAntepasadoMasCercanoGana.Elegir([deArticulo, deCategoria], cantidad: 1m)
            .ShouldBe(deArticulo);
    }

    [Fact]
    public void Entre_dos_categorias_antepasadas_gana_la_mas_cercana()
    {
        LineaCandidata cercana = DeCategoria(nivel: 1, cantidadDesde: 0m, precio: 9m);
        LineaCandidata lejana = DeCategoria(nivel: 4, cantidadDesde: 0m, precio: 3m);

        ElAntepasadoMasCercanoGana.Elegir([lejana, cercana], cantidad: 1m).ShouldBe(cercana);
        ElAntepasadoMasCercanoGana.Elegir([cercana, lejana], cantidad: 1m).ShouldBe(cercana);
    }

    /// <summary>
    /// EL CASO DEL ÍTEM, escrito con sus tres profundidades: la rama hermana más honda no compite.
    /// </summary>
    /// <remarks>
    /// El artículo cuelga de una categoría a profundidad 5. Hay una línea en una categoría a
    /// profundidad 3 —antepasada suya, a dos saltos— y otra en una categoría a profundidad 7, que
    /// cuelga por otro lado del árbol. La de 7 <b>no aparece entre las candidatas</b>, porque las
    /// candidatas salen del ascenso, y si apareciera con nivel nulo tampoco ganaría. Las dos
    /// mitades se ejercen aquí: la segunda llamada mete a propósito la intrusa para comprobar que
    /// ni siquiera colada se lleva el precio.
    /// </remarks>
    [Fact]
    public void La_categoria_mas_profunda_que_no_es_antepasada_no_compite()
    {
        // Profundidad 3, dos saltos por encima del artículo: ES antepasada.
        LineaCandidata antepasadaADosSaltos = DeCategoria(nivel: 2, cantidadDesde: 0m, precio: 12m);

        // Profundidad 7, más honda que la del artículo y de otra rama: NO es antepasada, así que
        // no tiene salto que contar. Es la línea que una ordenación por profundidad elegiría.
        LineaCandidata deOtraRamaMasHonda = new(
            Guid.NewGuid(), null, Guid.NewGuid(), null, 0m, 1m, null);

        ElAntepasadoMasCercanoGana.Elegir([antepasadaADosSaltos], cantidad: 1m)
            .ShouldBe(antepasadaADosSaltos);

        ElAntepasadoMasCercanoGana
            .Elegir([deOtraRamaMasHonda, antepasadaADosSaltos], cantidad: 1m)
            .ShouldBe(
                antepasadaADosSaltos,
                "ha ganado una línea de una categoría que no es antepasada del artículo. Es la " +
                "mutación 1: ordenar por profundidad pasa el caso de dos líneas en la misma rama " +
                "y devuelve el precio de otra rama en cuanto hay una hermana más honda");
    }

    [Fact]
    public void Sin_ninguna_candidata_no_hay_linea_y_no_hay_cero()
    {
        // Devolver nulo es lo que permite a quien llama contestar «sin tarifa aplicable» con
        // nombre. Un cero aquí sería el precio que nadie escribió, entrando por la puerta de atrás
        // (decisión 3 del ADR-0023).
        ElAntepasadoMasCercanoGana.Elegir([], cantidad: 1m).ShouldBeNull();
    }

    [Fact]
    public void Una_categoria_cercana_sin_tramo_aplicable_deja_pasar_a_la_de_arriba()
    {
        // La cercana empieza en 100 y se piden 5: no pone precio. Quedarse con el nivel mínimo y
        // mirar sólo ése devolvería «sin línea aplicable» teniendo la madre un tramo desde cero.
        LineaCandidata cercanaPeroAlta = DeCategoria(nivel: 0, cantidadDesde: 100m, precio: 8m);
        LineaCandidata lejanaDesdeCero = DeCategoria(nivel: 3, cantidadDesde: 0m, precio: 20m);

        ElAntepasadoMasCercanoGana.Elegir([cercanaPeroAlta, lejanaDesdeCero], cantidad: 5m)
            .ShouldBe(lejanaDesdeCero);
    }

    /// <summary>LA FRONTERA DE LOS TRAMOS: la cantidad del borde cae en el tramo de arriba.</summary>
    /// <remarks>
    /// Con tramos en 0 y en 100, pedir exactamente 100 aplica el de 100, que es lo que significa
    /// una tabla que dice «a partir de 100». Es <c>[desde, siguiente)</c>: cerrado por abajo y
    /// abierto por arriba. La decisión hay que tomarla —una cantidad justo en el borde cae en uno
    /// de los dos y hay que decir en cuál— y ésta es la que coincide con cómo se lee la tabla
    /// impresa.
    /// </remarks>
    [Theory]
    [InlineData(99, 10)]
    [InlineData(99.999999, 10)]
    [InlineData(100, 8)]
    [InlineData(100.000001, 8)]
    [InlineData(250, 8)]
    public void El_tramo_se_elige_con_la_frontera_hacia_arriba(decimal cantidad, decimal esperado)
    {
        LineaCandidata desdeCero = DeArticulo(cantidadDesde: 0m, precio: 10m);
        LineaCandidata desdeCien = DeArticulo(cantidadDesde: 100m, precio: 8m);

        ElAntepasadoMasCercanoGana.Elegir([desdeCero, desdeCien], cantidad)!.Precio.ShouldBe(
            esperado,
            "la frontera de los tramos se ha movido un escalón. Es la mutación 7, y no da error: " +
            "da el precio del tramo de al lado para la cantidad exacta del borde");
    }

    [Fact]
    public void Un_tramo_de_categoria_alto_no_le_gana_a_la_linea_del_articulo()
    {
        // El orden de los dos desempates importa: primero QUIÉN pone el precio, después cuál
        // tramo. Al revés —coger la mayor `CantidadDesde` de todas las candidatas— esta línea de
        // categoría, que empieza más arriba, le ganaría a la del propio artículo.
        LineaCandidata deArticuloDesdeCero = DeArticulo(cantidadDesde: 0m, precio: 10m);
        LineaCandidata deCategoriaDesdeCien = DeCategoria(nivel: 0, cantidadDesde: 100m, precio: 4m);

        ElAntepasadoMasCercanoGana
            .Elegir([deCategoriaDesdeCien, deArticuloDesdeCero], cantidad: 500m)
            .ShouldBe(
                deArticuloDesdeCero,
                "es la mutación 2: la línea del artículo pierde contra la de la categoría, y el " +
                "precio pactado para una referencia concreta deja de aplicarse en cuanto se " +
                "piden muchas unidades");
    }

    [Fact]
    public void La_cantidad_por_debajo_del_primer_tramo_no_encuentra_nada()
    {
        // No puede pasar por la puerta de la API —el primer tramo de cada destino empieza en cero
        // y la cantidad no puede ser negativa—, pero la regla tiene que decirlo igual: lo que no
        // tiene tramo no tiene precio, y no tiene cero.
        ElAntepasadoMasCercanoGana
            .Elegir([DeArticulo(cantidadDesde: 10m, precio: 5m)], cantidad: 1m)
            .ShouldBeNull();
    }

    private static LineaCandidata DeArticulo(decimal cantidadDesde, decimal precio) =>
        new(Guid.NewGuid(), s_articulo, null, null, cantidadDesde, precio, null);

    private static LineaCandidata DeCategoria(int nivel, decimal cantidadDesde, decimal precio) =>
        new(Guid.NewGuid(), null, Guid.NewGuid(), nivel, cantidadDesde, precio, null);
}
