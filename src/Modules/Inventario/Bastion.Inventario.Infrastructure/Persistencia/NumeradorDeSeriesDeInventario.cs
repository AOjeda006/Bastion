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
/// <b>En qué series numera cada documento lo dice <see cref="SeriesDeInventario"/></b>, y no este
/// numerador, desde el ítem 2.13: el alta del recuento lo pregunta también, y las dos tienen que
/// leer la misma lista.
/// </para>
/// </remarks>
/// <param name="contexto">El contexto del módulo.</param>
/// <param name="inquilino">De donde sale la empresa que condiciona el incremento.</param>
internal sealed class NumeradorDeSeriesDeInventario(
    InventarioDbContext contexto, IInquilinoActual inquilino)
    : NumeradorDeSerie<DocumentoQueNumera>(contexto, inquilino), INumeradorDeSeriesDeInventario
{
    /// <inheritdoc />
    protected override string TipoDeSerieQueNumera(DocumentoQueNumera documento) =>
        SeriesDeInventario.De(documento);
}
