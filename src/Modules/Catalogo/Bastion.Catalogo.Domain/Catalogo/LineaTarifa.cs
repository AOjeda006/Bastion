using Bastion.BuildingBlocks.Domain.Entidades;
using Bastion.BuildingBlocks.Domain.Multiempresa;

namespace Bastion.Catalogo.Domain.Catalogo;

/// <summary>
/// Lo que una tarifa dice de un artículo o de una categoría a partir de cierta cantidad.
/// </summary>
/// <remarks>
/// <para>
/// <b>Apunta a un artículo O a una categoría, exactamente a uno.</b> Ninguno de los dos es el
/// caso general del otro: una línea de artículo es un precio pactado para esa referencia, y una de
/// categoría es una política para toda una rama. Los dos a la vez no querrían decir nada, y
/// ninguno de los dos deja la línea sin destino al que aplicarse.
/// </para>
/// <para>
/// <b>Lleva <c>EmpresaId</c> propio aunque su tarifa ya lo tenga</b>, por lo mismo que
/// <c>Ubicacion</c> respecto de su almacén: el filtro global de la R8 se escribe por entidad y se
/// evalúa sobre las columnas de la fila, así que sin la columna habría que salir a buscar la
/// tarifa en cada lectura, y bastaría una consulta que empezara por las líneas —un listado, un
/// informe, la resolución de un precio— para que salieran las de otra empresa.
/// </para>
/// <para>
/// <b>Es agregado propio y no una colección dentro de la tarifa</b>, y es una decisión con motivo:
/// una tarifa de una empresa mediana tiene una línea por referencia, o sea miles. Cargarlas todas
/// para añadir una sola es lo que la R12 —una transacción, un agregado— existe para evitar, y el
/// enredo del rastreador que documenta el ADR-0010 sale precisamente de manipular hijos a través
/// de la colección del padre. Lo que sí es invariante <b>entre</b> líneas —que los tramos de un
/// mismo destino ni se solapen ni dejen hueco— no se resuelve cargándolas: se resuelve con la
/// forma que tienen los tramos, que está explicada en <see cref="CantidadDesde"/>.
/// </para>
/// <para>
/// <b>No guarda ningún dato de ninguna persona</b>, igual que el artículo y la categoría: un
/// precio y una cantidad no identifican a nadie. Eso hace que la tarifa <b>no sea bloqueable</b>
/// (art. 32 de la LOPDGDD, R16), y la respuesta está comprobada y no supuesta —
/// <c>ElCatalogoNoGuardaDatosDeNadieTests</c> recorre el modelo entero de este módulo—. El día que
/// aquí aparezca a quién se le concedió un precio especial, la respuesta deja de ser ésta.
/// </para>
/// </remarks>
public sealed class LineaTarifa : EntidadBase, IDeInquilino
{
    private LineaTarifa(
        Guid id,
        Guid empresaId,
        Guid tarifaId,
        Guid? articuloId,
        Guid? categoriaId,
        decimal cantidadDesde,
        PrecioODescuento precioODescuento,
        DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        EmpresaId = empresaId;
        TarifaId = tarifaId;
        ArticuloId = articuloId;
        CategoriaId = categoriaId;
        CantidadDesde = cantidadDesde;
        PrecioODescuento = precioODescuento;
    }

    private LineaTarifa() => PrecioODescuento = null!;

    /// <summary>Identificador de la línea.</summary>
    public Guid Id { get; private set; }

    /// <summary>Empresa a la que pertenece (R8).</summary>
    public Guid EmpresaId { get; private set; }

    /// <summary>Tarifa de la que cuelga.</summary>
    public Guid TarifaId { get; private set; }

    /// <summary>Artículo al que se aplica, o nulo si la línea es de categoría.</summary>
    public Guid? ArticuloId { get; private set; }

    /// <summary>Categoría a la que se aplica, o nulo si la línea es de artículo.</summary>
    public Guid? CategoriaId { get; private set; }

    /// <summary>
    /// Cantidad a partir de la cual se aplica esta línea, incluida.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es el ÚNICO extremo escrito del tramo, y de ahí sale todo lo demás.</b> El tramo de una
    /// línea es <c>[CantidadDesde, la CantidadDesde de la siguiente)</c>: cerrado por abajo,
    /// abierto por arriba, y el último llega hasta donde haga falta. Escribir solo el extremo
    /// inferior no es economía de columnas — es lo que hace que las dos cosas que pueden salir mal
    /// no puedan pasar:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///   <b>Solaparse es imposible:</b> dos tramos del mismo destino con la misma
    ///   <c>CantidadDesde</c> los rechaza un índice único, y con distinta son disjuntos por la
    ///   forma del intervalo. No hace falta comprobar nada.
    ///   </description></item>
    ///   <item><description>
    ///   <b>Dejar hueco es imposible:</b> cada tramo llega hasta donde empieza el siguiente, así
    ///   que el único hueco posible está <b>por debajo del primero</b>. Por eso la primera línea de
    ///   cada destino tiene que empezar en <b>cero</b>, esta columna no se modifica y no hay
    ///   borrado de líneas. Con esas tres, toda cantidad cae en algún tramo.
    ///   </description></item>
    /// </list>
    /// <para>
    /// <b>Y el hueco importa más que el solape</b>, que es lo que decide la forma: un solape
    /// devuelve un precio de dos que alguien escribió, y un hueco devuelve «sin tarifa aplicable»
    /// para una cantidad que está <b>en medio de una tabla de precios</b> — quien lo ve tiene el
    /// precio delante y el sistema le dice que no hay.
    /// </para>
    /// <para>
    /// <b>La frontera cae hacia arriba</b>, y es la otra mitad de la decisión: pedir exactamente
    /// 100 unidades con tramos en 0 y en 100 aplica el de 100. Es lo que significa una tabla que
    /// dice «a partir de 100», que es como se escriben y como se leen.
    /// </para>
    /// </remarks>
    public decimal CantidadDesde { get; private set; }

