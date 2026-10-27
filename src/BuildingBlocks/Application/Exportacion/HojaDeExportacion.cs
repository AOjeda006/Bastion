namespace Bastion.BuildingBlocks.Application.Exportacion;

/// <summary>Una columna de un fichero exportado: su nombre en la cabecera y cómo sale de cada fila.</summary>
/// <typeparam name="T">Lo que publica el listado de cada elemento.</typeparam>
/// <param name="Nombre">El nombre de la cabecera, en <c>snake_case</c> y sin tildes.</param>
/// <param name="Campo">Cómo se escribe el campo de un elemento.</param>
public sealed record ColumnaCsv<T>(string Nombre, Func<T, CampoCsv> Campo);

/// <summary>
/// Lo que exporta un listado: cómo se llama el recurso, que da nombre al fichero, y sus columnas,
/// en su orden.
/// </summary>
/// <remarks>
/// La cabecera la escribe el servidor y va en <c>snake_case</c>, sin tildes y sin traducir, como la
/// de la importación (respuesta 4 de la puerta del 3.2): un fichero que sale de una pantalla en
/// inglés tiene que poder leerse con la misma macro que el que sale de una en español.
/// </remarks>
/// <typeparam name="T">Lo que publica el listado de cada elemento.</typeparam>
/// <param name="Recurso">El nombre del recurso en la ruta, que es el principio del fichero.</param>
/// <param name="Columnas">Las columnas, en el orden en que salen.</param>
public sealed record HojaDeExportacion<T>(string Recurso, IReadOnlyList<ColumnaCsv<T>> Columnas);
