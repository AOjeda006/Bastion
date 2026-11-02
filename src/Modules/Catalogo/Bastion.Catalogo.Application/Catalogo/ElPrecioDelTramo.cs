using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>
/// El precio que un tramo de tarifa le pone a un artículo, o nulo si no le pone ninguno.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo comparten el precio que se pide por HTTP y el precio de compra que pide otro módulo</b>
/// (ADR-0077), y por eso está aparte. Los dos llegan aquí con el tramo que rige y el artículo ya
/// leídos, y lo que queda —la ascendencia, las candidatas, la línea que gana y la respuesta— es lo
/// mismo. Con dos copias, la que se corrigiera un día dejaría a la otra con la regla de antes.
/// </para>
/// <para>
/// <b>Dos consultas, y ninguna depende de la profundidad del artículo en el árbol</b>: la
/// ascendencia, traída plana de una vez, y las líneas candidatas.
/// </para>
/// </remarks>
internal static class ElPrecioDelTramo
{
    /// <summary>Resuelve el precio de un artículo en un tramo que rige.</summary>
    /// <param name="tramo">El tramo que rige el día por el que se pregunta.</param>
    /// <param name="articulo">El artículo, ya leído.</param>
    /// <param name="cantidad">Cantidad por la que se pregunta.</param>
    /// <param name="dia">Día para el que se resuelve.</param>
    /// <param name="lineas">Las líneas de tarifa guardadas.</param>
    /// <param name="categorias">El árbol de categorías.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>
    /// El precio, o nulo si ninguna línea del tramo alcanza al artículo con esa cantidad.
    /// </returns>
    internal static async Task<PrecioResueltoDto?> ResolverAsync(
        Tarifa tramo,
        Articulo articulo,
        decimal cantidad,
        DateOnly dia,
        IRepositorioDeLineasDeTarifa lineas,
        IRepositorioDeCategorias categorias,
        CancellationToken cancelacion)
    {
        // La ascendencia, en UNA consulta. Un artículo sin clasificar no tiene ninguna, y entonces
        // lo único que puede ganar es una línea suya: no es un caso especial, es la lista vacía.
        IReadOnlyList<Guid> ascendencia = articulo.CategoriaId is { } categoriaId
            ? await categorias.AscendenciaAsync(categoriaId, cancelacion).ConfigureAwait(false)
            : [];

        IReadOnlyList<LineaCandidata> candidatas = await lineas
            .CandidatasAsync(tramo.Id, articulo.Id, ascendencia, cancelacion)
            .ConfigureAwait(false);

        LineaCandidata? gana = ElAntepasadoMasCercanoGana.Elegir(candidatas, cantidad);

        if (gana is null)
        {
            return null;
        }

        return new PrecioResueltoDto(
            tramo.Id,
            tramo.Codigo,

            // La divisa de la TARIFA, y va siempre. Es lo que hace que una tarifa expresada en otra
            // divisa que la de la empresa sea segura sin haberla rechazado al abrirla.
            tramo.Divisa,
            articulo.Id,
            cantidad,
            dia,
            gana.Precio,
            gana.DescuentoPorcentaje,
            gana.ArticuloId is null ? OrigenDelPrecio.Categoria : OrigenDelPrecio.Articulo,
            gana.ArticuloId ?? gana.CategoriaId!.Value,
            gana.Nivel,
            gana.CantidadDesde);
    }
}
