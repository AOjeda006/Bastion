namespace Bastion.BuildingBlocks.Infrastructure.Listados;

/// <summary>
/// Lo que hace falta para convertir el <c>?q=</c> de un cliente en un patrón de <c>ILIKE</c>.
/// </summary>
/// <remarks>
/// Aquí y no en cada repositorio porque lo que hay que acertar es el escape, y se acierta una vez.
/// </remarks>
public static class Filtros
{
    /// <summary>Carácter de escape del patrón, el mismo que espera <c>ILIKE ... ESCAPE</c>.</summary>
    public const string Escape = "\\";

    /// <summary>
    /// El patrón que busca el texto en cualquier posición.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Los comodines del cliente se escapan</b>, y no es puntillismo: sin escapar, un
    /// <c>?q=%</c> devuelve la tabla entera —el tope de tamaño la corta, pero el
    /// <c>COUNT</c> ya se ha comido el recorrido— y un <c>?q=a_c</c> encuentra cosas que nadie
    /// escribió. El usuario que escribe un guion bajo quiere un guion bajo.
    /// </para>
    /// <para>
    /// Hay que llamarlo ANTES de construir el árbol de expresión, nunca dentro: dentro, EF Core
    /// intentaría traducir esta función a SQL y fallaría. Fuera, el patrón entra como parámetro,
    /// que además es lo que permite que el plan se reutilice.
    /// </para>
    /// </remarks>
    /// <param name="texto">Lo que escribió el cliente, ya recortado.</param>
    public static string Contiene(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        // El escape primero: al revés, se escaparían las barras que acaba de meter este método.
        string escapado = texto
            .Replace(Escape, Escape + Escape, StringComparison.Ordinal)
            .Replace("%", Escape + "%", StringComparison.Ordinal)
            .Replace("_", Escape + "_", StringComparison.Ordinal);

        return $"%{escapado}%";
    }
}