    /// <summary>El precio o el descuento de esta línea. Uno de los dos, nunca los dos ni ninguno.</summary>
    public PrecioODescuento PrecioODescuento { get; private set; }

    /// <summary>Da de alta una línea para un artículo.</summary>
    /// <param name="empresaId">Empresa a la que pertenece (R8), que sale del <i>claim</i>.</param>
    /// <param name="tarifaId">Tarifa de la que cuelga, ya comprobada.</param>
    /// <param name="articuloId">Artículo al que se aplica, ya comprobado.</param>
    /// <param name="cantidadDesde">Cantidad a partir de la cual se aplica, incluida.</param>
    /// <param name="precioODescuento">El precio o el descuento.</param>
    /// <param name="momento">Ahora, de quien tenga el <c>TimeProvider</c>.</param>
    public static LineaTarifa ParaArticulo(
        Guid empresaId,
        Guid tarifaId,
        Guid articuloId,
        decimal cantidadDesde,
        PrecioODescuento precioODescuento,
        DateTimeOffset momento)
    {
        if (articuloId == Guid.Empty)
        {
            throw new ArgumentException(
                "Una línea de artículo se aplica a algún artículo.", nameof(articuloId));
        }

        return Nueva(
            empresaId, tarifaId, articuloId, null, cantidadDesde, precioODescuento, momento);
    }

    /// <summary>Da de alta una línea para una categoría y todo lo que cuelgue de ella.</summary>
    /// <param name="empresaId">Empresa a la que pertenece (R8), que sale del <i>claim</i>.</param>
    /// <param name="tarifaId">Tarifa de la que cuelga, ya comprobada.</param>
    /// <param name="categoriaId">Categoría a la que se aplica, ya comprobada.</param>
    /// <param name="cantidadDesde">Cantidad a partir de la cual se aplica, incluida.</param>
    /// <param name="precioODescuento">El precio o el descuento.</param>
    /// <param name="momento">Ahora, de quien tenga el <c>TimeProvider</c>.</param>
    public static LineaTarifa ParaCategoria(
        Guid empresaId,
        Guid tarifaId,
        Guid categoriaId,
        decimal cantidadDesde,
        PrecioODescuento precioODescuento,
        DateTimeOffset momento)
    {
        if (categoriaId == Guid.Empty)
        {
            throw new ArgumentException(
                "Una línea de categoría se aplica a alguna categoría.", nameof(categoriaId));
        }

        return Nueva(
            empresaId, tarifaId, null, categoriaId, cantidadDesde, precioODescuento, momento);
    }

    /// <summary>
    /// Cambia el precio o el descuento. <b>Ni el destino ni la cantidad desde la que se aplica.</b>
    /// </summary>
    /// <remarks>
    /// La cantidad no se modifica y eso es lo que cierra el hueco del todo: si el tramo que empieza
    /// en cero pudiera moverse a cinco, una cantidad de tres dejaría de tener tramo sin que nadie
    /// hubiera borrado nada. Corregir la escala de una tarifa es añadir el tramo que falta, que no
    /// reescribe ningún precio ya aplicado.
    /// </remarks>
    /// <param name="precioODescuento">El precio o el descuento nuevos.</param>
    public void Modificar(PrecioODescuento precioODescuento)
    {
        ArgumentNullException.ThrowIfNull(precioODescuento);

        PrecioODescuento = precioODescuento;
    }

    private static LineaTarifa Nueva(
        Guid empresaId,
        Guid tarifaId,
        Guid? articuloId,
        Guid? categoriaId,
        decimal cantidadDesde,
        PrecioODescuento precioODescuento,
        DateTimeOffset momento)
    {
        ArgumentNullException.ThrowIfNull(precioODescuento);

        if (empresaId == Guid.Empty)
        {
            throw new ArgumentException(
                "Una línea de tarifa pertenece siempre a una empresa (R8).", nameof(empresaId));
        }

        if (tarifaId == Guid.Empty)
        {
            throw new ArgumentException(
                "Una línea de tarifa cuelga siempre de una tarifa.", nameof(tarifaId));
        }

        if (cantidadDesde < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cantidadDesde),
                cantidadDesde,
                "Un tramo de cantidad no empieza por debajo de cero: no hay cantidades negativas " +
                "que comprar.");
        }

        return new LineaTarifa(
            Guid.CreateVersion7(),
            empresaId,
            tarifaId,
            articuloId,
            categoriaId,
            cantidadDesde,
            precioODescuento,
            momento);
    }
}
