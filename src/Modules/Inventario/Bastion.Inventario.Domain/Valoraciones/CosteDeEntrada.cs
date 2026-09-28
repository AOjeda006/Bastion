using Bastion.BuildingBlocks.Domain.Dinero;

namespace Bastion.Inventario.Domain.Valoraciones;

/// <summary>
/// El coste de una línea que sube, tal como se escribió: tanto por cada unidad en la que se escribió
/// la cantidad, y cuántas de esas unidades.
/// </summary>
/// <remarks>
/// <para>
/// <b>El coste es por la unidad introducida, no por la unidad base.</b> Quien escribe «3 cajas a
/// 18 €» ha escrito el precio de una caja, y la línea guarda la cantidad, la unidad y el coste tal
/// como se escribieron. El valor es <c>18 × 3</c>, redondeado una sola vez en
/// <see cref="PrecioUnitario.Por"/> (R6). El precio medio, en cambio, es por unidad base, porque es
/// lo que suman todas las filas de la clave sea cual sea la unidad en que se escribieron.
/// </para>
/// <para>
/// <b>Por eso el coste lleva sus unidades al lado.</b> Pasarlo a unidad base antes de multiplicar
/// sería dividir por el factor y redondear a seis decimales, y luego volver a redondear el producto:
/// dos redondeos donde la R6 admite uno.
/// </para>
/// </remarks>
public sealed record CosteDeEntrada
{
    /// <summary>Un coste por unidad introducida, y cuántas se introdujeron.</summary>
    /// <param name="porUnidad">El coste de una unidad introducida. Cero vale; negativo no.</param>
    /// <param name="unidades">La cantidad introducida, mayor que cero.</param>
    public CosteDeEntrada(PrecioUnitario porUnidad, decimal unidades)
    {
        ArgumentNullException.ThrowIfNull(porUnidad);

        // EL CERO SÍ ES UN COSTE: una muestra o un regalo del proveedor entra a cero a propósito, y
        // baja el precio medio de lo que había. Lo que no existe es un coste negativo.
        ArgumentOutOfRangeException.ThrowIfNegative(porUnidad.Cantidad, nameof(porUnidad));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(unidades);

        PorUnidad = porUnidad;
        Unidades = unidades;
    }

    /// <summary>El coste de una unidad introducida.</summary>
    public PrecioUnitario PorUnidad { get; }

    /// <summary>La cantidad introducida.</summary>
    public decimal Unidades { get; }

    /// <summary>Lo que la línea suma: el coste por las unidades, redondeado una vez.</summary>
    public Importe Valor => PorUnidad.Por(Unidades);
}
