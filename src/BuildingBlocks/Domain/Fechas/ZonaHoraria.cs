using System.Diagnostics.CodeAnalysis;

namespace Bastion.BuildingBlocks.Domain.Fechas;

/// <summary>
/// La zona horaria de una empresa o de un almacén: la que decide a qué día pertenece un instante
/// allí (ADR-0079).
/// </summary>
/// <remarks>
/// <para>
/// <b>Una lista cerrada, y no cualquier zona que conozca el sistema.</b> Son las tres de España:
/// la peninsular, que es también la de Baleares; la de Canarias; y la de Ceuta y Melilla, que hoy
/// dice lo mismo que la peninsular y tiene su propio identificador. Una zona que el sistema
/// conoce no es una que el negocio haya pensado: con <c>America/Bogota</c> el día de un almacén
/// iría cinco horas por detrás, y ninguna regla de las fechas está escrita para eso. Ampliar la
/// lista es una decisión que se pregunta, no una línea que se añade.
/// </para>
/// <para>
/// <b>Se guarda el identificador IANA</b>, que es lo que entiende el sistema en Linux, donde corre
/// la imagen, y también en Windows, que lo traduce con ICU. Las reglas de cada una —cuándo cambia
/// la hora— vienen del sistema, y las lee solo <see cref="DiaDeNegocio"/>: por eso son internas.
/// </para>
/// <para>
/// <b>Solo existen las tres instancias</b>: el constructor es privado, y <see cref="De"/> y
/// <see cref="Intentar"/> devuelven una de ellas. Una zona fuera de la lista no se puede
/// construir, así que una entidad que tiene una tiene una válida.
/// </para>
/// </remarks>
public sealed record ZonaHoraria
{
    private ZonaHoraria(string iana) => Iana = iana;

    /// <summary>La peninsular, y la de Baleares.</summary>
    public static ZonaHoraria Madrid { get; } = new("Europe/Madrid");

    /// <summary>La de Canarias, una hora por detrás de la peninsular todo el año.</summary>
    public static ZonaHoraria Canarias { get; } = new("Atlantic/Canary");

    /// <summary>La de Ceuta y Melilla.</summary>
    public static ZonaHoraria Ceuta { get; } = new("Africa/Ceuta");

    /// <summary>La de una empresa que no dice otra: la peninsular.</summary>
    public static ZonaHoraria PorOmision => Madrid;

    /// <summary>Las zonas que admite el negocio, y ninguna más.</summary>
    public static IReadOnlyList<ZonaHoraria> Admitidas { get; } = [Madrid, Canarias, Ceuta];

    /// <summary>El identificador IANA de la zona, que es como se guarda.</summary>
    public string Iana { get; }

    /// <summary>Las reglas de la zona, como las conoce el sistema.</summary>
    /// <remarks>
    /// El sistema guarda lo que ya buscó, así que pedirlas cada vez no vuelve a leer el disco. Y no
    /// se guardan aquí: un campo más entraría en la igualdad del <c>record</c>.
    /// </remarks>
    internal TimeZoneInfo Reglas => TimeZoneInfo.FindSystemTimeZoneById(Iana);

    /// <summary>Una de las zonas admitidas, por su identificador, o lanza si no lo es.</summary>
    /// <param name="iana">El identificador IANA, exacto.</param>
    /// <returns>La zona.</returns>
    /// <exception cref="ArgumentException">No es una de las zonas admitidas.</exception>
    public static ZonaHoraria De(string iana)
    {
        if (!Intentar(iana, out ZonaHoraria? zona))
        {
            throw new ArgumentException(
                $"«{iana}» no es una zona horaria admitida. Admitidas: {Enumerar()}.", nameof(iana));
        }

        return zona;
    }

    /// <summary>Una de las zonas admitidas, por su identificador, sin lanzar.</summary>
    /// <remarks>
    /// El identificador se compara exacto, con sus mayúsculas: es el nombre de un fichero de la
    /// base de zonas, y <c>europe/madrid</c> no lo es en Linux.
    /// </remarks>
    /// <param name="iana">El identificador IANA.</param>
    /// <param name="zona">La zona, si es una de las admitidas.</param>
    /// <returns>Si lo es.</returns>
    public static bool Intentar(string? iana, [NotNullWhen(true)] out ZonaHoraria? zona)
    {
        zona = Admitidas.FirstOrDefault(admitida => string.Equals(admitida.Iana, iana, StringComparison.Ordinal));

        return zona is not null;
    }

    /// <summary>Los identificadores admitidos, para poder decírselos a quien se equivocó.</summary>
    /// <returns>Los identificadores, separados por comas.</returns>
    public static string Enumerar() => string.Join(", ", Admitidas.Select(zona => zona.Iana));

    /// <inheritdoc/>
    public override string ToString() => Iana;
}
