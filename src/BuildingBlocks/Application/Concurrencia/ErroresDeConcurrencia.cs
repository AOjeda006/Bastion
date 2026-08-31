using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.BuildingBlocks.Application.Concurrencia;

/// <summary>
/// Los tres desenlaces de la concurrencia optimista, con sus códigos, que son contrato publicado.
/// </summary>
/// <remarks>
/// Están AQUÍ y no en cada módulo a propósito: el cliente que sabe tratar el <c>412</c> de un
/// almacén tiene que saber tratar el de una factura sin cambiar nada. Un código por módulo
/// obligaría a repetir la misma lógica en el frontal tantas veces como recursos haya.
/// </remarks>
public static class ErroresDeConcurrencia
{
    /// <summary>Código estable del <c>412</c>.</summary>
    public const string CodigoDeVersionObsoleta = "version-obsoleta";

    /// <summary>Código estable del <c>428</c>.</summary>
    public const string CodigoDeFaltaLaCabecera = "falta-if-match";

    /// <summary>Código estable del <c>400</c> por una cabecera ilegible.</summary>
    public const string CodigoDeCabeceraNoValida = "if-match-no-valido";

    /// <summary>La petición no dice sobre qué versión escribe.</summary>
    public static ErrorDeOperacion FaltaLaCabecera() => ErrorDeOperacion.FaltaLaVersion(
        CodigoDeFaltaLaCabecera,
        "Esta operación exige la cabecera If-Match con la versión del recurso. Léalo primero y " +
        "devuelva el ETag que trae su respuesta.");

    /// <summary>La cabecera viene, pero no se puede leer como una versión concreta.</summary>
    /// <param name="recibido">Lo que llegó, para que se vea en el mensaje.</param>
    public static ErrorDeOperacion CabeceraNoValida(string recibido) => ErrorDeOperacion.Validacion(
        CodigoDeCabeceraNoValida,
        $"La cabecera If-Match no es una versión válida: {recibido}. Se espera una sola etiqueta " +
        "fuerte y entrecomillada, la que devolvió el ETag de la lectura. El comodín * no vale: " +
        "se escribe sobre una versión concreta.");

    /// <summary>Otro guardó en medio, y además ya no queda recurso que versionar.</summary>
    /// <remarks>
    /// Sigue siendo <c>412</c> y no <c>404</c>: lo que ha fallado es la precondición que traía la
    /// petición. Un <c>404</c> diría «esa ruta no existe» y mandaría al cliente a comprobar la
    /// URL, cuando lo que ha pasado es que el recurso estaba y alguien lo quitó en medio.
    /// </remarks>
    public static ErrorDeOperacion ObsoletaYSinRecurso() => ErrorDeOperacion.VersionObsoleta(
        CodigoDeVersionObsoleta,
        "Este recurso ha cambiado o ha desaparecido mientras usted lo editaba. Vuelva a leerlo " +
        "antes de guardar.");

    /// <summary>Otro guardó en medio.</summary>
    /// <param name="version">La versión que el recurso tiene AHORA, para que el cliente la vea.</param>
    public static ErrorDeOperacion Obsoleta(VersionDeRecurso version) => ErrorDeOperacion.VersionObsoleta(
        CodigoDeVersionObsoleta,
        "Alguien ha guardado este recurso mientras usted lo editaba. Su versión actual es " +
        $"{version.Etiqueta}: vuelva a leerlo, compare y decida qué conservar.");
}
