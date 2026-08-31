using Bastion.BuildingBlocks.Infrastructure.Idempotencia;

namespace Bastion.Organizacion.Infrastructure.Persistencia;

/// <summary>El almacén de claves del módulo, sobre su propio <see cref="OrganizacionDbContext"/>.</summary>
internal sealed class AlmacenDeIdempotenciaDeOrganizacion(OrganizacionDbContext contexto)
    : AlmacenDeIdempotencia(contexto);
