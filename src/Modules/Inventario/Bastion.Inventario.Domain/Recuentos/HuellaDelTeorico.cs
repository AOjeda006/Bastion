using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Bastion.Inventario.Domain.Recuentos;

/// <summary>
/// La huella del teórico de un recuento: lo que la ficha enseña y la confirmación devuelve, para
/// saber si el mundo cambió entre las dos (ADR-0055 §2).
/// </summary>
/// <remarks>
/// <para>
/// <b>Es el SHA-256, en hexadecimal y en minúsculas, de las líneas ordenadas por su identificador</b>,
/// cada una escrita como <c>{id}:{teórico};</c>, con el identificador en 32 cifras sin guiones y el
/// teórico con seis decimales y punto.
/// </para>
/// <para>
/// <b>El orden es el del texto del identificador, comparado byte a byte</b>, y no el de
/// <see cref="Guid.CompareTo(Guid)"/>: ese depende de cómo guarda .NET los bytes de un
/// <see cref="Guid"/>, y el texto se lee igual en cualquier sitio.
/// </para>
/// <para>
/// <b>Seis decimales, que son los de la cantidad</b>, así que la escala del <see cref="decimal"/> no
/// cuenta: la base devuelve <c>5.000000</c> y el dominio puede tener <c>5</c>, y es el mismo teórico.
/// Y la cultura invariante, porque la del servidor no puede cambiar la huella.
/// </para>
/// </remarks>
public static class HuellaDelTeorico
{
    /// <summary>Calcula la huella de un teórico por línea.</summary>
    /// <param name="teoricos">El teórico de cada línea, por su identificador.</param>
    /// <returns>El SHA-256 en hexadecimal, en minúsculas.</returns>
    public static string De(IReadOnlyDictionary<Guid, decimal> teoricos)
    {
        ArgumentNullException.ThrowIfNull(teoricos);

        var texto = new StringBuilder(teoricos.Count * 48);

        foreach ((string id, decimal teorico) in teoricos
            .Select(par => (Id: par.Key.ToString("N"), Teorico: par.Value))
            .OrderBy(par => par.Id, StringComparer.Ordinal))
        {
            texto
                .Append(id)
                .Append(':')
                .Append(teorico.ToString("F6", CultureInfo.InvariantCulture))
                .Append(';');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto.ToString())));
    }
}
