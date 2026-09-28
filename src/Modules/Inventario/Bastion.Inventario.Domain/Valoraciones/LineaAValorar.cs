using Bastion.BuildingBlocks.Domain.Dinero;

namespace Bastion.Inventario.Domain.Valoraciones;

/// <summary>
/// Una línea de un documento, tal como la necesita la valoración: qué clave mueve, cuánto y, si
/// sube, con qué coste.
/// </summary>
/// <remarks>
/// <para>
/// <b>La cantidad va en unidad base y con signo</b>: positiva sube la clave y negativa la baja.
/// Es la misma que la fila del libro, y el precio medio es por unidad base.
/// </para>
/// <para>
/// <b>Una línea que baja no lleva coste</b>, porque su valor es el precio medio, y un coste escrito
/// ahí no se usaría. Tampoco lo lleva negativo. Las dos cosas las rechaza el borde con
/// <c>ajuste-coste-no-valido</c>, así que aquí llegan como defecto de quien llama.
/// </para>
/// <para>
/// <b><see cref="ValorQueCompensa"/> es lo que trae el inverso</b> (ADR-0046 §6): el valor de la
/// línea del original, con el signo cambiado. No se suma a un coste, lo sustituye, y por eso las
/// dos cosas no van juntas.
/// </para>
/// </remarks>
public sealed record LineaAValorar
{
    /// <summary>Una línea con su clave, su cantidad y lo que dice su valor, si dice algo.</summary>
    /// <param name="clave">El artículo y el almacén.</param>
    /// <param name="cantidad">La cantidad en unidad base, con signo y distinta de cero.</param>
    /// <param name="coste">El coste por unidad base de una línea que sube, si lo trae.</param>
    /// <param name="valorQueCompensa">El valor exacto que compensa, si es la línea de un inverso.</param>
    public LineaAValorar(
        ClaveDeValoracion clave,
        decimal cantidad,
        PrecioUnitario? coste = null,
        Importe? valorQueCompensa = null)
    {
        if (cantidad == 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cantidad), "una línea que no mueve nada no tiene nada que valorar");
        }

        if (coste is not null && (cantidad < 0m || coste.Cantidad < 0m))
        {
            throw new ArgumentException(
                "Solo una línea que sube lleva coste, y no negativo: la que baja se valora al " +
                "precio medio (ADR-0046 §7).",
                nameof(coste));
        }

        if (coste is not null && valorQueCompensa is not null)
        {
            throw new ArgumentException(
                "Una línea que compensa un valor no lleva coste: el valor exacto sustituye al " +
                "coste, y con los dos no se sabría cuál manda (ADR-0046 §6).",
                nameof(valorQueCompensa));
        }

        // CERO VALE CON CUALQUIER SIGNO: el inverso de una línea de antes del 2.8, cuyo valor es
        // cero, compensa cero (ADR-0046 §8).
        if (valorQueCompensa is not null && valorQueCompensa.Cantidad * cantidad < 0m)
        {
            throw new ArgumentException(
                "El valor que compensa va con el signo de la cantidad: lo que sube suma valor y lo " +
                "que baja lo resta.",
                nameof(valorQueCompensa));
        }

        Clave = clave;
        Cantidad = cantidad;
        Coste = coste;
        ValorQueCompensa = valorQueCompensa;
    }

    /// <summary>El artículo y el almacén.</summary>
    public ClaveDeValoracion Clave { get; }

    /// <summary>La cantidad en unidad base, con signo.</summary>
    public decimal Cantidad { get; }

    /// <summary>El coste por unidad base, solo en una línea que sube.</summary>
    public PrecioUnitario? Coste { get; }

    /// <summary>El valor exacto que compensa la línea de un inverso, con el signo de la cantidad.</summary>
    public Importe? ValorQueCompensa { get; }
}
