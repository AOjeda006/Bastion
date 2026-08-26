using Bastion.Identidad.Application;

namespace Bastion.Identidad.Infrastructure.Persistencia;

/// <summary>
/// La unidad de trabajo del módulo, sobre su propio <see cref="IdentidadDbContext"/>.
/// </summary>
internal sealed class UnidadDeTrabajoDeIdentidad(IdentidadDbContext contexto) : IUnidadTrabajoDeIdentidad
{
    public Task<int> ConfirmarAsync(CancellationToken cancelacion) =>
        contexto.SaveChangesAsync(cancelacion);
}
