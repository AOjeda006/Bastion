using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>Los desenlaces fallidos de los códigos de barras del artículo.</summary>
internal static class ErroresDeCodigoBarras
{
    /// <summary>Ese GTIN ya lo lleva un artículo de esta empresa (ADR-0051 §5).</summary>
    /// <remarks>
    /// <para>
    /// <b>Lo dan dos caminos, y tienen que dar el mismo cuerpo.</b> La comprobación previa del alta
    /// lo devuelve sin llegar al motor, y el borde lo contesta cuando el índice único
    /// <c>(empresa_id, gtin)</c> para a quien pierde la carrera. El índice solo trae su nombre, así que
    /// el error no lleva parámetros: si los llevara, la respuesta diría por qué camino se llegó.
    /// </para>
    /// <para>
    /// <b>Por eso Infrastructure ve esta clase</b>: la declaración de la restricción vive en
    /// <c>ModuloDeCatalogo</c>, y una sola fábrica es un solo texto. Copiarla allí, como hace
    /// Inventario con los errores que ningún caso de uso devuelve, dejaría dos textos para un
    /// <c>type</c>.
    /// </para>
    /// </remarks>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion Duplicado() => ErrorDeOperacion.Conflicto(
        "codigo-barras-duplicado",
        "Ese GTIN ya lo lleva un artículo de esta empresa, y un GTIN identifica una sola cosa. Si " +
        "ahora es de otro artículo, quítelo antes del que lo lleva.");
}
