using Bastion.Catalogo.Application.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Catalogo.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeProveedoresDeArticulo"/>
/// <remarks>
/// Las tres consultas van por el filtro de inquilinato del contexto (R8): un suministro de otra
/// empresa no existe desde aquí. No hace falta pasarle la empresa a ningún método porque el filtro
/// la toma del ámbito, y hacerlo abriría la puerta a pasarle otra.
/// </remarks>
internal sealed class RepositorioDeProveedoresDeArticulo(CatalogoDbContext contexto)
    : IRepositorioDeProveedoresDeArticulo
{
    public Task<ArticuloProveedor?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        contexto.ProveedoresDeArticulo
            .FirstOrDefaultAsync(suministro => suministro.Id == id, cancelacion);

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// Con orden escrito y desempate por el identificador: sin él, la misma lista pedida dos veces
    /// puede salir distinta y quien la lee no sabe si ha cambiado algo.
    /// </para>
    /// <para>
    /// Y los nulos se ordenan a mano —<c>ReferenciaDelProveedor == null</c> como primera clave— en
    /// vez de dejárselo al motor. PostgreSQL pone los nulos al FINAL en ascendente y otros los
    /// ponen al principio; confiar en eso es escribir en el código una costumbre del motor que el
    /// código no dice.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<ArticuloProveedor>> DeArticuloAsync(
        Guid articuloId, CancellationToken cancelacion) =>
        await contexto.ProveedoresDeArticulo
            .Where(suministro => suministro.ArticuloId == articuloId)
            .OrderBy(suministro => suministro.ReferenciaDelProveedor == null)
            .ThenBy(suministro => suministro.ReferenciaDelProveedor)
            .ThenBy(suministro => suministro.Id)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    public Task<bool> YaLoSuministraAsync(
        Guid articuloId, Guid terceroId, CancellationToken cancelacion) =>
        contexto.ProveedoresDeArticulo
            .AnyAsync(
                suministro => suministro.ArticuloId == articuloId
                    && suministro.TerceroId == terceroId,
                cancelacion);

    public void Agregar(ArticuloProveedor suministro) =>
        contexto.ProveedoresDeArticulo.Add(suministro);

    public void Eliminar(ArticuloProveedor suministro) =>
        contexto.ProveedoresDeArticulo.Remove(suministro);
}
