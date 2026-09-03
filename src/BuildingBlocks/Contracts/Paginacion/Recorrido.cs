namespace Bastion.BuildingBlocks.Contracts.Paginacion;

/// <summary>
/// Lo que una <b>búsqueda</b> pide para seguir por donde se quedó: un cursor y un tamaño.
/// </summary>
/// <remarks>
/// <para>
/// No lleva número de página, y esa ausencia es la diferencia con <see cref="Paginacion"/>. Un
/// cursor no cuenta páginas: dice «después de esto», que es lo que permite que una fila dada de
/// alta entre la página 1 y la 2 no desplace el resto ni haga que un elemento se vea dos veces o
/// ninguna.
/// </para>
/// <para>
/// Viaja en el <b>cuerpo</b> de un <c>POST</c>, junto al criterio, nunca en la cadena de consulta
/// (ADR-0025).
/// </para>
/// </remarks>
/// <param name="Cursor">Dónde seguir, o nulo para empezar por el principio.</param>
/// <param name="Tamanio">Cuántos elementos se piden.</param>
public sealed record Recorrido(string? Cursor, int Tamanio)
{
    /// <summary>El primer tramo: sin cursor y con el tamaño por omisión.</summary>
    public static Recorrido Primero => new(null, Paginacion.TamanioPorDefecto);
}
