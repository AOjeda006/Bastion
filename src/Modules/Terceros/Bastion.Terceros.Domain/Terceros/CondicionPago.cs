using Bastion.BuildingBlocks.Domain.Entidades;

namespace Bastion.Terceros.Domain.Terceros;

/// <summary>
/// En cuántos días se paga, con qué día fijo y con qué descuento por pronto pago.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aquí se guarda el PLAZO, no el vencimiento de nada.</b> Terceros no tiene entregas —eso es
/// facturación— así que en este módulo no se calcula ninguna fecha. La ficha dice «a treinta días»;
/// convertir eso en un 14 de marzo necesita saber cuándo se entregó, y eso pasa en otro sitio.
/// </para>
/// <para>
/// <b>Los sesenta días son un INVARIANTE, no un valor por omisión.</b> El artículo 4 de la Ley
/// 3/2004 fija el máximo en sesenta días naturales y dice que <b>no es ampliable por acuerdo entre
/// las partes</b>: un pacto de noventa días es nulo, no es un pacto peor. Por eso el tope no vive
/// en la pantalla ni en el validador del borde, sino aquí: un modelo que admita guardar noventa
/// «porque el cliente lo pidió» está mal aunque ninguna interfaz lo ofrezca, porque a esta fábrica
/// se llega también desde una importación de CSV, desde una migración y desde un test.
/// </para>
/// <para>
/// <b>Y se cuentan desde la ENTREGA o la prestación, no desde la fecha de factura.</b> No es un
/// matiz: fechar la factura tarde era la forma clásica de estirar el plazo, y la ley cerró esa
/// puerta contando desde que se recibió la mercancía. Este módulo no ve entregas, así que no puede
/// comprobarlo — lo que sí puede, y hace, es <b>decir de qué se cuentan</b> en el nombre y aquí,
/// para que quien calcule el vencimiento en la fase 3 no tenga que deducirlo.
/// </para>
/// <para>
/// <b>Lo que este invariante NO alcanza, dicho para que no parezca que sí.</b> Con
/// <see cref="DiaDePagoFijo"/> puesto, el vencimiento efectivo es el primer día de pago que caiga
/// tras el plazo, y esa combinación <b>puede pasarse de sesenta días naturales</b> aunque el plazo
/// almacenado no llegue. Comprobarlo exige aritmética de fechas sobre una entrega concreta, o sea,
/// el cálculo del vencimiento, que es de la fase 3. Lo que corresponde allí es <b>recortar al día
/// sesenta</b>, y queda escrito aquí porque este es el sitio donde se busca.
/// </para>
/// <para>
/// <b>Una por rol, y no una por ficha.</b> El §7.2 dice que un tercero es cliente y proveedor a la
/// vez «constantemente», y las condiciones de las dos caras no tienen por qué parecerse: lo que se
/// concede cobrando no es lo que se acepta pagando. Una sola condición por ficha obligaría a
/// elegir cuál de las dos se guarda, y a migrar un maestro con datos dentro el día que hiciera
/// falta la otra.
/// </para>
/// </remarks>
public sealed class CondicionPago : EntidadBase
{
    /// <summary>
    /// El máximo legal, en días naturales desde la entrega. Artículo 4 de la Ley 3/2004.
    /// </summary>
    public const int DiasMaximosDePlazo = 60;

    /// <summary>Un descuento por pronto pago es un porcentaje, y no pasa del cien por cien.</summary>
    public const decimal DescuentoMaximo = 100m;

    private CondicionPago(
        Guid id,
        Guid terceroId,
        RolDeCondicionPago rol,
        int diasDePlazo,
        int? diaDePagoFijo,
        decimal? descuentoPorProntoPago,
        DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        TerceroId = terceroId;
        Rol = rol;
        DiasDePlazo = diasDePlazo;
        DiaDePagoFijo = diaDePagoFijo;
        DescuentoPorProntoPago = descuentoPorProntoPago;
    }

    // EF Core materializa desde la base sin pasar por las invariantes.
    private CondicionPago()
    {
    }

    /// <summary>Identificador de la condición.</summary>
    public Guid Id { get; private set; }

    /// <summary>La ficha a la que pertenece.</summary>
    public Guid TerceroId { get; private set; }

    /// <summary>De qué cara de la relación es esta condición.</summary>
    public RolDeCondicionPago Rol { get; private set; }

