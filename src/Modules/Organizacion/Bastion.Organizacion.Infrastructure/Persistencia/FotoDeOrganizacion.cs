using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Organizacion.Application;

namespace Bastion.Organizacion.Infrastructure.Persistencia;

/// <summary>La foto del módulo, sobre su propio <see cref="OrganizacionDbContext"/> (ADR-0073).</summary>
internal sealed class FotoDeOrganizacion(OrganizacionDbContext contexto) : IFotoDeOrganizacion
{
    /// <inheritdoc />
    public Task<IAsyncDisposable> AbrirAsync(CancellationToken cancelacion) =>
        FotoDeLectura.AbrirAsync(contexto, cancelacion);
}
