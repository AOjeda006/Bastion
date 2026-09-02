using Bastion.BuildingBlocks.Domain.Entidades;

namespace Bastion.Organizacion.Domain.Unidades;

/// <summary>
/// Una unidad en la que se cuenta la mercancía: unidad, kilogramo, metro, caja.
/// </summary>
/// <remarks>
/// <para>
/// Maestro de la instalación y no de una empresa (R8): un kilo es un kilo en todas. Está declarado
/// con su motivo en la lista de globales de <c>CadaEntidadDeclaraSuInquilinatoTests</c>.
/// </para>
/// <para>
/// <b>Los decimales SÍ son una columna aquí, al contrario que en la divisa</b>, y la diferencia
/// merece leerse porque las dos entidades se parecen. Cuántos decimales tiene el euro lo dice una
/// norma fiscal y equivocarse cambia una cuota; cuántos admite el kilo lo decide quien monta el
/// almacén —hay quien pesa a gramos y quien pesa a décimas—, no hay norma que consultar, y
/// cambiarlo no reescribe ninguna liquidación. Lo primero se guarda en código con un caso dorado;
/// lo segundo, en una fila.
/// </para>
/// <para>
/// Los decimales son <b>del maestro</b> y no del artículo, y esa es la restricción útil: si cada
/// artículo eligiera los suyos, dos artículos medidos en kilos admitirían cantidades con distinta
/// precisión y sumarlos daría un número que no se puede volver a repartir. La unidad manda sobre
/// todo lo que se mida con ella.
/// </para>
/// </remarks>
public sealed class UnidadMedida : EntidadBase
{
    /// <summary>Tope del código: cabe en una línea de albarán.</summary>
    public const int LongitudMaximaDeCodigo = 10;

    /// <summary>Tope del nombre con el que se muestra.</summary>
    public const int LongitudMaximaDeNombre = 60;

    /// <summary>
    /// Decimales máximos que se admiten en una cantidad.
    /// </summary>
    /// <remarks>
    /// Seis, que es lo que EF Core y PostgreSQL guardan sin discusión en la escala prevista para
    /// las cantidades. Por encima, el número que se guarda deja de ser el que se escribió.
    /// </remarks>
    public const int DecimalesMaximos = 6;

    private UnidadMedida(Guid id, string codigo, string nombre, int decimales, DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        Codigo = codigo;
        Nombre = nombre;
        Decimales = decimales;
    }

    private UnidadMedida()
    {
        Codigo = null!;
        Nombre = null!;
    }

    /// <summary>Identificador de la unidad.</summary>
    public Guid Id { get; private set; }

    /// <summary>Código en mayúsculas: <c>UD</c>, <c>KG</c>, <c>M</c>.</summary>
    public string Codigo { get; private set; }

    /// <summary>Nombre con el que se muestra.</summary>
    public string Nombre { get; private set; }

    /// <summary>Decimales que admite una cantidad medida en esta unidad.</summary>
    /// <remarks>Cero en las que no se parten: no existe media unidad de un tornillo.</remarks>
    public int Decimales { get; private set; }

    /// <summary>Da de alta una unidad de medida.</summary>
    /// <param name="codigo">Código; se normaliza a mayúsculas.</param>
    /// <param name="nombre">Nombre con el que se muestra.</param>
    /// <param name="decimales">Decimales que admite una cantidad.</param>
    /// <param name="momento">Ahora, de quien tenga el <c>TimeProvider</c>.</param>
    public static UnidadMedida Crear(
        string codigo,
        string nombre,
        int decimales,
        DateTimeOffset momento) =>
        new(
            Guid.CreateVersion7(),
            CodigoValido(codigo),
            NombreValido(nombre),
            DecimalesValidos(decimales),
            momento);

    /// <summary>
    /// Cambia el nombre. Ni el código ni los decimales.
    /// </summary>
    /// <remarks>
    /// <b>Los decimales no se tocan</b>, y esta es la razón: bajarlos de tres a cero convertiría
    /// cada existencia de 1,250 kg ya registrada en un número que la unidad dice que no puede
    /// existir, y el histórico de valoración dejaría de cuadrar sin que nadie hubiera movido
    /// mercancía. Medir con otra precisión es una unidad nueva y su conversión.
    /// </remarks>
    /// <param name="nombre">Nombre con el que se muestra.</param>
    public void Modificar(string nombre) => Nombre = NombreValido(nombre);

    /// <summary>Deja el código en la forma exacta en la que se guarda.</summary>
    /// <param name="codigo">Código tal como lo escribieron.</param>
    public static string NormalizarCodigo(string codigo)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        return codigo.Trim().ToUpperInvariant();
    }

    private static string CodigoValido(string codigo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);

        string normalizado = NormalizarCodigo(codigo);

        return normalizado.Length <= LongitudMaximaDeCodigo
            ? normalizado
            : throw new ArgumentException(
                $"El código de unidad admite {LongitudMaximaDeCodigo} caracteres como máximo.",
                nameof(codigo));
    }

    private static string NombreValido(string nombre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        string limpio = nombre.Trim();

        return limpio.Length <= LongitudMaximaDeNombre
            ? limpio
            : throw new ArgumentException(
                $"El nombre de la unidad admite {LongitudMaximaDeNombre} caracteres como máximo.",
                nameof(nombre));
    }

    private static int DecimalesValidos(int decimales) =>
        decimales is >= 0 and <= DecimalesMaximos
            ? decimales
            : throw new ArgumentOutOfRangeException(
                nameof(decimales),
                decimales,
                $"Los decimales de una unidad van de 0 a {DecimalesMaximos}.");
}
