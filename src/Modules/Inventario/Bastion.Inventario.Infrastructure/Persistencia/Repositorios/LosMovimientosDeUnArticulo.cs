using Bastion.Inventario.Contracts.Movimientos;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Inventario.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IMovimientosDeArticulos"/>
/// <remarks>
/// <para>
/// Lo que Inventario contesta cuando Catálogo quiere cambiar la marca de un artículo. Vive aquí, en
/// el módulo dueño del libro, y se resuelve en proceso: Catálogo llama a un método y no sabe que
/// detrás hay un <c>DbContext</c> (§4, reglas de frontera 1 y 3).
/// </para>
/// <para>
/// <b>La empresa la pone el filtro global</b>, que alcanza a esta consulta porque va por el ORM.
/// La sirve el índice del libro que empieza por la empresa y el artículo.
/// </para>
/// </remarks>
/// <param name="contexto">El contexto de Inventario.</param>
internal sealed class LosMovimientosDeUnArticulo(InventarioDbContext contexto)
    : IMovimientosDeArticulos
{
    /// <inheritdoc/>
    public Task<bool> TieneMovimientosAsync(Guid articuloId, CancellationToken cancelacion) =>
        contexto.Movimientos.AnyAsync(
            movimiento => movimiento.ArticuloId == articuloId, cancelacion);
}
