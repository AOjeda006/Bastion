using Bastion.BuildingBlocks.Domain.Dinero;

namespace Bastion.Inventario.Domain.Valoraciones;

/// <summary>
/// Cuánto hay de una clave, cuánto vale y de qué fecha es su último movimiento, tal como lo dejó la
/// última confirmación (ADR-0046 §5, ADR-0047).
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
/// <para>
/// <b>La fecha va con el saldo porque se lee con él</b>, de la misma fila bloqueada (ADR-0047 §2).
/// Leída aparte, dos documentos de la misma clave con las fechas cruzadas no se verían, y los dos
/// pasarían.
/// </para>
/// <para>
/// <b>Y el tránsito, por la divisa</b> (ADR-0053 §1). Lo que vuela hacia la clave está en su fila,
/// fuera de la cantidad y del valor, y en la divisa de la valoración. Una clave con tránsito no está
/// vacía, así que no empieza de nuevo en otra divisa.
/// </para>
/// </remarks>
public sealed record SaldoValorado
{
    /// <summary>Un saldo con su cantidad en unidad base, su valor y su último movimiento.</summary>
    /// <param name="cantidad">La cantidad en unidad base, la suma de las filas del libro de la clave.</param>
    /// <param name="valor">El valor, la suma del valor de esas mismas filas.</param>
    /// <param name="ultimaFecha">
    /// La fecha de operación más alta de esas filas, o <see langword="null"/> si la clave no se ha
    /// movido nunca.
    /// </param>
    /// <param name="enTransito">Lo que vuela hacia la clave, en unidad base. No está en el libro.</param>
    public SaldoValorado(decimal cantidad, Importe valor, DateOnly? ultimaFecha = null, decimal enTransito = 0m)
    {
        ArgumentNullException.ThrowIfNull(valor);
        ArgumentOutOfRangeException.ThrowIfNegative(cantidad);
        ArgumentOutOfRangeException.ThrowIfNegative(valor.Cantidad, nameof(valor));
        ArgumentOutOfRangeException.ThrowIfNegative(enTransito);

        if (cantidad == 0m && valor.Cantidad != 0m)
        {
            throw new ArgumentException(
                $"Sin cantidad no hay valor: {valor.Cantidad} {valor.Divisa} sin una sola unidad " +
                "que los sostenga no es un saldo.",
                nameof(valor));
        }

        Cantidad = cantidad;
        Valor = valor;
        UltimaFecha = ultimaFecha;
        EnTransito = enTransito;
    }

    /// <summary>La cantidad en unidad base.</summary>
    public decimal Cantidad { get; }

    /// <summary>El valor de esa cantidad, en la divisa de la valoración.</summary>
    public Importe Valor { get; }

    /// <summary>
    /// La fecha del último movimiento de la clave. Ningún documento puede llevar una anterior
    /// (ADR-0047), y es <see langword="null"/> en una clave que no se ha movido nunca.
    /// </summary>
    public DateOnly? UltimaFecha { get; }

    /// <summary>Lo que vuela hacia la clave, en unidad base: el tránsito de su valoración.</summary>
    public decimal EnTransito { get; }

    /// <summary>
    /// Sin cantidad y sin tránsito: lo único que puede empezar de nuevo en otra divisa (ADR-0046 §7).
    /// </summary>
    public bool EstaVacio => Cantidad == 0m && EnTransito == 0m;

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
