using Bastion.BuildingBlocks.Contracts.Importacion;

namespace Bastion.BuildingBlocks.Application.Importacion;

/// <summary>
/// Va apuntando por qué se rechaza cada fila mientras se decide, y al final lo entrega agrupado por
/// columna y motivo (ADR-0034 §2).
/// </summary>
/// <remarks>
/// <para>
/// <b>No recibe ningún valor, y no es una recomendación sino su firma</b>: línea, columna y motivo.
/// Un caso de uso que quisiera meter en el informe el NIF de una fila no tiene por dónde.
/// </para>
/// <para>
/// <b>Un motivo por columna de cada fila, el primero que se apunta.</b> Una identificación vacía es
/// obligatoria que falta, y decir además que no es un NIF válido no ayuda a arreglarla; el caso de uso
/// apunta en orden de lo más básico a lo más elaborado, y lo demás se descarta.
/// </para>
/// </remarks>
/// <param name="columnas">Las columnas de la cabecera, en su orden: el del informe.</param>
public sealed class RechazosDeImportacion(IReadOnlyList<string> columnas)
{
    private readonly Dictionary<(string? Columna, MotivoDeRechazo Motivo), SortedSet<int>> _grupos = [];
    private readonly HashSet<(int Linea, string? Columna)> _apuntados = [];
    private readonly HashSet<int> _lineas = [];

    /// <summary>Cuántas filas tienen al menos un motivo.</summary>
    public int Rechazadas => _lineas.Count;

    /// <summary>Apunta un motivo, salvo que esa columna de esa fila ya tenga uno.</summary>
    /// <param name="linea">La línea de la fila, como la cuenta <see cref="FilaCsv.Linea"/>.</param>
    /// <param name="columna">La columna, con el nombre de la cabecera; nula si es de la fila entera.</param>
    /// <param name="motivo">Por qué.</param>
    public void Apuntar(int linea, string? columna, MotivoDeRechazo motivo)
    {
        if (columna is not null && !columnas.Contains(columna, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"«{columna}» no es una columna de la cabecera: el informe diría una que el fichero no tiene.",
                nameof(columna));
        }

        if (!_apuntados.Add((linea, columna)))
        {
            return;
        }

        if (!_grupos.TryGetValue((columna, motivo), out SortedSet<int>? lineas))
        {
            lineas = [];
            _grupos[(columna, motivo)] = lineas;
        }

        lineas.Add(linea);
        _lineas.Add(linea);
    }

    /// <summary>Si la fila de esa línea tiene ya algún motivo.</summary>
    /// <param name="linea">La línea de la fila.</param>
    public bool EstaRechazada(int linea) => _lineas.Contains(linea);

    /// <summary>
    /// Los rechazos agrupados: primero los de la fila entera, luego por el orden de las columnas en la
    /// cabecera, y dentro de cada columna por el orden de los motivos.
    /// </summary>
    public IReadOnlyList<RechazoDto> AInforme() =>
        [.. _grupos
            .OrderBy(grupo => grupo.Key.Columna is null ? -1 : IndiceDe(grupo.Key.Columna))
            .ThenBy(grupo => grupo.Key.Motivo)
            .Select(grupo => new RechazoDto(grupo.Key.Columna, grupo.Key.Motivo, [.. grupo.Value]))];

    private int IndiceDe(string columna)
    {
        for (int indice = 0; indice < columnas.Count; indice++)
        {
            if (string.Equals(columnas[indice], columna, StringComparison.Ordinal))
            {
                return indice;
            }
        }

        return columnas.Count;
    }
}
