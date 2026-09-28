using Bastion.BuildingBlocks.Domain.Dinero;

namespace Bastion.Inventario.Domain.Valoraciones;

/// <summary>
/// Cuánto hay de una clave y cuánto vale, tal como lo dejó la última confirmación (ADR-0046 §5).
/// </summary>
/// <remarks>
/// <para>
/// <b>El valor es la verdad y el precio medio se deduce.</b> Si se guardara el precio medio
/// redondeado y el valor se recalculara como precio por cantidad, el valor se desviaría un poco en
/// cada movimiento, y la desviación no la vería nadie.
/// </para>
/// <para>
/// <b>Las tres guardas son las tres restricciones de la tabla</b>: la cantidad no baja de cero, el
/// valor tampoco, y sin cantidad no hay valor. Un saldo que llegue sin cumplirlas no se ha leído de
/// la tabla, y valorar contra él sería valorar contra algo que no existe.
/// </para>
/// </remarks>
public sealed record SaldoValorado
{
    /// <summary>Un saldo con su cantidad en unidad base y su valor.</summary>
    /// <param name="cantidad">La cantidad en unidad base, la suma de las filas del libro de la clave.</param>
    /// <param name="valor">El valor, la suma del valor de esas mismas filas.</param>
    public SaldoValorado(decimal cantidad, Importe valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        ArgumentOutOfRangeException.ThrowIfNegative(cantidad);
        ArgumentOutOfRangeException.ThrowIfNegative(valor.Cantidad, nameof(valor));

        if (cantidad == 0m && valor.Cantidad != 0m)
        {
            throw new ArgumentException(
                $"Sin cantidad no hay valor: {valor.Cantidad} {valor.Divisa} sin una sola unidad " +
                "que los sostenga no es un saldo.",
                nameof(valor));
        }

        Cantidad = cantidad;
        Valor = valor;
    }

    /// <summary>La cantidad en unidad base.</summary>
    public decimal Cantidad { get; }

    /// <summary>El valor de esa cantidad, en la divisa de la valoración.</summary>
    public Importe Valor { get; }

    /// <summary>
    /// El valor por unidad, redondeado a la escala de <see cref="PrecioUnitario"/>. Sin cantidad no
    /// existe.
    /// </summary>
    public PrecioUnitario? PrecioMedio =>
        Cantidad > 0m ? PrecioUnitario.De(Valor.Cantidad / Cantidad, Valor.Divisa) : null;

    /// <summary>El saldo de una clave que no se ha movido nunca.</summary>
    /// <param name="divisa">La divisa en la que empieza.</param>
    /// <returns>Cantidad cero y valor cero.</returns>
    public static SaldoValorado Vacio(string divisa) => new(0m, Importe.Cero(divisa));
}
