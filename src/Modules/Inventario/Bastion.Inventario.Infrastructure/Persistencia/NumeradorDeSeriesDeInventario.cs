using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Infrastructure.Numeracion;
using Bastion.Inventario.Application;

namespace Bastion.Inventario.Infrastructure.Persistencia;

/// <summary>
/// El numerador del módulo, sobre su propio <see cref="InventarioDbContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sobre el contexto de Inventario aunque la tabla sea de Organización</b>, y eso no es un
/// descuido: la transacción de la petición está abierta en este contexto —la abre el filtro de
/// idempotencia—, así que es el único desde el que el número y el documento caen en el mismo
/// <c>COMMIT</c>. Es la segunda excepción del ADR-0013 y el motivo entero de que exista.
/// </para>
/// <para>
/// <b>Y es quien dice en qué series numera cada documento del módulo.</b> El tipo de la serie es
/// un enumerado de <c>Organizacion.Domain</c>, que desde aquí no se ve, así que se escribe su
/// nombre; que siga siendo el que la columna guarda lo comprueba
/// <c>LosDocumentosDeInventarioNumeranEnSusSeriesTests</c> contra el modelo de EF Core.
/// </para>
/// </remarks>
/// <param name="contexto">El contexto del módulo.</param>
/// <param name="inquilino">De donde sale la empresa que condiciona el incremento.</param>
internal sealed class NumeradorDeSeriesDeInventario(
    InventarioDbContext contexto, IInquilinoActual inquilino)
    : NumeradorDeSerie<DocumentoQueNumera>(contexto, inquilino), INumeradorDeSeriesDeInventario
{
    /// <summary>El tipo de serie de los ajustes, con el nombre que le da Organización.</summary>
    internal const string SeriesDeAjustes = "AjusteDeInventario";

    /// <summary>El tipo de serie de las transferencias, con el nombre que le da Organización.</summary>
    internal const string SeriesDeTransferencias = "TransferenciaDeInventario";

    /// <summary>El tipo de serie de los recuentos, con el nombre que le da Organización.</summary>
    internal const string SeriesDeRecuentos = "RecuentoDeInventario";

    /// <summary>En qué series numera cada documento del módulo.</summary>
    /// <remarks>
    /// <b>Lanza con un documento que no esté aquí</b>, y no numera en ninguna serie por defecto: el
    /// día que un valor exista sin su línea aquí, el caso que recorre el enumerado entero se pone
    /// rojo antes que nada en la base. Así entró la transferencia, en el 2.11. Su inverso numera en
    /// la misma serie que ella, como el del ajuste (ADR-0053 §5). Y así entró el recuento, en el 2.12
    /// (ADR-0055 §10).
    /// </remarks>
    /// <param name="documento">El documento que pide el número.</param>
    /// <returns>El valor de <c>tipo_de_documento</c> de sus series.</returns>
    internal static string SeriesDe(DocumentoQueNumera documento) => documento switch
    {
        DocumentoQueNumera.Ajuste => SeriesDeAjustes,
        DocumentoQueNumera.Transferencia => SeriesDeTransferencias,
        DocumentoQueNumera.Recuento => SeriesDeRecuentos,
        _ => throw new ArgumentOutOfRangeException(
            nameof(documento),
            documento,
            "Este documento de inventario no dice en qué series numera."),
    };

    /// <inheritdoc />
    protected override string TipoDeSerieQueNumera(DocumentoQueNumera documento) =>
        SeriesDe(documento);
}
