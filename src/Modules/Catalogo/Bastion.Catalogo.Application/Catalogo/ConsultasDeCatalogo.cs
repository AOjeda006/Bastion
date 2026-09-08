using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Application.Comun;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>Devuelve un artículo por su identificador.</summary>
public interface IObtenerArticulo
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del artículo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ConVersion<ArticuloDto>>> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <summary>
/// Devuelve una página de artículos, opcionalmente acotada a una categoría.
/// </summary>
/// <remarks>
/// <b>No es un <c>IListado&lt;ArticuloDto&gt;</c>, y no por capricho.</b> Ese contrato dice
/// «paginación entra, página sale», y aquí entra además una categoría. Meterla en
/// <c>Paginacion</c> —que es del bloque común— se la publicaría a los doce listados del sistema
/// como un parámetro que ninguno mira: una mentira del contrato que contesta <c>200</c> y no falla
/// nunca, que es la peor clase. Lo que sí se comparte es <see cref="IOrdenaPor"/>, que es lo que
/// el borde necesita para validar el <c>?sort=</c> en un solo sitio.
/// </remarks>
public interface IListarArticulos : IOrdenaPor
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="paginacion">Qué página, de qué tamaño, con qué orden y qué filtro.</param>
    /// <param name="categoriaId">Categoría por la que se acota, o nula para no acotar.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<PaginaDe<ArticuloDto>> EjecutarAsync(
        Paginacion paginacion,
        Guid? categoriaId,
        CancellationToken cancelacion);
}

/// <summary>Devuelve una categoría por su identificador.</summary>
public interface IObtenerCategoria
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la categoría.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ConVersion<CategoriaDto>>> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <summary>Devuelve una página de categorías.</summary>
public interface IListarCategorias : IListado<CategoriaDto>
{
}

/// <inheritdoc cref="IObtenerArticulo"/>
internal sealed class ObtenerArticulo(
    IRepositorioDeArticulos articulos,
    IVersionesDeCatalogo versiones) : IObtenerArticulo
{
    public async Task<Resultado<ConVersion<ArticuloDto>>> EjecutarAsync(
        Guid id,
        CancellationToken cancelacion)
    {
        Articulo? articulo = await articulos.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        return articulo is null
            ? Resultado.Fallo<ConVersion<ArticuloDto>>(ErroresDeArticulo.NoEncontrado(id))
            : Resultado.Correcto(
                new ConVersion<ArticuloDto>(articulo.ADto(), versiones.De(articulo)));
    }
}

/// <inheritdoc cref="IListarArticulos"/>
internal sealed class ListarArticulos(IRepositorioDeArticulos articulos) : IListarArticulos
{
    public IReadOnlySet<string> CamposOrdenables => articulos.CamposOrdenables;

    public async Task<PaginaDe<ArticuloDto>> EjecutarAsync(
        Paginacion paginacion,
        Guid? categoriaId,
        CancellationToken cancelacion)
    {
        PaginaDe<Articulo> pagina = await articulos
            .ListarAsync(paginacion, categoriaId, cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<ArticuloDto>(
            [.. pagina.Elementos.Select(articulo => articulo.ADto())],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}

/// <inheritdoc cref="IObtenerCategoria"/>
internal sealed class ObtenerCategoria(
    IRepositorioDeCategorias categorias,
    IVersionesDeCatalogo versiones) : IObtenerCategoria
{
    public async Task<Resultado<ConVersion<CategoriaDto>>> EjecutarAsync(
        Guid id,
        CancellationToken cancelacion)
    {
        Categoria? categoria = await categorias.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        return categoria is null
            ? Resultado.Fallo<ConVersion<CategoriaDto>>(ErroresDeCategoria.NoEncontrada(id))
            : Resultado.Correcto(
                new ConVersion<CategoriaDto>(categoria.ADto(), versiones.De(categoria)));
    }
}

/// <inheritdoc cref="IListarCategorias"/>
internal sealed class ListarCategorias(IRepositorioDeCategorias categorias) : IListarCategorias
{
    public IReadOnlySet<string> CamposOrdenables => categorias.CamposOrdenables;

    public async Task<PaginaDe<CategoriaDto>> EjecutarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion)
    {
        PaginaDe<Categoria> pagina = await categorias
            .ListarAsync(paginacion, cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<CategoriaDto>(
            [.. pagina.Elementos.Select(categoria => categoria.ADto())],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}
