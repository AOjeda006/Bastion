using Bastion.BuildingBlocks.Domain.Multiempresa;

namespace Bastion.Inventario.Domain.Existencias;

/// <summary>
/// El saldo de una existencia al cierre de un mes: la suma del libro hasta el último día de ese
/// mes (ADR-0044).
/// </summary>
/// <remarks>
/// <para>
/// <b>Es una optimización y se puede tirar.</b> Borrar la tabla entera y recalcularla desde el libro
/// no cambia un número, y hay un caso que lo hace. Existe para que el ítem 2.14 conteste el saldo
/// a una fecha pasada sin recorrer el libro desde el principio: la instantánea del mes anterior
/// más lo que se movió desde entonces.
/// </para>
/// <para>
/// <b>El mes es el mismo límite que la partición del libro</b>, el primer día de cada mes. Y la
/// tabla es densa: una fila por mes desde el primero en que la existencia se movió hasta el corte
/// de la empresa, aunque ese mes no se moviera nada. Así la lectura es siempre una fila, y no
/// «la última que haya antes de esta fecha».
/// </para>
/// <para>
/// <b>Un movimiento atrasado no es un caso aparte.</b> La sentencia que anota el libro le suma su
/// cantidad a todas las instantáneas de su existencia desde su mes hasta el corte, y crea las que
/// falten. Lo que entra en un ejercicio reabierto es un movimiento atrasado más.
/// </para>
/// </remarks>
public sealed class InstantaneaMensual : IDeInquilino
{
    /// <summary>Constructor de materialización: EF Core rellena la fila.</summary>
    private InstantaneaMensual()
    {
    }

    /// <summary>La existencia de la que es el saldo.</summary>
    public Guid ExistenciaId { get; private set; }

    /// <summary>El primer día del mes cuyo cierre guarda.</summary>
    public DateOnly Mes { get; private set; }

    /// <inheritdoc/>
    /// <remarks>
    /// Es la de su existencia, repetida aquí para que el filtro de inquilinato y el recálculo de una
    /// empresa no tengan que cruzar tablas.
    /// </remarks>
    public Guid EmpresaId { get; private set; }

    /// <summary>La suma del libro de esa existencia hasta el último día del mes.</summary>
    public decimal Fisico { get; private set; }
}
