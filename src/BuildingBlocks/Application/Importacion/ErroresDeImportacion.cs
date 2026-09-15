using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.BuildingBlocks.Application.Importacion;

/// <summary>
/// Lo que rechaza un fichero ENTERO antes de mirar ninguna fila, con sus códigos, que son contrato
/// publicado (ADR-0034 §5).
/// </summary>
/// <remarks>
/// <para>
/// <b>Un error de fichero es el que impide saber qué es una fila.</b> Con otra codificación, otro
/// separador o unas comillas que no se cierran, no hay manera de decir dónde empieza cada campo, y
/// cualquier informe por filas sería inventado. Lo demás —un importe mal escrito, una comilla fuera
/// de sitio, una fila con campos de menos— es motivo de fila, y las buenas entran.
/// </para>
/// <para>
/// <b>Ninguno repite nada del fichero</b>: ni la cabecera que llegó ni el trozo que falló. Dicen qué
/// se esperaba, que es lo que hace falta para arreglarlo.
/// </para>
/// </remarks>
public static class ErroresDeImportacion
{
    /// <summary>Código estable del <c>400</c> por una codificación que no es del dialecto.</summary>
    public const string CodigoDeCodificacionNoAdmitida = "importacion-codificacion-no-admitida";

    /// <summary>Código estable del <c>400</c> por una primera fila que no es la cabecera.</summary>
    public const string CodigoDeCabeceraNoValida = "importacion-cabecera-no-valida";

    /// <summary>Código estable del <c>400</c> por una cabecera que casaría con otro separador.</summary>
    public const string CodigoDeSeparadorNoAdmitido = "importacion-separador-no-admitido";

    /// <summary>Código estable del <c>400</c> por un retorno de carro suelto.</summary>
    public const string CodigoDeFinDeLineaNoAdmitido = "importacion-fin-de-linea-no-admitido";

    /// <summary>Código estable del <c>400</c> por unas comillas que no se cierran.</summary>
    public const string CodigoDeComillasSinCerrar = "importacion-comillas-sin-cerrar";

    /// <summary>Código estable del <c>413</c> por pasar del tope de filas.</summary>
    public const string CodigoDeDemasiadasFilas = "importacion-demasiadas-filas";

    /// <summary>UTF-16, UTF-32 o un byte nulo: nada de eso lo escribe un Excel en español.</summary>
    public static ErrorDeOperacion CodificacionNoAdmitida() => ErrorDeOperacion.Validacion(
        CodigoDeCodificacionNoAdmitida,
        "El fichero no está en UTF-8 ni en Windows-1252. Guárdelo desde la hoja de cálculo como " +
        "«CSV UTF-8» o como «CSV (delimitado por comas)».");

    /// <summary>La primera fila no es la cabecera, escrita tal cual y en su orden.</summary>
    /// <param name="cabecera">Las columnas que se esperaban, para que el mensaje las diga.</param>
    public static ErrorDeOperacion CabeceraNoValida(IReadOnlyList<string> cabecera)
    {
        ArgumentNullException.ThrowIfNull(cabecera);

        return ErrorDeOperacion.Validacion(
            CodigoDeCabeceraNoValida,
            "La primera fila tiene que ser la cabecera, con estas columnas en este orden y " +
            $"separadas por punto y coma: {string.Join(';', cabecera)}.");
    }

    /// <summary>La cabecera casaría separada por comas o por tabuladores.</summary>
    /// <remarks>
    /// Es un diagnóstico y no una detección: el fichero no se lee con el otro separador. Solo se
    /// mira si la primera fila casaría, para poder decir por qué no casa.
    /// </remarks>
    public static ErrorDeOperacion SeparadorNoAdmitido() => ErrorDeOperacion.Validacion(
        CodigoDeSeparadorNoAdmitido,
        "Los campos tienen que ir separados por punto y coma, que es como guarda el CSV una hoja " +
        "de cálculo en español. Este fichero usa otro separador.");

    /// <summary>Un retorno de carro que no va seguido de salto de línea, fuera de comillas.</summary>
    public static ErrorDeOperacion FinDeLineaNoAdmitido() => ErrorDeOperacion.Validacion(
        CodigoDeFinDeLineaNoAdmitido,
        "Las filas tienen que acabar en CRLF o en LF. Este fichero tiene un retorno de carro suelto, " +
        "que es el fin de línea de otros sistemas.");

    /// <summary>Unas comillas abiertas que llegan al final del fichero sin cerrarse.</summary>
    public static ErrorDeOperacion ComillasSinCerrar() => ErrorDeOperacion.Validacion(
        CodigoDeComillasSinCerrar,
        "Hay unas comillas que se abren y no se cierran, así que no se puede saber dónde acaba el " +
        "campo. Revise el fichero: dentro de un campo entre comillas, cada comilla va doble.");

    /// <summary>Más filas de las que admite una importación, contando las vacías.</summary>
    /// <param name="tope">El tope, para que el mensaje lo diga.</param>
    public static ErrorDeOperacion DemasiadasFilas(int tope) => ErrorDeOperacion.DemasiadoGrande(
        CodigoDeDemasiadasFilas,
        $"El fichero pasa de las {tope} filas que admite una importación, contando las vacías. " +
        "Pártalo en varios ficheros e impórtelos por separado.");
}
