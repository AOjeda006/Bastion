using Bastion.BuildingBlocks.Infrastructure.Idempotencia;

namespace Bastion.Identidad.Infrastructure.Persistencia;

/// <summary>El almacén de claves del módulo, sobre su propio <see cref="IdentidadDbContext"/>.</summary>
internal sealed class AlmacenDeIdempotenciaDeIdentidad(IdentidadDbContext contexto)
    : AlmacenDeIdempotencia(contexto);
