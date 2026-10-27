using System.Globalization;

namespace Bastion.BuildingBlocks.Application.Exportacion;

/// <summary>
/// Un campo de una fila exportada, ya escrito en el dialecto de la importación (ADR-0034 §5), y
/// con la marca de si lo escribió una persona.
/// </summary>
/// <remarks>
/// <para>
/// <b>El tipo decide la forma, y la forma es la que lee la importación</b>: un importe o una
/// cantidad, con coma decimal, sin punto de miles y sin ceros a la derecha; sí o no, con la tilde;
/// una fecha, en <c>AAAA-MM-DD</c>. Así un fichero exportado se puede volver a importar, y lo que
/// lee <c>CamposCsv</c> es justo lo que se escribe aquí.
/// </para>
/// <para>
/// <b>Solo el texto lleva la regla del apóstrofo</b> (decisión 7 del 1.11, ADR-0073). Un texto lo
/// escribe una persona, y si empieza por <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>, un tabulador o un
/// retorno, una hoja de cálculo lo tomaría por una fórmula. Un número lo escribe Bastion, y un
/// <c>-12,5</c> con apóstrofo dejaría de ser un número al abrirlo.
/// </para>
/// </remarks>
public readonly record struct CampoCsv
{
    // Veintiocho almohadillas, que es la escala máxima de un `decimal`: así no se redondea nada, y
    // las que sobran no escriben ceros. Sin separador de grupos, que es lo que pide el dialecto.
    private const string FormatoDeNumero = "0.############################";

    private CampoCsv(string escrito, bool esTexto)
    {
        Escrito = escrito;
        EsTexto = esTexto;
    }

    /// <summary>El campo tal como se escribe, antes del apóstrofo y de las comillas.</summary>
    public string Escrito { get; }

    /// <summary>Si lo escribió una persona, que es lo que lleva la regla del apóstrofo.</summary>
    public bool EsTexto { get; }

    /// <summary>Un texto que escribió una persona: un nombre, un código, una referencia.</summary>
    /// <param name="texto">El texto, o nulo para un campo vacío.</param>
    public static CampoCsv Texto(string? texto) => new(texto ?? string.Empty, esTexto: true);

    /// <summary>Un importe, una cantidad o un factor, con coma decimal y sin ceros a la derecha.</summary>
    /// <param name="numero">El número, o nulo para un campo vacío.</param>
    public static CampoCsv Numero(decimal? numero) =>
        new(numero is { } valor ? Escribir(valor) : string.Empty, esTexto: false);

    /// <summary>Un número entero, como una línea o un ejercicio.</summary>
    /// <param name="numero">El número, o nulo para un campo vacío.</param>
    public static CampoCsv Numero(long? numero) =>
        new(numero?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, esTexto: false);

    /// <summary>Un sí o un no, con la tilde, que es como lo escribe una persona en español.</summary>
    /// <param name="valor">El valor, o nulo para un campo vacío.</param>
    public static CampoCsv SiNo(bool? valor) =>
        new(valor switch { true => "sí", false => "no", null => string.Empty }, esTexto: false);

    /// <summary>Una fecha, en <c>AAAA-MM-DD</c>.</summary>
    /// <param name="fecha">La fecha, o nula para un campo vacío.</param>
    public static CampoCsv Fecha(DateOnly? fecha) =>
        new(fecha?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty, esTexto: false);

    /// <summary>
    /// Un identificador, que va cuando falta el permiso de ver el maestro al que apunta (ADR-0055
    /// §12), como en la pantalla.
    /// </summary>
    /// <param name="identificador">El identificador, o nulo para un campo vacío.</param>
    public static CampoCsv Identificador(Guid? identificador) =>
        new(identificador?.ToString("D", CultureInfo.InvariantCulture) ?? string.Empty, esTexto: false);

    /// <summary>Un valor de un conjunto cerrado —un estado, un tipo—, con su nombre, sin traducir.</summary>
    /// <typeparam name="TValor">El conjunto.</typeparam>
    /// <param name="valor">El valor, o nulo para un campo vacío.</param>
    public static CampoCsv Valor<TValor>(TValor? valor)
        where TValor : struct, Enum =>
        new(valor?.ToString() ?? string.Empty, esTexto: false);

    // El cero sale sin signo sin pedirlo: un `decimal` guarda el signo del cero, pero el formato no
    // lo escribe, y `-0,0` sale `0`, como `0,0000`. Lo afirma la teoría de los números del escritor.
    private static string Escribir(decimal numero) =>
        numero.ToString(FormatoDeNumero, CultureInfo.InvariantCulture).Replace('.', ',');
}
