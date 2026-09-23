namespace Bastion.Inventario.Domain.Movimientos;

/// <summary>
/// Qué clase de documento escribió una fila del libro. La mitad «movimiento → documento» de la
/// R13.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es una lista cerrada y hoy tiene un solo valor</b>, que es el único documento que existe.
/// La transferencia entra en el 2.11 y el recuento en el 2.12; cada uno añade su valor y su caso.
/// El 2.5 NO añade ninguno, y esa ausencia es la decisión: un ajuste inverso es un ajuste, así
/// que sus filas salen con este mismo valor — que es lo que permite sumar el par entero de una
/// vez, en vez de tener que unir dos clases de fila para comprobar que se compensan.
/// Un valor que ningún productor produce es el defecto del ítem 1.10 y lo pone rojo
/// <c>LaMatrizDeLosPuertosDeEstadoTests</c>, así que la lista no se adelanta a los ítems.
/// </para>
/// <para>
/// <b>Por qué un enumerado y no una clave ajena.</b> Los documentos de inventario viven en tablas
/// distintas —un ajuste no es un recuento— y una fila del libro apunta a una de ellas. Ninguna
/// clave ajena puede expresar «apunta a la tabla que diga esta otra columna», así que la
/// integridad de esa flecha no la sostiene el motor: la sostiene la regla que la comprueba, y eso
/// está dicho en la fila de la R13 de <c>docs/dominio/reglas-duras.md</c> con su nombre.
/// </para>
/// </remarks>
public enum TipoDeDocumentoOrigen
{
    /// <summary>Un ajuste de inventario: la corrección de existencias del ítem 2.3.</summary>
    Ajuste = 1,
}
