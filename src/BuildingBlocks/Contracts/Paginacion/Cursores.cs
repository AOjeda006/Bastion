using System.Buffers.Text;

namespace Bastion.BuildingBlocks.Contracts.Paginacion;

/// <summary>
/// Cómo se escribe y se lee el cursor de un <see cref="Recorrido"/>: una posición, y nada más.
/// </summary>
/// <remarks>
/// <para>
/// <b>Qué significa aquí «opaco».</b> No significa cifrado: significa que el cliente no lo
/// construye ni lo interpreta, solo lo devuelve tal cual lo recibió. Esa es la propiedad que
/// permite cambiar mañana la clave del recorrido —hoy el identificador— sin romper a nadie, y la
/// que impide que alguien se invente un cursor «de la página 7». Lo que va dentro no es secreto:
/// es la posición del último elemento que el cliente YA tiene delante.
/// </para>
/// <para>
/// <b>Lo que NO va dentro es el criterio</b>, y ese es el motivo de que esto exista (ADR-0025). Un
/// cursor que llevara el criterio dentro devolvería el NIF al cliente en una cadena que se copia,
/// se comparte y acaba en un registro — la fuga que la búsqueda por cuerpo evita, entrando por la
/// puerta de al lado. El criterio lo reenvía el cliente en el cuerpo del siguiente <c>POST</c>.
/// </para>
/// <para>
/// La codificación es <b>Base64 para URL</b> aunque el cursor viaje en el cuerpo: es lo que hace
/// que quepa sin escapar en un JSON, en una cabecera o —el día que haga falta— en un enlace, sin
/// tener que decidirlo otra vez.
/// </para>
/// </remarks>
public static class Cursores
{
    /// <summary>Escribe el cursor que apunta a una posición.</summary>
    /// <param name="posicion">Identificador del último elemento entregado.</param>
    public static string De(Guid posicion) => Base64Url.EncodeToString(posicion.ToByteArray());

    /// <summary>
    /// Lee un cursor. Devuelve <c>false</c> en vez de lanzar: un cursor ilegible es una entrada
    /// del cliente, no un error de programación, y su desenlace es un <c>400</c> (ADR-0004).
    /// </summary>
    /// <param name="cursor">El cursor tal como llegó.</param>
    /// <param name="posicion">La posición leída, si el cursor era legible.</param>
    public static bool Intentar(string? cursor, out Guid posicion)
    {
        posicion = default;

        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        Span<byte> bytes = stackalloc byte[16];

        if (!Base64Url.TryDecodeFromChars(cursor, bytes, out int escritos) || escritos != 16)
        {
            return false;
        }

        posicion = new Guid(bytes);
        return true;
    }
}
