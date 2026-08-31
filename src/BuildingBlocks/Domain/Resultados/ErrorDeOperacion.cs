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
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> s_sinCampos =
        new Dictionary<string, IReadOnlyList<string>>();

    private ErrorDeOperacion(
        string codigo,
        string mensaje,
        TipoDeError tipo,
        IReadOnlyDictionary<string, IReadOnlyList<string>> campos) =>
        (Codigo, Mensaje, Tipo, Campos) = (codigo, mensaje, tipo, campos);

    /// <summary>Identificador estable del error, en minúsculas y con guiones.</summary>
    public string Codigo { get; }

    /// <summary>Qué puede hacer al respecto quien pidió la operación.</summary>
    public string Mensaje { get; }

    /// <summary>Clase de error, que es lo que el borde traduce a un código de estado.</summary>
    public TipoDeError Tipo { get; }

    /// <summary>
    /// Qué campo concreto falla y por qué, cuando el error es de validación y se sabe. Vacío
    /// —nunca nulo— en cualquier otro caso.
    /// </summary>
    /// <remarks>
    /// El borde lo publica como la extensión <c>errors</c> del MISMO <c>ProblemDetails</c>
    /// (§9), que es donde el ADR-0004 dijo que entrarían: un segundo formato de error obligaría
    /// a cada cliente a distinguir dos formas de leer lo mismo.
    /// </remarks>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Campos { get; }

    /// <summary>Los datos recibidos no cumplen el contrato de entrada.</summary>
    public static ErrorDeOperacion Validacion(string codigo, string mensaje) =>
        Crear(codigo, mensaje, TipoDeError.Validacion, s_sinCampos);

    /// <summary>
    /// Los datos recibidos no cumplen el contrato de entrada, y se sabe exactamente qué campos.
    /// </summary>
    /// <remarks>
    /// Solo la validación tiene esta sobrecarga. Un 404 o un 409 no son de un campo, son del
    /// recurso entero; ofrecérsela a todos invitaría a inventar nombres de campo donde no los hay.
    /// </remarks>
    /// <param name="codigo">Identificador estable del error, que es contrato publicado.</param>
    /// <param name="mensaje">Qué puede hacer al respecto quien pidió la operación.</param>
    /// <param name="campos">Nombre del campo tal como viaja en el cuerpo, y sus incumplimientos.</param>
    public static ErrorDeOperacion Validacion(
        string codigo,
        string mensaje,
        IReadOnlyDictionary<string, IReadOnlyList<string>> campos)
    {
        ArgumentNullException.ThrowIfNull(campos);

        // Copia: el error viaja del caso de uso al borde, y si guardara el diccionario original
        // quien lo construyó podría seguir tocandolo después. Lo que se publica dejaría de ser
        // lo que se decidió publicar.
        return Crear(codigo, mensaje, TipoDeError.Validacion, new Dictionary<string, IReadOnlyList<string>>(campos));
    }

    /// <summary>Quien pide la operación no ha demostrado quién es.</summary>
    public static ErrorDeOperacion NoAutenticado(string codigo, string mensaje) =>
        Crear(codigo, mensaje, TipoDeError.NoAutenticado, s_sinCampos);

    /// <summary>Quien pide la operación no tiene permiso para hacerla.</summary>
    public static ErrorDeOperacion PermisoDenegado(string codigo, string mensaje) =>
        Crear(codigo, mensaje, TipoDeError.PermisoDenegado, s_sinCampos);

    /// <summary>Lo que la operación necesita no existe o no es visible.</summary>
    public static ErrorDeOperacion NoEncontrado(string codigo, string mensaje) =>
        Crear(codigo, mensaje, TipoDeError.NoEncontrado, s_sinCampos);

    /// <summary>El estado del recurso no admite la operación.</summary>
    public static ErrorDeOperacion Conflicto(string codigo, string mensaje) =>
        Crear(codigo, mensaje, TipoDeError.Conflicto, s_sinCampos);

    /// <summary>Otro escribió antes: la versión que trae el cliente ya no es la actual.</summary>
    public static ErrorDeOperacion VersionObsoleta(string codigo, string mensaje) =>
        Crear(codigo, mensaje, TipoDeError.VersionObsoleta, s_sinCampos);

    /// <summary>La operación exige decir sobre qué versión se escribe y no se ha dicho.</summary>
    public static ErrorDeOperacion FaltaLaVersion(string codigo, string mensaje) =>
        Crear(codigo, mensaje, TipoDeError.FaltaLaVersion, s_sinCampos);

    /// <summary>Una regla de negocio impide la operación.</summary>
    public static ErrorDeOperacion ReglaDeNegocio(string codigo, string mensaje) =>
        Crear(codigo, mensaje, TipoDeError.ReglaDeNegocio, s_sinCampos);

    private static ErrorDeOperacion Crear(
        string codigo,
        string mensaje,
        TipoDeError tipo,
        IReadOnlyDictionary<string, IReadOnlyList<string>> campos)
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

        return new ErrorDeOperacion(codigo, mensaje, tipo, campos);
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
