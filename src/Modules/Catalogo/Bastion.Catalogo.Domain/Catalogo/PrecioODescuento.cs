using Bastion.BuildingBlocks.Domain.Dinero;

namespace Bastion.Catalogo.Domain.Catalogo;

/// <summary>
/// Lo que una línea de tarifa dice del artículo al que se aplica: un precio, <b>o</b> un descuento.
/// Nunca los dos, y nunca ninguno.
/// </summary>
/// <remarks>
/// <para>
/// <b>La exclusividad vive aquí, en el objeto de valor</b>, y no en el caso de uso ni en el
/// controlador: es lo que pide el criterio del ítem, y el motivo es que una línea de tarifa se
/// puede construir por más caminos de los que hay hoy —una importación CSV en el 1.11, una copia
/// de tarifa, una semilla—. Una regla escrita en el caso de uso protege el camino que pasa por él;
/// escrita aquí no hay forma de tener en las manos un <see cref="PrecioODescuento"/> que la
/// incumpla.
/// </para>
/// <para>
/// <b>Los dos negativos son dos, y el segundo es el que se olvida.</b> Que los dos vengan puestos
/// se rechaza porque son dos maneras de decir lo mismo con un orden de aplicación invisible: nadie
/// sabría si el descuento se aplica sobre el precio de la línea o sobre el de la anterior. Que no
/// venga <b>ninguno</b> se rechaza porque es el camino al <b>precio cero por la puerta de
/// atrás</b>: una línea vacía casa con el artículo, gana la precedencia y devuelve un importe de
/// cero euros que nadie escribió. Es la decisión 3 del ADR-0023 con otro sujeto —donde el hueco
/// importa, el valor por omisión es el error— y el motivo de que el estado por omisión de esta
/// estructura no sea válido.
/// </para>
/// <para>
/// <b>El precio no lleva divisa, y no es un descuido.</b> La divisa es de la <see cref="Tarifa"/>
/// entera (§7.3) y no de cada línea: repetirla en cada fila sería una segunda verdad que se separa
/// el día que alguien corrija una sola. Quien compone el <see cref="PrecioUnitario"/> con su código
/// ISO es quien resuelve el precio, que tiene la tarifa delante.
/// </para>
/// <para>
/// La escala es la de precio unitario de la R6 —<see cref="PrecioUnitario.Decimales"/>, seis
/// decimales—, no la de importe: un precio de tarifa se multiplica por una cantidad antes de ser
/// dinero, y redondear antes de multiplicar es exactamente lo que la R6 separa en dos escalas.
/// </para>
/// </remarks>
public sealed record PrecioODescuento
{
    /// <summary>Decimales del porcentaje de descuento.</summary>
    /// <remarks>
    /// Dos, como se escriben los descuentos comerciales («un 12,50 %») y como se teclean en un
    /// acuerdo. Más decimales no los usa nadie y darían la impresión de que el descuento tiene una
    /// precisión que el precio resultante no puede conservar.
    /// </remarks>
    public const int DecimalesDelDescuento = 2;

    // Los nombres de los parámetros CASAN con los de las propiedades, y no es estética: el tipo se
    // mapea como tipo complejo y EF Core materializa por este constructor emparejando por nombre,
    // igual que `Direccion`. Un `descuento` a secas dejaría de casar con `DescuentoPorcentaje` y el
    // fallo saldría al leer la primera fila, no al compilar.
    private PrecioODescuento(decimal? precio, decimal? descuentoPorcentaje) =>
        (Precio, DescuentoPorcentaje) = (precio, descuentoPorcentaje);

    /// <summary>Precio por unidad en la divisa de la tarifa, o nulo si la línea es de descuento.</summary>
    public decimal? Precio { get; }

