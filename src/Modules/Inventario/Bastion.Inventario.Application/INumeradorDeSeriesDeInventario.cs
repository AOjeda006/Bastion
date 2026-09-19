using Bastion.BuildingBlocks.Application.Numeracion;

namespace Bastion.Inventario.Application;

/// <summary>El numerador de series del módulo Inventario.</summary>
/// <remarks>
/// Es propio del módulo, como la unidad de trabajo y por el mismo motivo: el número se toma con una
/// sentencia que corre en la transacción del contexto de <b>este</b> módulo, y un
/// <c>INumeradorDeSerie</c> compartido entregaría el del último módulo registrado — es decir, un
/// número tomado en una transacción distinta de la del documento que lo lleva.
/// </remarks>
public interface INumeradorDeSeriesDeInventario : INumeradorDeSerie;
