namespace Bastion.Inventario.Application;

/// <summary>En qué series numera cada documento del módulo.</summary>
/// <remarks>
/// <para>
/// <b>El tipo de la serie es un enumerado de <c>Organizacion.Domain</c>, que desde aquí no se ve</b>,
/// así que se escribe su nombre. Que siga siendo el que la columna guarda lo comprueba
/// <c>LosDocumentosDeInventarioNumeranEnSusSeriesTests</c> contra el enumerado de Organización.
/// </para>
/// <para>
/// <b>Vive en Application desde el ítem 2.13, y no en el numerador</b>, porque desde entonces lo
/// leen dos: el numerador, que lo pone en el <c>WHERE</c> de la sentencia que toma el número y es
/// la garantía de la R5, y el alta del recuento, que lo pregunta antes de que nadie cuente nada y
/// es la cortesía (ADR-0055 §1.3). Si cada uno tuviera su copia, la cortesía podría aceptar la
/// serie que la regla rechaza.
/// </para>
/// <para>
/// <b>Lanza con un documento que no esté aquí</b>, y no numera en ninguna serie por defecto: el
/// día que un valor exista sin su línea aquí, el caso que recorre el enumerado entero se pone
/// rojo antes que nada en la base. Así entró la transferencia, en el 2.11. Su inverso numera en
/// la misma serie que ella, como el del ajuste (ADR-0053 §5). Y así entró el recuento, en el 2.12
/// (ADR-0055 §10).
/// </para>
/// </remarks>
public static class SeriesDeInventario
{
    /// <summary>El tipo de las series en las que numera ese documento.</summary>
    /// <param name="documento">El documento que pide el número.</param>
    /// <returns>El valor de <c>tipo_de_documento</c> de sus series.</returns>
    public static string De(DocumentoQueNumera documento) => documento switch
    {
        DocumentoQueNumera.Ajuste => "AjusteDeInventario",
        DocumentoQueNumera.Transferencia => "TransferenciaDeInventario",
        DocumentoQueNumera.Recuento => "RecuentoDeInventario",
        _ => throw new ArgumentOutOfRangeException(
            nameof(documento),
            documento,
            "Este documento de inventario no dice en qué series numera."),
    };
}