    /// <summary>
    /// Descuento en tanto por ciento —el 12,5 % es <c>12,5</c>—, o nulo si la línea es de precio.
    /// </summary>
    /// <remarks>
    /// En tanto por ciento y no en tanto por uno, por lo mismo que el porcentaje de un impuesto: se
    /// guarda como aparece escrito en el acuerdo comercial, que es como lo comprueba quien lo
    /// reclama. Dividir entre cien es cosa del cálculo, y hacerlo en un solo sitio quita la duda de
    /// si un <c>0,125</c> guardado era el descuento o ya el factor.
    /// </remarks>
    public decimal? DescuentoPorcentaje { get; }

    /// <summary>Una línea que fija el precio por unidad.</summary>
    /// <param name="precio">Precio por unidad, en la divisa de la tarifa.</param>
    /// <exception cref="ArgumentOutOfRangeException">El precio es negativo.</exception>
    public static PrecioODescuento DePrecio(decimal precio)
    {
        // El cero SÍ es un precio válido, y hay que dejarlo pasar: una muestra comercial o un
        // artículo de regalo se factura a cero a propósito, con su línea en el documento. Lo que el
        // ítem prohíbe no es el precio cero, es el precio cero que NADIE ESCRIBIÓ — que es el que
        // sale de una línea sin precio ni descuento, o de una resolución sin línea aplicable.
        if (precio < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(precio),
                precio,
                "Un precio de tarifa no es negativo. Cobrar menos que nada es un descuento o un " +
                "abono, y los dos tienen su sitio y su signo.");
        }

        return new PrecioODescuento(
            Math.Round(precio, PrecioUnitario.Decimales, MidpointRounding.AwayFromZero), null);
    }

    /// <summary>Una línea que fija un descuento sobre el precio que traiga el artículo.</summary>
    /// <param name="porcentaje">Descuento en tanto por ciento.</param>
    /// <exception cref="ArgumentOutOfRangeException">El descuento no está entre 0 y 100.</exception>
    public static PrecioODescuento DeDescuento(decimal porcentaje)
    {
        if (porcentaje is < 0m or > 100m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(porcentaje),
                porcentaje,
                "Un descuento va del 0 al 100 por ciento. Por encima de cien el importe cambiaría " +
                "de signo, y un negativo es un recargo: ninguno de los dos se declara aquí.");
        }

        return new PrecioODescuento(
            null, Math.Round(porcentaje, DecimalesDelDescuento, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Construye el objeto de valor a partir del par que llega de fuera, exigiendo que venga
    /// exactamente uno de los dos.
    /// </summary>
    /// <remarks>
    /// <b>Es la puerta doble del ADR-0004 por el lado de dentro.</b> Aquí se lanza, porque llegar
    /// con los dos puestos o con ninguno es un programa mal escrito. Quien recibe el par de una
    /// petición HTTP no llama a esto a ciegas: contesta antes con un error de negocio con nombre,
    /// que es lo que hace <c>ErroresDeTarifa.PrecioODescuento</c>. Las dos comprobaciones dicen lo
    /// mismo y ninguna sobra: una protege a quien usa la API y la otra a quien usa el tipo.
    /// </remarks>
    /// <param name="precio">Precio por unidad, o nulo.</param>
    /// <param name="descuentoPorcentaje">Descuento en tanto por ciento, o nulo.</param>
    /// <exception cref="ArgumentException">Vienen los dos, o no viene ninguno.</exception>
    public static PrecioODescuento De(decimal? precio, decimal? descuentoPorcentaje)
    {
        if (precio is not null && descuentoPorcentaje is not null)
        {
            throw new ArgumentException(
                "Una línea de tarifa fija un precio o un descuento, no los dos: con los dos " +
                "puestos, el orden en que se aplican no lo dice nadie.",
                nameof(precio));
        }

        if (precio is { } importe)
        {
            return DePrecio(importe);
        }

        if (descuentoPorcentaje is { } porcentaje)
        {
            return DeDescuento(porcentaje);
        }

        throw new ArgumentException(
            "Una línea de tarifa sin precio y sin descuento no dice nada, y lo que devolvería al " +
            "aplicarse es un cero que nadie ha escrito.",
            nameof(precio));
    }
}
