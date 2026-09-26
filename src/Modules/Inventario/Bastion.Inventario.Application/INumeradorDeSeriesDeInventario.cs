using Bastion.BuildingBlocks.Application.Numeracion;
using Bastion.Inventario.Domain.Movimientos;

namespace Bastion.Inventario.Application;

/// <summary>El numerador de series del módulo Inventario.</summary>
/// <remarks>
/// <para>
/// Es propio del módulo, como la unidad de trabajo y por el mismo motivo: el número se toma con una
/// sentencia que corre en la transacción del contexto de <b>este</b> módulo, y un
/// <c>INumeradorDeSerie</c> compartido entregaría el del último módulo registrado — es decir, un
/// número tomado en una transacción distinta de la del documento que lo lleva.
/// </para>
/// <para>
/// <b>Los documentos se nombran con <see cref="TipoDeDocumentoOrigen"/></b>, el mismo enumerado
/// que dice qué documento escribió una fila del libro: son la misma lista, y cada documento que
/// entre en ella tendrá que decir en qué series numera o el numerador lanzará.
/// </para>
/// </remarks>
public interface INumeradorDeSeriesDeInventario : INumeradorDeSerie<TipoDeDocumentoOrigen>;
