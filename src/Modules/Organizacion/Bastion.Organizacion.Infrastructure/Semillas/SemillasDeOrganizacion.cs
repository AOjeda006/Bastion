using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bastion.Organizacion.Infrastructure.Semillas;

/// <summary>
/// Los ficheros de <c>db/semillas/</c> que carga este módulo, dónde están una vez publicados y
/// cómo se leen.
/// </summary>
/// <remarks>
/// <para>
/// <b>La lista de ficheros está ESCRITA A MANO y se comprueba en los dos sentidos.</b> Un
/// <c>Directory.GetFiles("*.json")</c> sería más corto y tendría el defecto exacto que esta clase
/// existe para impedir: una carpeta vacía devuelve cero ficheros sin error, se cargarían cero
/// semillas, y «cero» es indistinguible de «no había nada que cargar». Con la lista escrita, falta
/// un fichero y se dice cuál; sobra un fichero y también, porque un <c>.json</c> que nadie carga
/// es una semilla que alguien creyó haber sembrado.
/// </para>
/// <para>
/// <b>Se leen del directorio del ensamblado, no del repositorio.</b> En el contenedor no hay
/// <c>db/semillas/</c>: hay <c>/app/semillas/</c>, porque el <c>Dockerfile.api</c> se lleva
/// únicamente <c>/publicado</c>. Que los ficheros lleguen ahí es cosa del <c>&lt;Content Include&gt;</c>
/// del <c>.csproj</c>, y que sigan llegando lo comprueba la CI mirando DENTRO de la imagen.
/// </para>
/// </remarks>
public static class SemillasDeOrganizacion
{
    /// <summary>Carpeta, relativa al ensamblado, donde se publican las semillas.</summary>
    public const string Carpeta = "semillas";

    /// <summary>Tipos impositivos por tramos de vigencia.</summary>
    public const string Impuestos = "impuestos.json";

    /// <summary>Unidades de medida de la instalación.</summary>
    public const string UnidadesDeMedida = "unidades-de-medida.json";

    /// <summary>Extensión de los ficheros de semilla, y el filtro con el que se listan.</summary>
    public const string Extension = "*.json";

    /// <summary>Los ficheros que este módulo espera encontrar, y ni uno más.</summary>
    public static IReadOnlyList<string> Ficheros { get; } = [Impuestos, UnidadesDeMedida];

    // Comentarios PERMITIDOS a propósito. Un tipo impositivo sin la norma que lo publicó al lado
    // es un número que nadie puede comprobar, y la explicación tiene que vivir donde vive el dato:
    // en un `.md` hermano se separa de él en el primer `git mv`. Lo que cuesta es esta línea.
    //
    // `PropertyNameCaseInsensitive` se queda en falso —el valor por omisión— y no es descuido:
    // con `JsonUnmappedMemberHandling.Disallow` en cada registro, un `Codigo` con la mayúscula
    // cambiada deja de casar, sale como miembro no mapeado y revienta la carga diciendo cuál. Ser
    // tolerante aquí convertiría ese aviso en un `required` sin rellenar, que dice menos.
    private static readonly JsonSerializerOptions s_json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Dónde están las semillas de este ensamblado, ya publicado.</summary>
    public static string CarpetaPublicada => Path.Combine(AppContext.BaseDirectory, Carpeta);

    /// <summary>
    /// Comprueba que la carpeta existe y que dentro están exactamente los ficheros esperados.
    /// </summary>
    /// <param name="carpeta">Carpeta donde buscarlos.</param>
    /// <exception cref="SemillasQueNoLleganException">
    /// Si falta la carpeta, falta un fichero o sobra uno que nadie carga.
    /// </exception>
    public static void ComprobarQueEstanTodas(string carpeta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(carpeta);

        if (!Directory.Exists(carpeta))
        {
            throw new SemillasQueNoLleganException(
                $"No existe la carpeta de semillas «{carpeta}». El ensamblado se ha publicado sin " +
                "ellas: revise el `<Content Include>` de Bastion.Organizacion.Infrastructure.csproj " +
                "y que el `.dockerignore` no excluya `db/semillas`.");
        }

        HashSet<string> presentes =
        [
            .. Directory.EnumerateFiles(carpeta, Extension).Select(Path.GetFileName).OfType<string>(),
        ];

        string[] faltan = [.. Ficheros.Where(fichero => !presentes.Contains(fichero))];
        string[] sobran = [.. presentes.Where(fichero => !Ficheros.Contains(fichero))];

        if (faltan.Length > 0)
        {
            throw new SemillasQueNoLleganException(
                $"Faltan semillas en «{carpeta}»: {string.Join(", ", faltan)}. Si el fichero está " +
                "en `db/semillas/` pero no aquí, no se está publicando.");
        }

        if (sobran.Length > 0)
        {
            throw new SemillasQueNoLleganException(
                $"Hay semillas en «{carpeta}» que nadie carga: {string.Join(", ", sobran)}. Un " +
                "fichero que se publica y no se lee es una semilla que alguien creyó haber " +
                $"sembrado; añádalo a {nameof(SemillasDeOrganizacion)}.{nameof(Ficheros)} o quítelo.");
        }
    }

    /// <summary>
    /// Lee un fichero de semilla y devuelve sus filas, <b>afirmando que hay al menos una</b>.
    /// </summary>
    /// <typeparam name="TSemilla">Forma de cada fila del fichero.</typeparam>
    /// <param name="carpeta">Carpeta donde está el fichero.</param>
    /// <param name="fichero">Nombre del fichero, con extensión.</param>
    /// <returns>Las filas del fichero, nunca vacías.</returns>
    /// <exception cref="SemillasQueNoLleganException">
    /// Si el fichero no existe, no se puede interpretar o no trae ninguna fila.
    /// </exception>
    public static IReadOnlyList<TSemilla> Leer<TSemilla>(string carpeta, string fichero)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(carpeta);
        ArgumentException.ThrowIfNullOrWhiteSpace(fichero);

        string ruta = Path.Combine(carpeta, fichero);

        if (!File.Exists(ruta))
        {
            throw new SemillasQueNoLleganException($"No existe la semilla «{ruta}».");
        }

        List<TSemilla>? filas;

        try
        {
            using FileStream flujo = File.OpenRead(ruta);

            filas = JsonSerializer.Deserialize<List<TSemilla>>(flujo, s_json);
        }
        catch (JsonException excepcion)
        {
            // Se envuelve y no se deja pasar: un `JsonException` a secas dice la línea y la
            // columna, pero no QUÉ fichero de qué carpeta, y el que se está leyendo puede ser el
            // del repositorio o el de dentro de la imagen. La diferencia es media diagnosis.
            throw new SemillasQueNoLleganException(
                $"La semilla «{ruta}» no se puede interpretar: {excepcion.Message}", excepcion);
        }

        // LA AFIRMACIÓN DE CONJUNTO NO VACÍO, y la misma de la que se dejó constancia en el
        // migrador: un fichero con `[]` dentro se lee sin error, siembra cero filas y sale con 0.
        // «Cero semillas» y «no había nada que sembrar» tienen que poder distinguirse, y aquí solo
        // hay una manera: exigir que haya algo.
        if (filas is not { Count: > 0 })
        {
            throw new SemillasQueNoLleganException(
                $"La semilla «{ruta}» no trae ninguna fila. Un fichero vacío se carga sin error y " +
                "deja la instalación sin maestros, que es indistinguible de no haberlo cargado.");
        }

        return filas;
    }
}
