using Bastion.BuildingBlocks.Application.Numeracion;

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
/// <b>Los documentos se nombran con <see cref="DocumentoQueNumera"/></b>, y no con el enumerado que
/// dice qué documento escribió una fila del libro: hasta el 2.11 eran la misma lista, y el recuento
/// las separó, porque numera y no escribe (ADR-0055 §10). Cada documento que entre en la lista
/// tendrá que decir en qué series numera, o el numerador lanzará.
/// </para>
/// </remarks>
public interface INumeradorDeSeriesDeInventario : INumeradorDeSerie<DocumentoQueNumera>;
