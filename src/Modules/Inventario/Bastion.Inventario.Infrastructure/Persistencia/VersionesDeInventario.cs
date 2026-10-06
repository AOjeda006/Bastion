using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.Inventario.Application;

namespace Bastion.Inventario.Infrastructure.Persistencia;

/// <summary>Las versiones del módulo, sobre su propio <see cref="InventarioDbContext"/>.</summary>
internal sealed class VersionesDeInventario(InventarioDbContext contexto)
    : Versiones(contexto), IVersionesDeInventario;
