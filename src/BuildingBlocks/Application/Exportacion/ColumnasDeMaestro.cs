namespace Bastion.BuildingBlocks.Application.Exportacion;

/// <summary>
/// Las dos columnas de un maestro al que apunta una fila, código y nombre, con el prefijo de lo que
/// es para ella (respuesta 1 de la puerta del 3.2, ADR-0074).
/// </summary>
/// <remarks>
/// <para>
/// <b>Un maestro sin nombrar sale con su identificador en la columna del código</b>, y el nombre
/// vacío: es lo que enseña la pantalla en su lugar. El identificador no lleva apóstrofo, porque no
/// lo escribió una persona.
/// </para>
/// <para>
/// <b>Una fila que no apunta a ninguno sale con los dos campos vacíos</b>, como un artículo sin
/// categoría: las columnas están siempre, para que cada fila cuadre con la cabecera.
/// </para>
/// </remarks>
public static class ColumnasDeMaestro
{
    /// <summary>Las dos columnas, <c>{prefijo}_codigo</c> y <c>{prefijo}_nombre</c>.</summary>
    /// <typeparam name="T">Lo que se escribe de cada elemento.</typeparam>
    /// <param name="prefijo">Lo que es el maestro para la fila, como <c>unidad_base</c>.</param>
    /// <param name="maestro">El maestro de un elemento, o nulo si no apunta a ninguno.</param>
    public static IReadOnlyList<ColumnaCsv<T>> De<T>(string prefijo, Func<T, MaestroCitado?> maestro)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefijo);
        ArgumentNullException.ThrowIfNull(maestro);

        return
        [
            new($"{prefijo}_codigo", elemento => Codigo(maestro(elemento))),
            new($"{prefijo}_nombre", elemento => CampoCsv.Texto(maestro(elemento)?.Nombre?.Nombre)),
        ];
    }

    private static CampoCsv Codigo(MaestroCitado? citado) => citado switch
    {
        null => CampoCsv.Texto(null),
        { Nombre: { } nombre } => CampoCsv.Texto(nombre.Codigo),
        _ => CampoCsv.Identificador(citado.Id),
    };
}
