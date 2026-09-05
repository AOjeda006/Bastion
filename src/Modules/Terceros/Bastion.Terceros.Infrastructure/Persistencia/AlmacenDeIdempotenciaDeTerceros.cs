using Bastion.BuildingBlocks.Infrastructure.Idempotencia;

namespace Bastion.Terceros.Infrastructure.Persistencia;

/// <summary>El almacén de claves del módulo, sobre su propio <see cref="TercerosDbContext"/>.</summary>
internal sealed class AlmacenDeIdempotenciaDeTerceros(TercerosDbContext contexto)
    : AlmacenDeIdempotencia(contexto);
