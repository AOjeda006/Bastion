using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.Terceros.Application;

namespace Bastion.Terceros.Infrastructure.Persistencia;

/// <summary>Las versiones del módulo, sobre su propio <see cref="TercerosDbContext"/>.</summary>
internal sealed class VersionesDeTerceros(TercerosDbContext contexto)
    : Versiones(contexto), IVersionesDeTerceros;
