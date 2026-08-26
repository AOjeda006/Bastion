using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Bastion.Identidad.Infrastructure.Seguridad;

/// <summary>
/// Cómo se firma y cómo se valida el token de acceso.
/// </summary>
/// <remarks>
/// <para>
/// <b>La clave de firma sale de una variable de entorno y de ningún otro sitio.</b> No hay valor
/// por omisión, ni uno «solo para desarrollo», ni uno en <c>appsettings.json</c>: un secreto con
/// valor por omisión es un secreto conocido, y el día que alguien despliegue sin poner el suyo,
/// todo el mundo puede firmar tokens de administrador. Si falta, la aplicación <b>no arranca</b> —
/// que es la única manera de que ese despliegue no llegue a existir.
/// </para>
/// <para>
/// El emisor y la audiencia también son obligatorios y se validan en cada petición. Sin ellos, un
/// token firmado con la misma clave por otro sistema de la casa valdría aquí.
/// </para>
/// </remarks>
public sealed class OpcionesDeJwt
{
    /// <summary>Longitud mínima de la clave, en bytes.</summary>
    /// <remarks>
    /// HMAC-SHA256 exige una clave de al menos 256 bits; con menos, la propia biblioteca se niega
    /// a firmar. El tope se comprueba aquí, al arrancar, y no en la primera petición de acceso.
    /// </remarks>
    public const int BytesMinimosDeClave = 32;

    /// <summary>Nombre de la variable con la clave de firma.</summary>
    public const string VariableDeClave = "JWT_SIGNING_KEY";

    /// <summary>Nombre de la variable con el emisor.</summary>
    public const string VariableDeEmisor = "JWT_ISSUER";

    /// <summary>Nombre de la variable con la audiencia.</summary>
    public const string VariableDeAudiencia = "JWT_AUDIENCE";

    private OpcionesDeJwt(string emisor, string audiencia, SymmetricSecurityKey clave) =>
        (Emisor, Audiencia, Clave) = (emisor, audiencia, clave);

    /// <summary>Quién emite el token (<c>iss</c>).</summary>
    public string Emisor { get; }

    /// <summary>Para quién vale (<c>aud</c>).</summary>
    public string Audiencia { get; }

    /// <summary>La clave simétrica con la que se firma y se comprueba.</summary>
    public SymmetricSecurityKey Clave { get; }

    /// <summary>Cuánto vale un token de acceso.</summary>
    /// <remarks>
    /// Quince minutos (§11). Corto porque no se puede revocar: se valida con la firma, sin tocar
    /// la base de datos, así que dar de baja a alguien no lo echa hasta que su token caduque. Ese
    /// cuarto de hora es exactamente la ventana que se acepta.
    /// </remarks>
    public TimeSpan DuracionDelAcceso { get; } = TimeSpan.FromMinutes(15);

    /// <summary>Cuánto vale un token de refresco.</summary>
    /// <remarks>
    /// Catorce días: lo que dura una sesión de trabajo sin tener que volver a escribir la
    /// contraseña. Rota en cada uso, así que el que se robe sirve una vez y delata al ladrón.
    /// </remarks>
    public TimeSpan DuracionDelRefresco { get; } = TimeSpan.FromDays(14);

    /// <summary>Construye las opciones a partir de tres valores ya leídos.</summary>
    /// <param name="emisor">Valor de <c>JWT_ISSUER</c>.</param>
    /// <param name="audiencia">Valor de <c>JWT_AUDIENCE</c>.</param>
    /// <param name="clave">Valor de <c>JWT_SIGNING_KEY</c>.</param>
    /// <exception cref="InvalidOperationException">
    /// Si falta alguno o la clave es demasiado corta.
    /// </exception>
    public static OpcionesDeJwt De(string? emisor, string? audiencia, string? clave)
    {
        Exigir(emisor, VariableDeEmisor);
        Exigir(audiencia, VariableDeAudiencia);
        Exigir(clave, VariableDeClave);

        byte[] bytes = Encoding.UTF8.GetBytes(clave!);

        if (bytes.Length < BytesMinimosDeClave)
        {
            // Sin decir cuánto medía la que se ha puesto: el mensaje de un fallo de arranque acaba
            // en registros que ve mucha gente.
            throw new InvalidOperationException(
                $"La variable de entorno {VariableDeClave} tiene que medir al menos " +
                $"{BytesMinimosDeClave} bytes. Genere una con: openssl rand -base64 48");
        }

        return new OpcionesDeJwt(emisor!, audiencia!, new SymmetricSecurityKey(bytes));
    }

    private static void Exigir(string? valor, string variable)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new InvalidOperationException(
                $"Falta la variable de entorno {variable}. La aplicación no arranca sin ella: un " +
                "valor por omisión para un secreto es un secreto conocido.");
        }
    }
}
