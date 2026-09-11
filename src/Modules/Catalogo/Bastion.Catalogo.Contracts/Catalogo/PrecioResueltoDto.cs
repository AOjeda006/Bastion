namespace Bastion.Catalogo.Contracts.Catalogo;

/// <summary>De dónde ha salido el precio que se ha resuelto.</summary>
/// <remarks>
/// Sale como texto y no como ordinal, por lo mismo que el tipo de artículo: un número es un
/// contrato que se rompe con solo reordenar el enumerado, y sin que quien lo reordena lo vea.
/// </remarks>
public static class OrigenDelPrecio
{
    /// <summary>La tarifa tenía una línea para este artículo exactamente.</summary>
    public const string Articulo = "Articulo";

    /// <summary>La tarifa tenía una línea para una categoría de la que este artículo cuelga.</summary>
    public const string Categoria = "Categoria";
}

/// <summary>
/// El precio que una tarifa le pone a un artículo para una cantidad y una fecha, con el porqué.
/// </summary>
/// <remarks>
/// <para>
/// <b>La divisa va siempre, y no es adorno: es lo que hace que una tarifa en otra divisa sea
/// segura.</b> Una tarifa puede estar expresada en una divisa distinta de la de la empresa —una de
/// exportación en dólares lo está—, y este ítem lo acepta a propósito en vez de rechazarlo. Lo que
/// impide que eso acabe en un descuadre no es una comprobación en el alta: es que quien recibe el
/// precio recibe <b>con qué se mide</b>, y por tanto no puede tratarlo como si fuera el de la
/// empresa. Convertirlo es otra cosa y es de la fase 5.
/// </para>
/// <para>
/// <b>Y va el origen, que es la precedencia hecha visible.</b> Sin
/// <see cref="Origen"/>/<see cref="OrigenId"/>/<see cref="NivelDeLaCategoria"/>, dos
/// implementaciones distintas de la precedencia devuelven el mismo número en el caso fácil y
/// distinto en el que importa, y desde fuera no hay forma de ver cuál se ha usado. Con ellos, el
/// caso de las dos líneas de categoría a distinta profundidad puede afirmar <b>cuál</b> ha ganado
/// y no solo cuánto.
/// </para>
/// </remarks>
/// <param name="TarifaId">Tramo de tarifa que ha resuelto el precio.</param>
/// <param name="Codigo">Código de la tarifa, el que se pidió.</param>
/// <param name="DivisaId">Divisa en la que está expresado el precio.</param>
/// <param name="ArticuloId">Artículo por el que se preguntó.</param>
/// <param name="Cantidad">Cantidad por la que se preguntó.</param>
/// <param name="Fecha">Día para el que se ha resuelto.</param>
/// <param name="Precio">Precio por unidad, o nulo si la línea que gana es de descuento.</param>
/// <param name="DescuentoPorcentaje">Descuento en tanto por ciento, o nulo si la línea es de precio.</param>
/// <param name="Origen">
/// <see cref="OrigenDelPrecio.Articulo"/> o <see cref="OrigenDelPrecio.Categoria"/>.
/// </param>
/// <param name="OrigenId">Identificador del artículo o de la categoría de la línea que gana.</param>
/// <param name="NivelDeLaCategoria">
/// Cuántos saltos hacia arriba hay desde la categoría del artículo hasta la de la línea que gana:
/// <c>0</c> es su propia categoría, <c>1</c> la madre, y así. Nulo cuando gana una línea de
/// artículo. <b>Es el número que distingue una precedencia correcta de una que ordena por
/// profundidad</b>: una categoría más honda que la del artículo no es antepasada suya y no compite.
/// </param>
/// <param name="CantidadDesde">Cantidad desde la que rige el tramo que ha ganado, incluida.</param>
public sealed record PrecioResueltoDto(
    Guid TarifaId,
    string Codigo,
    Guid DivisaId,
    Guid ArticuloId,
    decimal Cantidad,
    DateOnly Fecha,
    decimal? Precio,
    decimal? DescuentoPorcentaje,
    string Origen,
    Guid OrigenId,
    int? NivelDeLaCategoria,
    decimal CantidadDesde);