    /// <summary>Días naturales desde la entrega o la prestación.</summary>
    public int DiasDePlazo { get; private set; }

    /// <summary>
    /// El día del mes en que se pagan los vencimientos, si lo hay.
    /// </summary>
    /// <remarks>
    /// De 1 a 28, y el tope no es un error de tecleo: un día 30 no existe en febrero, y un día 31
    /// no existe en cuatro meses del año. Admitirlos obliga a decidir en el cálculo si se adelanta
    /// o se atrasa, y esa decisión cambia quién paga intereses de demora. Con 28 la fecha existe
    /// siempre y no hay nada que decidir.
    /// </remarks>
    public int? DiaDePagoFijo { get; private set; }

    /// <summary>Porcentaje de descuento si se paga antes, si lo hay.</summary>
    public decimal? DescuentoPorProntoPago { get; private set; }

    /// <summary>Declara la condición.</summary>
    /// <param name="terceroId">La ficha a la que se cuelga.</param>
    /// <param name="rol">Si es la condición como cliente o como proveedor.</param>
    /// <param name="diasDePlazo">Días naturales desde la entrega. Como mucho, sesenta.</param>
    /// <param name="diaDePagoFijo">Día del mes en que se paga, de 1 a 28.</param>
    /// <param name="descuentoPorProntoPago">Porcentaje de descuento por pagar antes.</param>
    /// <param name="momento">Cuándo, del reloj inyectado.</param>
    public static CondicionPago Crear(
        Guid terceroId,
        RolDeCondicionPago rol,
        int diasDePlazo,
        int? diaDePagoFijo,
        decimal? descuentoPorProntoPago,
        DateTimeOffset momento)
    {
        if (terceroId == Guid.Empty)
        {
            throw new ArgumentException(
                "Una condición de pago cuelga siempre de una ficha.", nameof(terceroId));
        }

        if (!Enum.IsDefined(rol))
        {
            throw new ArgumentOutOfRangeException(
                nameof(rol), rol, "El rol tiene que ser cliente o proveedor.");
        }

        return new CondicionPago(
            Guid.CreateVersion7(),
            terceroId,
            rol,
            PlazoValido(diasDePlazo),
            DiaValido(diaDePagoFijo),
            DescuentoValido(descuentoPorProntoPago),
            momento);
    }

    /// <summary>Cambia el plazo, el día fijo y el descuento. El rol, no.</summary>
    /// <param name="diasDePlazo">Días naturales desde la entrega. Como mucho, sesenta.</param>
    /// <param name="diaDePagoFijo">Día del mes en que se paga, de 1 a 28.</param>
    /// <param name="descuentoPorProntoPago">Porcentaje de descuento por pagar antes.</param>
    public void Modificar(int diasDePlazo, int? diaDePagoFijo, decimal? descuentoPorProntoPago)
    {
        DiasDePlazo = PlazoValido(diasDePlazo);
        DiaDePagoFijo = DiaValido(diaDePagoFijo);
        DescuentoPorProntoPago = DescuentoValido(descuentoPorProntoPago);
    }

    private static int PlazoValido(int dias) =>
        dias switch
        {
            < 0 => throw new ArgumentOutOfRangeException(
                nameof(dias), dias, "Un plazo de pago no puede ser negativo."),
            > DiasMaximosDePlazo => throw new ArgumentOutOfRangeException(
                nameof(dias),
                dias,
                $"El plazo máximo de pago son {DiasMaximosDePlazo} días naturales desde la " +
                "entrega (art. 4 de la Ley 3/2004), y no es ampliable por acuerdo entre las " +
                "partes: un plazo mayor no es una condición peor, es una cláusula nula."),
            _ => dias,
        };

    private static int? DiaValido(int? dia) =>
        dia is null or >= 1 and <= 28
            ? dia
            : throw new ArgumentOutOfRangeException(
                nameof(dia),
                dia,
                "El día de pago fijo va del 1 al 28: los días 29, 30 y 31 no existen en todos " +
                "los meses, y elegir si se adelantan o se atrasan cambia quién paga la demora.");

    private static decimal? DescuentoValido(decimal? descuento) =>
        descuento is null or >= 0m and <= DescuentoMaximo
            ? descuento
            : throw new ArgumentOutOfRangeException(
                nameof(descuento),
                descuento,
                $"El descuento por pronto pago es un porcentaje entre 0 y {DescuentoMaximo}.");
}
