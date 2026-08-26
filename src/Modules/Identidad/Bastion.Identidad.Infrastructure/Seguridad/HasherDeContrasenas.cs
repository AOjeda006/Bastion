using System.Security.Cryptography;
using Bastion.Identidad.Application.Sesiones;
using Bastion.Identidad.Domain.Usuarios;
using Microsoft.AspNetCore.Identity;

namespace Bastion.Identidad.Infrastructure.Seguridad;

/// <summary>
/// El resumen de contraseñas, delegado en el <see cref="PasswordHasher{TUser}"/> de ASP.NET Core
/// Identity con sus parámetros por defecto.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aquí no se inventa criptografía.</b> En su versión 3, ese hasher usa PBKDF2 con HMAC-SHA512,
/// sal aleatoria de 128 bits, clave derivada de 256 bits y 100 000 iteraciones, y guarda todos
/// esos parámetros DENTRO de la cadena resultante. Qué algoritmo, con qué parámetros y por qué
/// —y qué pasa cuando haya que subir el coste— está escrito en el <b>ADR-0008</b>.
/// </para>
/// <para>
/// Se adopta solo el hasher, no Identity entero: su modelo de datos no cabe en un dominio con
/// pertenencias por empresa y permisos por acción.
/// </para>
/// </remarks>
internal sealed class HasherDeContrasenas : IHasherDeContrasenas
{
    private readonly PasswordHasher<Usuario> _hasher = new();

    /// <summary>
    /// El resumen de relleno, calculado una vez con los parámetros de ahora sobre una contraseña
    /// aleatoria.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El servicio es <c>Singleton</c>, así que este cálculo —que cuesta lo que cuesta comprobar
    /// una contraseña, a propósito— se paga al arrancar y no en cada intento de acceso.
    /// </para>
    /// <para>
    /// La contraseña de la que sale la genera <see cref="RandomNumberGenerator"/> y no se guarda
    /// en ninguna parte: al salir del constructor no existe. No es un secreto que haya que
    /// custodiar —comprobar contra este resumen SIEMPRE falla, que es justo lo que se quiere—,
    /// pero tampoco tiene por qué ser adivinable.
    /// </para>
    /// </remarks>
    public string HashDeRelleno { get; }

    public HasherDeContrasenas() =>
        HashDeRelleno = _hasher.HashPassword(
            null!,
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    public string Hashear(string contrasena) => _hasher.HashPassword(null!, contrasena);

    public ResultadoDeComprobacion Comprobar(string hash, string contrasena) =>
        _hasher.VerifyHashedPassword(null!, hash, contrasena) switch
        {
            PasswordVerificationResult.Success => ResultadoDeComprobacion.Correcta,

            // El hasher avisa cuando el resumen se calculó con una versión o un coste anteriores.
            // Ese aviso es lo único que hace que subir el coste llegue a las cuentas que ya
            // existen; ignorarlo dejaría a todo el que ya tenga cuenta con el coste del día que
            // se dio de alta, para siempre.
            PasswordVerificationResult.SuccessRehashNeeded =>
                ResultadoDeComprobacion.CorrectaPeroConvieneRehashear,

            _ => ResultadoDeComprobacion.Incorrecta,
        };
}
