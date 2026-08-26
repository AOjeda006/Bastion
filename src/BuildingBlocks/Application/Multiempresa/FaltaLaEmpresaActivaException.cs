namespace Bastion.BuildingBlocks.Application.Multiempresa;

/// <summary>
/// Se ha consultado una entidad de inquilino sin empresa activa y sin haber abierto un ámbito sin
/// inquilino a propósito.
/// </summary>
/// <remarks>
/// <para>
/// Es un fallo del programa, no del usuario, y por eso es una excepción y no un
/// <c>Resultado</c> (ADR-0004): el borde ya devuelve <c>401</c> a quien no se ha identificado, así
/// que llegar hasta una consulta sin empresa significa que algo se ha escrito mal — una ruta que
/// debería exigir autenticación y no la exige, un trabajo de fondo que no declara su motivo, o un
/// emisor que ha dejado de escribir el <i>claim</i>.
/// </para>
/// <para>
/// Que reviente es la parte buena: la alternativa —seguir adelante sin filtro— es servir los datos
/// de todas las empresas, en silencio y con un <c>200</c>.
/// </para>
/// </remarks>
public sealed class FaltaLaEmpresaActivaException : InvalidOperationException
{
    private const string Explicacion =
        "Se ha consultado una entidad de inquilino sin empresa activa en el token y sin ámbito " +
        "sin inquilino abierto. O la ruta debería exigir autenticación y no la exige, o esta " +
        "operación es de las que corren sin empresa y le falta declarar su motivo.";

    /// <summary>Con la explicación de siempre.</summary>
    public FaltaLaEmpresaActivaException()
        : base(Explicacion)
    {
    }

    /// <summary>Con un mensaje propio.</summary>
    /// <param name="message">Qué ha pasado.</param>
    public FaltaLaEmpresaActivaException(string message)
        : base(message)
    {
    }

    /// <summary>Con un mensaje propio y la excepción que lo provocó.</summary>
    /// <param name="message">Qué ha pasado.</param>
    /// <param name="innerException">Lo que falló por debajo.</param>
    public FaltaLaEmpresaActivaException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
