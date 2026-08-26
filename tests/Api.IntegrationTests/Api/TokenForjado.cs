using System.Security.Claims;
using System.Text;
using Bastion.BuildingBlocks.Application.Autorizacion;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Bastion.Api.IntegrationTests.Api;

/// <summary>
/// Fabrica tokens que el borde <b>tiene que rechazar</b>.
/// </summary>
/// <remarks>
/// <para>
/// Que un token se valide no se puede comprobar con tokens buenos: hay que presentar uno caducado,
/// uno de otro emisor, uno para otra audiencia y uno firmado con otra clave, y mirar qué contesta.
/// Sin esto, «se valida la caducidad» es una línea de configuración que nadie ha ejercido — y una
/// línea de configuración que no se ejerce es exactamente lo que se cae sin ruido.
/// </para>
/// <para>
/// <b>Ninguno de estos tokens se usa para entrar.</b> Los tests que necesitan credenciales las
/// consiguen iniciando sesión, como un cliente. Este ayudante existe para el caso contrario.
/// </para>
/// </remarks>
public static class TokenForjado
{
    /// <summary>Un token por lo demás correcto, pero con estos datos.</summary>
    /// <param name="emisor">Emisor que se le pone.</param>
    /// <param name="audiencia">Audiencia que se le pone.</param>
    /// <param name="expiraEn">Cuándo caduca.</param>
    /// <param name="clave">Clave con la que se firma.</param>
    public static string Con(string emisor, string audiencia, DateTime expiraEn, string clave)
    {
        SymmetricSecurityKey material = new(Encoding.UTF8.GetBytes(clave));

        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = emisor,
            Audience = audiencia,
            Expires = expiraEn,
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimsDeBastion.Sujeto, Guid.CreateVersion7().ToString()),
                new Claim(ClaimsDeBastion.Nombre, "Forjado"),
                new Claim(ClaimsDeBastion.Empresa, Guid.CreateVersion7().ToString()),
            ]),
            SigningCredentials = new SigningCredentials(material, SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>Un token válido al que se le ha tocado un carácter de la firma.</summary>
    /// <remarks>
    /// El más barato de los cuatro y el que más dice: si esto pasa, la firma no se está
    /// comprobando y cualquiera puede escribirse los permisos que quiera en el cuerpo del token.
    /// </remarks>
    /// <param name="token">Un token recién emitido por el sistema.</param>
    public static string ConLaFirmaTocada(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        char ultimo = token[^1];

        return string.Concat(token.AsSpan(0, token.Length - 1), ultimo == 'A' ? "B" : "A");
    }
}
