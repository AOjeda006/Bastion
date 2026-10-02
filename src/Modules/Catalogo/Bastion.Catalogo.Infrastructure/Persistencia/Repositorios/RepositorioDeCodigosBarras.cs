using Bastion.Catalogo.Application.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Catalogo.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeCodigosBarras"/>
/// <remarks>
/// Las tres consultas van por el filtro de inquilinato del contexto (R8): un código de barras de
/// otra empresa no existe desde aquí, aunque lleve el mismo GTIN.
/// </remarks>
internal sealed class RepositorioDeCodigosBarras(CatalogoDbContext contexto) : IRepositorioDeCodigosBarras
{
    public Task<CodigoBarras?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        contexto.CodigosBarras.FirstOrDefaultAsync(codigo => codigo.Id == id, cancelacion);

    /// <inheritdoc/>
    /// <remarks>
    /// Por unidades, que pone la base delante y cada agrupación detrás de la más pequeña, y no por
    /// el nivel: el nivel se guarda como texto, y su orden sería el del alfabeto. El GTIN y el
    /// identificador desempatan, para que la misma lista pedida dos veces salga igual.
    /// </remarks>
    public async Task<IReadOnlyList<CodigoBarras>> DeArticuloAsync(
        Guid articuloId, CancellationToken cancelacion) =>
        await contexto.CodigosBarras
            .Where(codigo => codigo.ArticuloId == articuloId)
            .OrderBy(codigo => codigo.Unidades)
            .ThenBy(codigo => codigo.Gtin)
            .ThenBy(codigo => codigo.Id)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    public Task<CodigoBarras?> DelGtinAsync(Gtin gtin, CancellationToken cancelacion) =>
        contexto.CodigosBarras.FirstOrDefaultAsync(codigo => codigo.Gtin == gtin, cancelacion);

    public void Agregar(CodigoBarras codigo) => contexto.CodigosBarras.Add(codigo);

    public void Eliminar(CodigoBarras codigo) => contexto.CodigosBarras.Remove(codigo);
}
