using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.Catalogo.Application;

namespace Bastion.Catalogo.Infrastructure.Persistencia;

/// <summary>Las versiones del módulo, sobre su propio <see cref="CatalogoDbContext"/>.</summary>
internal sealed class VersionesDeCatalogo(CatalogoDbContext contexto)
    : Versiones(contexto), IVersionesDeCatalogo;
