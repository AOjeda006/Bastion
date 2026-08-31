using System.Security.Cryptography;
using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.BuildingBlocks.Application.Idempotencia;

/// <summary>
/// La identidad de una petición repetible: <b>la tupla entera</b> —empresa, usuario, método, ruta
/// y clave— y la huella de lo que se pidió.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué la tupla y no la clave sola.</b> La clave la elige el cliente, y dos clientes
/// distintos eligen la misma antes o después: <c>1</c>, <c>test</c>, el mismo UUID de una
/// plantilla copiada. Con la clave sola por identidad, el segundo recibiría la respuesta guardada
/// del primero —la factura de otra empresa, el usuario de otro— y el fallo se vería como un dato
/// correcto. La empresa y el usuario están dentro por eso, y el método y la ruta porque la misma
/// clave contra <c>POST /almacenes</c> y contra <c>POST /series</c> son dos operaciones y nadie
/// dijo lo contrario.
/// </para>
/// <para>
/// <b>La huella se calcula sobre los BYTES DEL CUERPO tal como llegaron</b>, antes de
/// deserializar nada, y es un SHA-256 en hexadecimal. Sobre el objeto ya deserializado dependería
/// del serializador y de sus opciones, y cambiar una opción cambiaría la identidad de peticiones
/// ya guardadas. La contrapartida, dicha para que nadie la descubra depurando: dos cuerpos que
/// solo se diferencian en espacios en blanco tienen huellas distintas, así que el segundo intento
/// se rechaza con un <c>409</c> en vez de repetir la respuesta. Se prefiere ese error a devolver
/// el desenlace de una petición que no es exactamente la que se hizo.
/// </para>
/// <para>
/// El cuerpo <b>no se guarda</b>: se guarda su huella. Es lo único que hace falta para responder
/// «esto no es lo que pediste antes», y evita que una tabla de servicio acabe siendo una segunda
/// copia de todo lo que ha entrado por la API.
/// </para>
/// </remarks>
/// <param name="EmpresaId">Empresa activa del <i>claim</i> (R8).</param>
/// <param name="UsuarioId">Quién pide la operación.</param>
/// <param name="Metodo">Método HTTP, en mayúsculas.</param>
/// <param name="Ruta">Ruta de la petición, sin la cadena de consulta.</param>
/// <param name="Clave">Lo que venía en la cabecera <c>Idempotency-Key</c>.</param>
public sealed record ClaveDeIdempotencia(
    Guid EmpresaId,
    Guid UsuarioId,
    string Metodo,
    string Ruta,
    string Clave)
{
    /// <summary>Longitud máxima de la clave que manda el cliente.</summary>
    /// <remarks>
    /// Ciento veintiocho da de sobra para un UUID, para un ULID y para un identificador compuesto
    /// del cliente, y pone un techo a lo que un tercero puede hacer crecer en una tabla nuestra
    /// mandando cabeceras enormes.
    /// </remarks>
    public const int MaximoDeLaClave = 128;

    /// <summary>Longitud máxima de la ruta que se guarda.</summary>
    public const int MaximoDeLaRuta = 512;

    /// <summary>Longitud del hexadecimal de un SHA-256.</summary>
    public const int LongitudDeLaHuella = 64;

    /// <summary>Valida la cabecera y forma la clave.</summary>
    /// <param name="empresaId">Empresa activa.</param>
    /// <param name="usuarioId">Usuario que pide.</param>
    /// <param name="metodo">Método HTTP.</param>
    /// <param name="ruta">Ruta de la petición.</param>
    /// <param name="cabecera">Valor crudo de la cabecera.</param>
    public static Resultado<ClaveDeIdempotencia> De(
        Guid empresaId,
        Guid usuarioId,
        string metodo,
        string ruta,
        string? cabecera)
    {
        string limpia = cabecera?.Trim() ?? string.Empty;

        if (limpia.Length is 0 or > MaximoDeLaClave)
        {
            return Resultado.Fallo<ClaveDeIdempotencia>(
                ErroresDeIdempotencia.ClaveNoValida(MaximoDeLaClave));
        }

        // La ruta se recorta y no se rechaza: una ruta larguísima no es culpa del cliente que
        // manda la cabecera, y lo que hace falta de ella es distinguir operaciones. Recortada por
        // el final sigue distinguiendo el módulo y el recurso, que es donde está la diferencia.
        string acortada = ruta.Length <= MaximoDeLaRuta ? ruta : ruta[..MaximoDeLaRuta];

        return Resultado.Correcto(new ClaveDeIdempotencia(
            empresaId, usuarioId, metodo.ToUpperInvariant(), acortada, limpia));
    }

    /// <summary>Huella del cuerpo, en hexadecimal minúsculo.</summary>
    /// <param name="cuerpo">Los bytes tal como llegaron. Vacío si no había cuerpo.</param>
    public static string HuellaDe(ReadOnlySpan<byte> cuerpo)
    {
        Span<byte> resumen = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(cuerpo, resumen);

        return Convert.ToHexStringLower(resumen);
    }
}
