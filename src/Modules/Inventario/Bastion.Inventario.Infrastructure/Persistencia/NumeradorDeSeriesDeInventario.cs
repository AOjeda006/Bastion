using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Infrastructure.Numeracion;
using Bastion.Inventario.Application;
using Bastion.Inventario.Domain.Movimientos;

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
    : NumeradorDeSerie<TipoDeDocumentoOrigen>(contexto, inquilino), INumeradorDeSeriesDeInventario
{
    /// <summary>El tipo de serie de los ajustes, con el nombre que le da Organización.</summary>
    internal const string SeriesDeAjustes = "AjusteDeInventario";

    /// <summary>En qué series numera cada documento del módulo.</summary>
    /// <remarks>
    /// <b>Lanza con un documento que no esté aquí</b>, y no numera en ninguna serie por defecto: la
    /// transferencia y el recuento entran en el 2.11 y el 2.12, y el día que su valor exista sin su
    /// línea aquí, el caso que recorre el enumerado entero se pone rojo antes que nada en la base.
    /// </remarks>
    /// <param name="documento">El documento que pide el número.</param>
    /// <returns>El valor de <c>tipo_de_documento</c> de sus series.</returns>
    internal static string SeriesDe(TipoDeDocumentoOrigen documento) => documento switch
    {
        TipoDeDocumentoOrigen.Ajuste => SeriesDeAjustes,
        _ => throw new ArgumentOutOfRangeException(
            nameof(documento),
            documento,
            "Este documento de inventario no dice en qué series numera."),
    };

    /// <inheritdoc />
    protected override string TipoDeSerieQueNumera(TipoDeDocumentoOrigen documento) =>
        SeriesDe(documento);
}
