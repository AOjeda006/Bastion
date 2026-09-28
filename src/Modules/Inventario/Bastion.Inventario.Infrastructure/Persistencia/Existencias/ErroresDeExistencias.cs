using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.Inventario.Infrastructure.Persistencia.Existencias;

/// <summary>Los desenlaces de negocio que guarda el motor sobre la existencia.</summary>
/// <remarks>
/// <b>Viven en la infraestructura y no en Aplicación</b> porque ningún caso de uso los devuelve: no
/// hay comprobación previa que pueda hacerlo sin que dos salidas simultáneas se la salten juntas. Los
/// declara el módulo para el borde, que es quien traduce la restricción (ADR-0046 §4).
/// </remarks>
internal static class ErroresDeExistencias
{
    internal const string CodigoStockInsuficiente = "stock-insuficiente";

    /// <summary>Una salida, o el inverso de una entrada, dejaría el físico por debajo de cero.</summary>
    /// <remarks>
    /// <b>No dice qué artículo ni qué ubicación</b>, porque la restricción del motor no lo dice: solo
    /// trae su nombre. Un documento con varias líneas del mismo artículo, o con varios artículos, se
    /// rechaza entero, y lo que el cliente necesita saber es que ninguna de sus filas llegó al
    /// libro.
    /// </remarks>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion StockInsuficiente() => ErrorDeOperacion.ReglaDeNegocio(
        CodigoStockInsuficiente,
        "No hay bastante stock en alguna de las ubicaciones del documento: confirmarlo dejaría el " +
        "físico por debajo de cero, y no se ha escrito nada. Si es la anulación de una entrada, sus " +
        "unidades ya han salido: anule antes esas salidas o registre la entrada que falta (R2).");
}
