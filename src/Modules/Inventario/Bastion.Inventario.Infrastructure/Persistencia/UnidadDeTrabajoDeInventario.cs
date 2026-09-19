using Bastion.Inventario.Application;

namespace Bastion.Inventario.Infrastructure.Persistencia;

/// <summary>
/// La unidad de trabajo del módulo, sobre su propio <see cref="InventarioDbContext"/>.
/// </summary>
/// <remarks>
/// Una POR MÓDULO, como en los otros cuatro. Aquí además carga con la transacción que dobla la
/// R12 a sabiendas: la confirmación de un ajuste escribe la cabecera del documento y sus filas del
/// libro en la misma llamada, y que sea una sola es lo que impide un ajuste confirmado con el
/// stock sin mover (rompería la R3).
/// </remarks>
/// <param name="contexto">El contexto del módulo.</param>
internal sealed class UnidadDeTrabajoDeInventario(InventarioDbContext contexto)
    : IUnidadTrabajoDeInventario
{
    public Task<int> ConfirmarAsync(CancellationToken cancelacion) =>
        contexto.SaveChangesAsync(cancelacion);
}
