namespace Bastion.Inventario.Domain.Recuentos;

/// <summary>Los cuatro estados por los que pasa un recuento (ADR-0055 §13).</summary>
/// <remarks>
/// Es un enumerado <b>propio</b>, como el del ajuste y el de la transferencia. El recuento no tiene
/// borrador: mientras está <see cref="EnCurso"/> se cuenta, y contar no es escribir un documento.
/// </remarks>
public enum EstadoDeRecuento
{
    /// <summary>Se está contando. No tiene número, no cuenta para el ejercicio y ocupa su almacén.</summary>
    EnCurso = 1,

    /// <summary>
    /// Confirmado: tiene número, y la diferencia la movió su ajuste, en la misma transacción. Si todo
    /// cuadraba, no hay ajuste.
    /// </summary>
    Confirmado = 2,

    /// <summary>
    /// Anulado, <b>con su ajuste</b>: el inverso de ese ajuste compensa lo que movió (ADR-0055 §9). Si
    /// no movió nada, solo cambió de estado.
    /// </summary>
    Anulado = 3,

    /// <summary>
    /// Descartado en curso, con su motivo: no movió nada, no se numeró y deja el almacén libre para
    /// otro (ADR-0055 §1.6).
    /// </summary>
    Descartado = 4,
}
