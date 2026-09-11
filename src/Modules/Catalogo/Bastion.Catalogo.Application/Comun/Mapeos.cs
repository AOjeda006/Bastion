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

    internal static TarifaDto ADto(this Tarifa tarifa)
    {
        ArgumentNullException.ThrowIfNull(tarifa);

        return new TarifaDto(
            tarifa.Id,
            tarifa.EmpresaId,
            tarifa.Codigo,
            tarifa.Nombre,
            tarifa.DivisaId,
            tarifa.VigenteDesde,
            tarifa.VigenteHasta);
    }

    /// <summary>La línea, con el precio y el descuento <b>desplegados</b> en dos campos nulables.</summary>
    /// <remarks>
    /// El objeto de valor no sale tal cual y no es descuido: hacia fuera son dos columnas de una
    /// tabla que alguien rellena, y el contrato de la API es el par. La exclusividad —uno de los
    /// dos, nunca los dos ni ninguno— la sostiene <c>PrecioODescuento</c> hacia dentro y el CHECK
    /// hacia abajo; en el DTO no se puede sostener, porque un DTO es lo que llega antes de haber
    /// sido comprobado.
    /// </remarks>
    internal static LineaTarifaDto ADto(this LineaTarifa linea)
    {
        ArgumentNullException.ThrowIfNull(linea);

        return new LineaTarifaDto(
            linea.Id,
            linea.EmpresaId,
            linea.TarifaId,
            linea.ArticuloId,
            linea.CategoriaId,
            linea.CantidadDesde,
            linea.PrecioODescuento.Precio,
            linea.PrecioODescuento.DescuentoPorcentaje);
    }
}
