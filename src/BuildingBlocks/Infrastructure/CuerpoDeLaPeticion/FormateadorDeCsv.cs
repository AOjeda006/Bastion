using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace Bastion.BuildingBlocks.Infrastructure.CuerpoDeLaPeticion;

/// <summary>Un fichero CSV recibido como cuerpo de una petición, tal como llegó.</summary>
/// <remarks>
/// Solo bytes: qué codificación traen, qué separador y qué filas, lo decide el lector del dialecto en
/// el caso de uso, que es quien sabe qué es un error con nombre (ADR-0034 §5). El borde solo entrega
/// lo que ha leído con su tope.
/// </remarks>
/// <param name="contenido">Los bytes del cuerpo.</param>
public sealed class FicheroCsv(ReadOnlyMemory<byte> contenido)
{
    /// <summary>Los bytes del cuerpo.</summary>
    public ReadOnlyMemory<byte> Contenido { get; } = contenido;
}

/// <summary>
/// Enlaza un cuerpo <c>text/csv</c> con un parámetro <see cref="FicheroCsv"/>, sin leer la red.
/// </summary>
/// <remarks>
/// <para>
/// <b>No lee el cuerpo: recoge lo que ya leyó <see cref="LectorAcotadoDelCuerpo"/></b>, que corre antes
/// como filtro de recurso de <see cref="TopeDelCuerpoAttribute"/>. Si no lo encuentra, LANZA en vez de
/// leer por su cuenta: una acción que recibe un fichero sin declarar su tope es una acción que
/// cualquiera puede llenar de memoria, y eso tiene que salir en la primera petición de la primera
/// prueba, no el día que alguien mande un gigabyte.
/// </para>
/// </remarks>
public sealed class FormateadorDeCsv : InputFormatter
{
    /// <summary>El tipo de contenido que enlaza.</summary>
    public const string TipoDeContenido = "text/csv";

    /// <summary>Declara el tipo de contenido.</summary>
    public FormateadorDeCsv() => SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse(TipoDeContenido));

    /// <inheritdoc />
    protected override bool CanReadType(Type type) => type == typeof(FicheroCsv);

    /// <summary>Entrega el fichero también cuando viene vacío.</summary>
    /// <remarks>
    /// La base, ante un cuerpo de cero bytes, no llama a <see cref="ReadRequestBodyAsync"/>: dice «sin valor»,
    /// y el enlace contesta un <c>datos-no-validos</c> con el «field is required» del marco dentro. Un fichero
    /// vacío es un fichero, y qué le falta —la cabecera— lo dice el lector del dialecto con su nombre, como
    /// a cualquier otro. Lo encontró el sondeo de ficheros hostiles del ítem 1.11.
    /// </remarks>
    /// <param name="context">El contexto del enlace.</param>
    public override Task<InputFormatterResult> ReadAsync(InputFormatterContext context) => ReadRequestBodyAsync(context);

    /// <inheritdoc />
    public override Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ReadOnlyMemory<byte> leido = LectorAcotadoDelCuerpo.Leido(context.HttpContext)
            ?? throw new InvalidOperationException(
                $"La acción {context.HttpContext.Request.Path} recibe un fichero CSV sin declarar " +
                $"[{nameof(TopeDelCuerpoAttribute).Replace("Attribute", string.Empty, StringComparison.Ordinal)}]: " +
                "leerlo aquí sería volcar el cuerpo sin tope.");

        return InputFormatterResult.SuccessAsync(new FicheroCsv(leido));
    }
}
