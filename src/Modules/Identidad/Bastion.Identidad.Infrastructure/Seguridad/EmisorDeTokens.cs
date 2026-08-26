using System.Security.Claims;
using System.Security.Cryptography;
using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.Identidad.Application.Sesiones;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Bastion.Identidad.Infrastructure.Seguridad;

/// <summary>
/// Emite los dos tokens: firma el de acceso y sortea el de refresco.
/// </summary>
/// <remarks>
/// Las dos mitades del <see cref="IEmisorDeTokens"/> son deliberadamente distintas. El de acceso
/// <b>lleva dentro</b> quién es y qué puede, firmado, para poder validarse sin consultar nada. El
/// de refresco <b>no lleva nada</b>: son 256 bits de azar, y todo lo que significa está en su fila.
/// </remarks>
internal sealed class EmisorDeTokens(OpcionesDeJwt opciones, TimeProvider reloj) : IEmisorDeTokens
{
    private static readonly JsonWebTokenHandler s_manejador = new();

    public TimeSpan DuracionDelRefresco => opciones.DuracionDelRefresco;

    public TokenDeAcceso EmitirAcceso(
        Guid usuarioId,
        string nombre,
        Guid empresaId,
        IReadOnlyList<string> permisos)
    {
        ArgumentNullException.ThrowIfNull(permisos);

        DateTimeOffset ahora = reloj.GetUtcNow();
        DateTimeOffset expira = ahora + opciones.DuracionDelAcceso;

        var identidad = new ClaimsIdentity();
        identidad.AddClaim(new Claim(ClaimsDeBastion.Sujeto, usuarioId.ToString()));
        identidad.AddClaim(new Claim(ClaimsDeBastion.Nombre, nombre));

        // La empresa activa, FIRMADA. Es lo que hace que R8 no dependa de que cada caso de uso se
        // acuerde de comprobarla: para cambiarla hay que conseguir otro token, y para conseguirlo
        // hay que pertenecer a la empresa.
        identidad.AddClaim(new Claim(ClaimsDeBastion.Empresa, empresaId.ToString()));

        foreach (string permiso in permisos)
        {
            identidad.AddClaim(new Claim(ClaimsDeBastion.Permiso, permiso));
        }

        string jwt = s_manejador.CreateToken(new SecurityTokenDescriptor
        {
            Subject = identidad,
            Issuer = opciones.Emisor,
            Audience = opciones.Audiencia,
            IssuedAt = ahora.UtcDateTime,
            NotBefore = ahora.UtcDateTime,
            Expires = expira.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                opciones.Clave, SecurityAlgorithms.HmacSha256),
        });

        return new TokenDeAcceso(jwt, expira);
    }

    public RefrescoGenerado GenerarRefresco()
    {
        // 256 bits del generador criptográfico del sistema. Ni `Guid.NewGuid()` —que no promete
        // ser impredecible— ni `Random`, que directamente no lo es.
        byte[] bytes = RandomNumberGenerator.GetBytes(32);

        // Base64 URL: viaja en una cookie sin que nadie tenga que escaparlo, y volver a leerlo no
        // depende de qué haga con el `+` o el `/` el intermediario de turno.
        string valor = Base64UrlEncoder.Encode(bytes);

        return new RefrescoGenerado(valor, HashearRefresco(valor));
    }

    // SHA-256 a secas, sin sal ni coste: no es una contraseña. Un token de refresco son 256 bits
    // aleatorios, así que no hay diccionario que probar y encarecer el cálculo solo encarecería
    // cada renovación. Lo que sí hace falta es que la tabla no guarde el token en claro.
    public string HashearRefresco(string valor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valor);

        return Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(valor)));
    }
}
