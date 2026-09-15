using Bastion.BuildingBlocks.Domain.Resultados;
using Microsoft.AspNetCore.Http;

namespace Bastion.BuildingBlocks.Infrastructure.CuerpoDeLaPeticion;

/// <summary>
/// Lee el cuerpo de una petición a memoria sin pasar nunca de un tope, y lo deja leído para quien
/// venga detrás.
/// </summary>
/// <remarks>
/// <para>
/// <b>El tope se impone mientras se lee, no después</b> (ADR-0034 §3). Un tope que se comprueba
/// después de haber copiado el cuerpo a memoria no es un tope: la memoria ya se ha gastado, y quien
/// manda un gigabyte ya ha conseguido lo que quería. Por eso las dos puertas van antes del volcado:
/// </para>
/// <list type="bullet">
///   <item>Si la petición <b>declara</b> un <c>Content-Length</c> mayor, se contesta sin leer un solo
///   byte.</item>
///   <item>Si no lo declara —un envío por trozos—, se leen <b>como mucho tope + 1</b> bytes. El byte
///   de más es el único modo de saber que había más sin seguir leyendo.</item>
/// </list>
/// <para>
/// <b>Lee una sola vez.</b> Lo leído se guarda como característica de la petición y el cuerpo se
/// sustituye por esa copia: el filtro de idempotencia, que necesita los bytes para la huella, y el
/// formateador que se los entrega a la acción ven los mismos, y ninguno vuelve a la red.
/// </para>
/// </remarks>
public static class LectorAcotadoDelCuerpo
{
    // Lo que se pide a la red en cada lectura. No limita nada: el límite lo pone lo que queda hasta
    // tope + 1.
    private const int Trozo = 64 * 1024;

    /// <summary>Lee el cuerpo, o dice que supera el tope.</summary>
    /// <param name="contexto">La petición.</param>
    /// <param name="tope">Cuántos bytes se admiten como mucho.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>Los bytes del cuerpo, o <see cref="ErroresDelCuerpo.DemasiadoGrande"/>.</returns>
    public static async Task<Resultado<ReadOnlyMemory<byte>>> LeerAsync(
        HttpContext contexto, long tope, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tope);

        // Un tope que no cabe en un array no es uno que este lector pueda cumplir: reventaría al
        // reservar. Es un error de quien lo declara, y se dice.
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(tope, (long)Array.MaxLength);

        if (contexto.Features.Get<CuerpoLeido>() is { } yaLeido)
        {
            return yaLeido.Bytes.Length > tope
                ? Resultado.Fallo<ReadOnlyMemory<byte>>(ErroresDelCuerpo.DemasiadoGrande(tope))
                : Resultado.Correcto<ReadOnlyMemory<byte>>(yaLeido.Bytes);
        }

        HttpRequest peticion = contexto.Request;

        if (peticion.ContentLength > tope)
        {
            return Resultado.Fallo<ReadOnlyMemory<byte>>(ErroresDelCuerpo.DemasiadoGrande(tope));
        }

        int limite = (int)tope + 1;
        using var acumulado = new MemoryStream((int)Math.Min(peticion.ContentLength ?? Trozo, limite));
        byte[] trozo = new byte[Math.Min(Trozo, limite)];

        while (acumulado.Length < limite)
        {
            int pedir = (int)Math.Min(trozo.Length, limite - acumulado.Length);
            int leidos = await peticion.Body.ReadAsync(trozo.AsMemory(0, pedir), cancelacion).ConfigureAwait(false);

            if (leidos == 0)
            {
                break;
            }

            acumulado.Write(trozo, 0, leidos);
        }

        if (acumulado.Length > tope)
        {
            return Resultado.Fallo<ReadOnlyMemory<byte>>(ErroresDelCuerpo.DemasiadoGrande(tope));
        }

        byte[] bytes = acumulado.ToArray();

        contexto.Features.Set(new CuerpoLeido(bytes));
        peticion.Body = new MemoryStream(bytes, writable: false);

        return Resultado.Correcto<ReadOnlyMemory<byte>>(bytes);
    }

    /// <summary>Los bytes del cuerpo, si ya los ha leído este lector; si no, nulo.</summary>
    /// <remarks>
    /// Para quien necesita el cuerpo <b>después</b> de los filtros y no tiene tope que imponer: el
    /// formateador. Si devuelve nulo, la acción no declaró <see cref="TopeDelCuerpoAttribute"/>, y
    /// leer entonces sería volcar sin tope.
    /// </remarks>
    /// <param name="contexto">La petición.</param>
    public static ReadOnlyMemory<byte>? Leido(HttpContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        // Con un `if` y no con `?.Bytes`. Un `byte[]` nulo se convierte en un `ReadOnlyMemory<byte>?`
        // CON valor —vacío—, no en nulo, y así estuvo hasta que lo destapó
        // `ElFormateadorDeCsvNoLeeLaRedTests`: el formateador nunca lanzaba y una acción sin tope
        // habría recibido un fichero vacío en silencio.
        if (contexto.Features.Get<CuerpoLeido>() is not { } leido)
        {
            return null;
        }

        return leido.Bytes;
    }

    private sealed class CuerpoLeido(byte[] bytes)
    {
        public byte[] Bytes { get; } = bytes;
    }
}
