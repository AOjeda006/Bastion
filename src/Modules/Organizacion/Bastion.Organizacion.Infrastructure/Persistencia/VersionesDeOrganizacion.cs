using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.Organizacion.Application;

namespace Bastion.Organizacion.Infrastructure.Persistencia;

/// <summary>Las versiones del módulo, sobre su propio <see cref="OrganizacionDbContext"/>.</summary>
internal sealed class VersionesDeOrganizacion(OrganizacionDbContext contexto)
    : Versiones(contexto), IVersionesDeOrganizacion;
