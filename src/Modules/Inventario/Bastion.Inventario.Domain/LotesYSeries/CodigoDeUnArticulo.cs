namespace Bastion.Inventario.Domain.LotesYSeries;

/// <summary>Un código de lote o de serie <b>de un artículo</b>: lo que identifica la fila.</summary>
/// <remarks>
/// El código solo no identifica nada. Dos proveedores pueden llamar <c>L-01</c> a lotes distintos
/// de artículos distintos, y el índice único de la tabla es por empresa, artículo y código
/// (ADR-0048 §2).
/// </remarks>
/// <param name="ArticuloId">El artículo.</param>
/// <param name="Codigo">El código, ya normalizado por <see cref="CodigoGs1"/>.</param>
public readonly record struct CodigoDeUnArticulo(Guid ArticuloId, string Codigo);
