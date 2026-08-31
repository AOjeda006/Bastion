using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.Identidad.Application;

namespace Bastion.Identidad.Infrastructure.Persistencia;

/// <summary>Las versiones del módulo, sobre su propio <see cref="IdentidadDbContext"/>.</summary>
internal sealed class VersionesDeIdentidad(IdentidadDbContext contexto)
    : Versiones(contexto), IVersionesDeIdentidad;
