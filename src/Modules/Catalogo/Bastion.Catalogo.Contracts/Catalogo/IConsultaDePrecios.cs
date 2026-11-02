namespace Bastion.Catalogo.Contracts.Catalogo;

/// <summary>Qué contesta una tarifa cuando se le pide un precio de compra.</summary>
/// <remarks>
/// <para>
/// <b>Un solo valor propone, y los demás dicen por qué no.</b> El pedido de compra del 3.7 copia
/// el precio y el descuento propuestos en su línea, y los congela al confirmar (R7). Lo que no se
/// propone no es un cero: es una línea que alguien escribe a mano. Cada motivo es un arreglo
/// distinto, y quien pregunta decide si lo enseña.
/// </para>
/// <para>
/// <c>NoExisteLaTarifa</c> es el valor cero por lo mismo que en <see cref="EstadoDeLaTarifa"/>: un
/// <c>default</c> que llegara por un camino que no pasó por el puerto no propone nada.
/// </para>
/// </remarks>
public enum PropuestaDePrecio
{
    /// <summary>No hay ninguna tarifa con ese identificador en esta empresa.</summary>
    NoExisteLaTarifa = 0,

    /// <summary>Propone el precio y el descuento de su línea.</summary>
    Propone = 1,

    /// <summary>La tarifa es de venta: su precio es el que se le cobra a un cliente.</summary>
    EsDeVenta = 2,

    /// <summary>Ningún tramo de su código rige ese día.</summary>
    NoRigeEseDia = 3,

    /// <summary>El tramo que rige no está en la divisa base de la empresa.</summary>
    /// <remarks>
    /// Convertirlo pide el tipo de cambio de ese día, que es de la fase 5. Hasta entonces, un
    /// precio en dólares para un pedido en euros se escribe a mano, y no se propone convertido a
    /// ojo. Una empresa bloqueada no tiene divisa base que contestar, y tampoco propone.
    /// </remarks>
    NoEstaEnLaDivisaBase = 4,

    /// <summary>No hay ningún artículo con ese identificador en esta empresa.</summary>
    NoExisteElArticulo = 5,

    /// <summary>
    /// La tarifa rige, pero ninguna de sus líneas alcanza a ese artículo con esa cantidad.
    /// </summary>
    SinLineaParaElArticulo = 6,
}

/// <summary>
/// El precio de compra que propone una tarifa, o el motivo por el que no propone.
/// </summary>
/// <remarks>
/// <b>El precio va solo con <see cref="PropuestaDePrecio.Propone"/>, y no se puede construir de
/// otra manera.</b> Un precio al lado de un motivo para no proponer sería un número que alguien
/// acabaría copiando: el de otra divisa, o el de un tramo que no rige. Las dos fábricas son las
/// únicas puertas.
/// </remarks>
public sealed record PrecioPropuesto
{
    private PrecioPropuesto(PropuestaDePrecio propuesta, PrecioResueltoDto? precio)
    {
        Propuesta = propuesta;
        Precio = precio;
    }

    /// <summary>Si propone, o por qué no.</summary>
    public PropuestaDePrecio Propuesta { get; }

    /// <summary>
    /// El precio resuelto, solo cuando <see cref="Propuesta"/> es <c>Propone</c>.
    /// </summary>
    public PrecioResueltoDto? Precio { get; }

    /// <summary>La tarifa propone este precio.</summary>
    /// <param name="precio">El precio resuelto, con su tramo, su divisa y su origen.</param>
    public static PrecioPropuesto Con(PrecioResueltoDto precio)
    {
        ArgumentNullException.ThrowIfNull(precio);

        return new PrecioPropuesto(PropuestaDePrecio.Propone, precio);
    }

    /// <summary>La tarifa no propone, por este motivo.</summary>
    /// <param name="motivo">Cualquiera de los valores menos <c>Propone</c>.</param>
    public static PrecioPropuesto Sin(PropuestaDePrecio motivo)
    {
        if (motivo == PropuestaDePrecio.Propone || !Enum.IsDefined(motivo))
        {
            throw new ArgumentOutOfRangeException(
                nameof(motivo), motivo, "Sin precio, la propuesta tiene que ser un motivo para no proponer.");
        }

        return new PrecioPropuesto(motivo, null);
    }
}

/// <summary>Lo que otros módulos pueden preguntar sobre los precios de las tarifas.</summary>
/// <remarks>
/// <para>
/// <b>Aparte de <see cref="IConsultaDeTarifas"/>, y no por orden.</b> Aquel contesta si una tarifa
/// se puede asignar; este, qué precio propone. Son dos familias de respuestas, y la matriz de
/// puerto × estado las cuenta por puerto: en uno solo, las dos se pisarían.
/// </para>
/// <para>
/// <b>Por aquí cruzan <c>Guid</c>, <c>decimal</c>, <c>DateOnly</c> y tipos de este proyecto</b>,
/// como en el resto de <c>Catalogo.Contracts</c>. La tarifa de compra de un proveedor la dice
/// <c>Terceros.Contracts</c>, y quien pregunta trae aquí su identificador.
/// </para>
/// </remarks>
public interface IConsultaDePrecios
{
    /// <summary>
    /// El precio de compra que propone una tarifa para un artículo, una cantidad y una fecha.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La tarifa es la que lleva el tercero, y el precio sale del tramo de su código que rige
    /// ese día</b>, como en <c>GET /api/v1/catalogo/tarifas/{codigo}/precio</c>. El tercero guarda
    /// el tramo que se le asignó, y ese tramo puede haber caducado y tener sucesor: lo que se
    /// propone es el precio del sucesor, y <see cref="PrecioResueltoDto.TarifaId"/> dice cuál es.
    /// </para>
    /// <para>
    /// <b>Solo propone una tarifa de compra en la divisa base de la empresa.</b> Los motivos se
    /// miran de la tarifa hacia la línea: primero lo que es la tarifa —que exista, su tipo, su
    /// vigencia y su divisa—, y después el artículo y su línea. Así, una tarifa de venta contesta
    /// <c>EsDeVenta</c> pregunte por el artículo que pregunte.
    /// </para>
    /// </remarks>
    /// <param name="tarifaId">El tramo de tarifa que lleva el tercero.</param>
    /// <param name="articuloId">Artículo al que se le quiere poner precio.</param>
    /// <param name="cantidad">Cantidad en la unidad base, mayor que cero.</param>
    /// <param name="fecha">Día para el que se resuelve: la fecha del documento.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<PrecioPropuesto> DeCompraAsync(
        Guid tarifaId,
        Guid articuloId,
        decimal cantidad,
        DateOnly fecha,
        CancellationToken cancelacion);
}
