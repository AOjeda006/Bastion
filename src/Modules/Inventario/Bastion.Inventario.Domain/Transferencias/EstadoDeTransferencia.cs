namespace Bastion.Inventario.Domain.Transferencias;

/// <summary>Los estados de una transferencia entre almacenes (R1, ADR-0053 §12).</summary>
/// <remarks>
/// <para>
/// <b>Es un enumerado propio y no el del ajuste</b>, como pedía el de los ajustes: una transferencia
/// tiene dos momentos que escriben en el libro, y entre los dos la mercancía vuela. Un
/// <c>Confirmado</c> no diría si ya llegó.
/// </para>
/// <para>
/// <b>Las transiciones</b> son <c>Borrador → Enviada → Recibida</c>, y <c>Enviada</c> o
/// <c>Recibida → Anulada</c>. El inverso con el que se anula nace <c>Recibida</c>: lo que mueve lo
/// mueve de una vez, y no deja nada en vuelo (ADR-0053 §5).
/// </para>
/// </remarks>
public enum EstadoDeTransferencia
{
    /// <summary>Se escribe y se corrige. No ha movido nada ni gastado número.</summary>
    Borrador = 1,

    /// <summary>Ha salido del origen y vuela: su tránsito está en el destino.</summary>
    Enviada = 2,

    /// <summary>Ha entrado en el destino. No le queda nada en vuelo.</summary>
    Recibida = 3,

    /// <summary>Un inverso la compensa (R2).</summary>
    Anulada = 4,
}
