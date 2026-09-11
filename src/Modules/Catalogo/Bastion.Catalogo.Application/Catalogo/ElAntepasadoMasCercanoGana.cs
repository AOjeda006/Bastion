namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>
/// Una línea candidata a ponerle precio a un artículo, con lo que hace falta para desempatar.
/// </summary>
/// <param name="Id">Identificador de la línea.</param>
/// <param name="ArticuloId">Artículo al que se aplica, o nulo si es de categoría.</param>
/// <param name="CategoriaId">Categoría a la que se aplica, o nulo si es de artículo.</param>
/// <param name="Nivel">
/// Cuántos saltos hacia arriba hay desde la categoría del artículo hasta la de esta línea: <c>0</c>
/// es su propia categoría, <c>1</c> la madre. Nulo en una línea de artículo.
/// <b>Lo calcula el ascenso, no la profundidad de la categoría</b>, y esa es toda la diferencia.
/// </param>
/// <param name="CantidadDesde">Cantidad a partir de la cual rige este tramo, incluida.</param>
/// <param name="Precio">Precio por unidad, o nulo si el tramo es de descuento.</param>
/// <param name="DescuentoPorcentaje">Descuento en tanto por ciento, o nulo si es de precio.</param>
public sealed record LineaCandidata(
    Guid Id,
    Guid? ArticuloId,
    Guid? CategoriaId,
    int? Nivel,
    decimal CantidadDesde,
    decimal? Precio,
    decimal? DescuentoPorcentaje);

/// <summary>
/// De todas las líneas que alcanzan a un artículo, cuál le pone el precio.
/// </summary>
/// <remarks>
/// <para>
/// <b>«Gana la más específica» no basta, y el criterio del ítem lo dice por su nombre.</b> Como la
/// categoría es jerárquica, un artículo puede casar con una línea de su propia categoría <b>y</b>
/// con otra de la categoría madre, y las dos son «por categoría». Sin la precedencia completa el
/// desempate lo decide el orden en que salgan las filas, que es un no-determinismo silencioso: el
/// mismo artículo, dos precios, según el plan de ejecución del día.
/// </para>
/// <para>
/// <b>El orden es: artículo, y después el antepasado MÁS CERCANO.</b> Las dos palabras importan y
/// no son la misma que «más profunda», que es la trampa de este ítem. Si el artículo cuelga de una
/// categoría a profundidad 5, y hay líneas en una categoría a profundidad 3 y en otra a
/// profundidad 7, gana la de 3 — y la de 7 <b>no compite siquiera</b>, porque no es antepasada
/// suya, sino algo que cuelga por otro lado del árbol. Una implementación que ordenara por
/// profundidad descendente pasaría el caso fácil de dos líneas en la misma rama y devolvería la
/// equivocada en cuanto hubiera una rama hermana más honda.
/// </para>
/// <para>
/// Por eso <see cref="LineaCandidata.Nivel"/> es el <b>salto desde el artículo</b> y no la
/// profundidad absoluta de la categoría: las candidatas ya vienen de un ascenso, así que lo que no
/// es antepasado no llega hasta aquí, y entre las que llegan el desempate es un mínimo. La regla no
/// puede elegir una categoría que no sea antepasada porque no la tiene delante.
/// </para>
/// <para>
/// <b>Vive en la capa de aplicación</b>, como <c>ElArbolSigueSiendoUnArbol</c> y
/// <c>LaInversaEsPlausible</c> — el patrón se reutiliza, no se inventa otro—: la regla relaciona
/// varias instancias y la R12 dice una transacción, un agregado. Y vive <b>aquí</b> y no dentro del
/// <c>SELECT</c> que trae las candidatas, que es donde habría sido más cómodo ponerla: en SQL, la
/// precedencia y el tramo solo se podrían ejercer con Docker levantado, y las dos son exactamente
/// las reglas que tienen que poder ponerse rojas en segundos.
/// </para>
/// </remarks>
internal static class ElAntepasadoMasCercanoGana
{
    /// <summary>
    /// La línea que le pone el precio a ese artículo para esa cantidad, o nula si ninguna lo hace.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El tramo se elige aquí también, y con la frontera hacia arriba.</b> De las líneas del
    /// destino que gana, rige la de <see cref="LineaCandidata.CantidadDesde"/> más alta que no pase
    /// de la cantidad pedida: con tramos en 0 y en 100, pedir exactamente 100 aplica el de 100, que
    /// es lo que significa una tabla que dice «a partir de 100». El <c>&lt;=</c> es lo que mete el
    /// borde en el tramo de arriba; con <c>&lt;</c> caería en el de abajo y nadie lo vería hasta
    /// que alguien pidiera justo esa cantidad.
    /// </para>
    /// <para>
    /// <b>El desempate se hace por destino y no por línea suelta</b>, y el orden importa: primero
    /// se decide QUIÉN pone el precio —el artículo, o la categoría antepasada más cercana— y solo
    /// después, entre las líneas de ese destino, cuál tramo. Al revés —coger la mayor
    /// <c>CantidadDesde</c> de todas las candidatas— una línea de categoría con un tramo alto le
    /// ganaría a la línea del propio artículo, que es justo lo que la precedencia existe para
    /// impedir.
    /// </para>
    /// </remarks>
    /// <param name="candidatas">Las líneas que alcanzan al artículo, ya sin las que no son suyas.</param>
    /// <param name="cantidad">Cantidad por la que se pregunta.</param>
    internal static LineaCandidata? Elegir(
        IReadOnlyList<LineaCandidata> candidatas,
        decimal cantidad)
    {
        ArgumentNullException.ThrowIfNull(candidatas);

        // Las del artículo ganan a todas las de categoría, estén al nivel que estén: un precio
        // pactado para una referencia concreta es lo más específico que existe.
        LineaCandidata? deArticulo = DelDestino(
            candidatas.Where(linea => linea.ArticuloId is not null), cantidad);

        if (deArticulo is not null)
        {
            return deArticulo;
        }

        // Y entre las de categoría, el antepasado más cercano. Se recorren los niveles de menor a
        // mayor y se devuelve el PRIMERO que tenga un tramo aplicable, en vez de quedarse con el
        // nivel mínimo y mirar solo ése: una categoría cercana cuya tabla empiece por encima de la
        // cantidad pedida no pone precio, y cortar ahí dejaría sin resolver un artículo que la
        // categoría madre sí cubre. Esto es lo que hace que el ascenso llegue hasta la raíz de
        // verdad, y no solo hasta el primer antepasado que tenga alguna línea.
        foreach (int nivel in candidatas
            .Where(linea => linea.CategoriaId is not null && linea.Nivel is not null)
            .Select(linea => linea.Nivel!.Value)
            .Distinct()
            .Order())
        {
            LineaCandidata? deLaCategoria = DelDestino(
                candidatas.Where(linea => linea.CategoriaId is not null && linea.Nivel == nivel),
                cantidad);

            if (deLaCategoria is not null)
            {
                return deLaCategoria;
            }
        }

        return null;
    }

    // El tramo de un destino: el de `CantidadDesde` más alta que no pase de la cantidad pedida.
    // `<=` y no `<`, que es la frontera hacia arriba de `LineaTarifa.CantidadDesde`.
    private static LineaCandidata? DelDestino(
        IEnumerable<LineaCandidata> deEsteDestino,
        decimal cantidad) =>
        deEsteDestino
            .Where(linea => linea.CantidadDesde <= cantidad)
            .OrderByDescending(linea => linea.CantidadDesde)
            .FirstOrDefault();
}
