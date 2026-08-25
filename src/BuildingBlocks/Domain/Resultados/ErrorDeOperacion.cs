namespace Bastion.BuildingBlocks.Domain.Resultados;

/// <summary>
/// Desenlace fallido y esperado de una operación de negocio: qué clase de error es, con qué
/// código estable, y qué se le cuenta a quien la pidió.
/// </summary>
/// <remarks>
/// <para>
/// El <see cref="Codigo"/> es CONTRATO: acaba publicado en el <c>type</c> del ProblemDetails
/// (<c>/errors/{codigo}</c>) y un cliente puede ramificar sobre él. El <see cref="Mensaje"/> no
/// lo es: se puede reescribir, traducir o afinar sin romper a nadie. Por eso son dos campos y
/// no uno; un error que solo lleva mensaje obliga al cliente a comparar cadenas de texto.
/// </para>
/// <para>
/// Este mensaje va dirigido a quien está FUERA: dice qué hacer, no qué ha pasado por dentro.
/// El detalle interno vive en el registro, con su identificador de traza. Ver ADR-0004.
/// </para>
/// </remarks>
public sealed record ErrorDeOperacion
{
    private ErrorDeOperacion(string codigo, string mensaje, TipoDeError tipo) =>
        (Codigo, Mensaje, Tipo) = (codigo, mensaje, tipo);

    /// <summary>Identificador estable del error, en minúsculas y con guiones.</summary>
    public string Codigo { get; }

    /// <summary>Qué puede hacer al respecto quien pidió la operación.</summary>
    public string Mensaje { get; }

    /// <summary>Clase de error, que es lo que el borde traduce a un código de estado.</summary>
    public TipoDeError Tipo { get; }

    /// <summary>Los datos recibidos no cumplen el contrato de entrada.</summary>
    public static ErrorDeOperacion Validacion(string codigo, string mensaje) =>
        Crear(codigo, mensaje, TipoDeError.Validacion);

    /// <summary>Quien pide la operación no tiene permiso para hacerla.</summary>
    public static ErrorDeOperacion PermisoDenegado(string codigo, string mensaje) =>
        Crear(codigo, mensaje, TipoDeError.PermisoDenegado);

    /// <summary>Lo que la operación necesita no existe o no es visible.</summary>
    public static ErrorDeOperacion NoEncontrado(string codigo, string mensaje) =>
        Crear(codigo, mensaje, TipoDeError.NoEncontrado);

    /// <summary>El estado del recurso no admite la operación, o hay concurrencia.</summary>
    public static ErrorDeOperacion Conflicto(string codigo, string mensaje) =>
        Crear(codigo, mensaje, TipoDeError.Conflicto);

    /// <summary>Una regla de negocio impide la operación.</summary>
    public static ErrorDeOperacion ReglaDeNegocio(string codigo, string mensaje) =>
        Crear(codigo, mensaje, TipoDeError.ReglaDeNegocio);

    private static ErrorDeOperacion Crear(string codigo, string mensaje, TipoDeError tipo)
    {
        // Guardas de argumento: LANZAN. Que alguien construya un error sin código no es un
        // desenlace de negocio, es código mal escrito. Ver ADR-0004.
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(mensaje);

        if (!EsRanuraEstable(codigo))
        {
            throw new ArgumentException(
                $"El código de error {codigo} tiene que ser una ranura estable en minúsculas y con " +
                "guiones (como stock-insuficiente): se publica dentro de un URI.", nameof(codigo));
        }

        return new ErrorDeOperacion(codigo, mensaje, tipo);
    }

    private static bool EsRanuraEstable(string codigo)
    {
        if (codigo[0] == '-' || codigo[^1] == '-')
        {
            return false;
        }

        bool guionPrevio = false;

        foreach (char caracter in codigo)
        {
            if (caracter == '-')
            {
                if (guionPrevio)
                {
                    return false;
                }

                guionPrevio = true;
                continue;
            }

            if (!char.IsAsciiLetterLower(caracter) && !char.IsAsciiDigit(caracter))
            {
                return false;
            }

            guionPrevio = false;
        }

        return true;
    }
}
