namespace Bastion.BuildingBlocks.Contracts.Paginacion;

/// <summary>
/// Por qué campo se ordena una colección, y en qué sentido.
/// </summary>
/// <remarks>
/// <para>
/// El <b>tope</b> del orden no vive aquí, y no es un olvido: este tipo dice qué se ha pedido, no
/// qué se puede pedir. Lo que se puede pedir lo declara cada recurso en su <c>Contracts</c>, y el
/// borde rechaza con un <c>400</c> cualquier campo que no esté en esa lista. Sin ese tope,
/// <c>?sort=</c> es una invitación a ordenar por una columna sin índice, que es una descarga
/// completa de la tabla escrita desde la barra del navegador — el mismo agujero que
/// <c>?size=100000</c>, por otra puerta.
/// </para>
/// <para>
/// El <see cref="Campo"/> es el nombre <b>externo</b>, el que viaja en la URL, no el de la
/// propiedad del dominio. Que los dos coincidan hoy es una casualidad cómoda; el día que un campo
/// se renombre por dentro, la URL de un cliente no tiene por qué romperse.
/// </para>
/// </remarks>
/// <param name="Campo">Nombre externo del campo por el que se ordena.</param>
/// <param name="Descendente">Si el orden va de mayor a menor.</param>
public sealed record Orden(string Campo, bool Descendente)
{
    /// <summary>Marca de sentido descendente en la cadena de consulta: <c>?sort=-codigo</c>.</summary>
    public const char MarcaDeDescendente = '-';

    /// <summary>
    /// Lee un <c>?sort=</c> tal como viaja en la URL: <c>codigo</c> o <c>-codigo</c>.
    /// </summary>
    /// <remarks>
    /// Solo separa el sentido del nombre. <b>No comprueba que el campo exista</b>, porque aquí no
    /// se sabe de qué recurso se habla; eso lo hace quien tiene la lista delante.
    /// </remarks>
    /// <param name="sort">Valor del parámetro, o nulo si no se pidió orden.</param>
    /// <returns>El orden pedido, o nulo si no se pidió ninguno.</returns>
    public static Orden? Leer(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return null;
        }

        string limpio = sort.Trim();

        return limpio[0] == MarcaDeDescendente
            ? new Orden(limpio[1..], Descendente: true)
            : new Orden(limpio, Descendente: false);
    }
}
