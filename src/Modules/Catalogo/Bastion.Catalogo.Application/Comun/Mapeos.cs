using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;

namespace Bastion.Catalogo.Application.Comun;

/// <summary>
/// Traducción entre las entidades del módulo y sus DTO.
/// </summary>
/// <remarks>
/// <para>
/// A mano y en un solo sitio, igual que en Organización y en Terceros: lo que sale de la API sale
/// porque alguien lo escribió (`patrones/repository-y-dto.md`).
/// </para>
/// <para>
/// El tipo de artículo sale como TEXTO (<c>ToString</c>): un ordinal es un contrato que se rompe
/// solo con reordenar el enumerado, sin que quien lo reordena vea que está rompiendo nada.
/// </para>
/// </remarks>
internal static class Mapeos
{
    internal static ArticuloDto ADto(this Articulo articulo)
    {
        ArgumentNullException.ThrowIfNull(articulo);

        return new ArticuloDto(
            articulo.Id,
            articulo.EmpresaId,
            articulo.Codigo,
            articulo.Descripcion,
            articulo.Tipo.ToString(),
            articulo.UnidadBaseId,
            articulo.ImpuestoPorDefectoId,
            articulo.CategoriaId);
    }

    internal static CategoriaDto ADto(this Categoria categoria)
    {
        ArgumentNullException.ThrowIfNull(categoria);

        return new CategoriaDto(
            categoria.Id,
            categoria.EmpresaId,
            categoria.Codigo,
            categoria.Nombre,
            categoria.PadreId);
    }
}
