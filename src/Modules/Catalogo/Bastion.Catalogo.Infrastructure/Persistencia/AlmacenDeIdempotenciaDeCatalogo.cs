using Bastion.BuildingBlocks.Infrastructure.Idempotencia;

namespace Bastion.Catalogo.Infrastructure.Persistencia;

/// <summary>El almacén de claves del módulo, sobre su propio <see cref="CatalogoDbContext"/>.</summary>
internal sealed class AlmacenDeIdempotenciaDeCatalogo(CatalogoDbContext contexto)
    : AlmacenDeIdempotencia(contexto);
