using Bastion.BuildingBlocks.Infrastructure.Idempotencia;

namespace Bastion.Inventario.Infrastructure.Persistencia;

/// <summary>El almacén de claves del módulo, sobre su propio <see cref="InventarioDbContext"/>.</summary>
internal sealed class AlmacenDeIdempotenciaDeInventario(InventarioDbContext contexto)
    : AlmacenDeIdempotencia(contexto);
