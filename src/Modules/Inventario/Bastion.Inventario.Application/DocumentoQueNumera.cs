namespace Bastion.Inventario.Application;

/// <summary>Qué documento del módulo pide un número a su serie.</summary>
/// <remarks>
/// <para>
/// <b>No es <c>TipoDeDocumentoOrigen</c>, y eso es la decisión</b> (ADR-0055 §10). Aquel enumerado
/// dice qué documento escribió una fila del libro, y el recuento no escribe ninguna: lo que mueve
/// es el ajuste que genera. Darle un valor allí sería una casilla que ningún productor produce,
/// el defecto del ítem 1.10. Pero el recuento sí numera, así que el numerador necesita su propia
/// lista.
/// </para>
/// <para>
/// <b>Cada valor dice en qué series numera</b> en <c>NumeradorDeSeriesDeInventario.SeriesDe</c>, y
/// el que no lo diga lanza: un documento nuevo no numera por defecto en las series de otro.
/// </para>
/// </remarks>
public enum DocumentoQueNumera
{
    /// <summary>El ajuste, y su inverso, que numera en la misma serie.</summary>
    Ajuste = 1,

    /// <summary>La transferencia, y su inverso, que numera en la misma serie (ADR-0053 §5).</summary>
    Transferencia = 2,

    /// <summary>
    /// El recuento, que numera al confirmarse (ADR-0055 §1.4) aunque no escriba en el libro: lo
    /// escribe su ajuste, que numera en las series de ajustes.
    /// </summary>
    Recuento = 3,
}
