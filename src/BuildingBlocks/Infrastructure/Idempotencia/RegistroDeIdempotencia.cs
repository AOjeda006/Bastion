using Bastion.BuildingBlocks.Application.Idempotencia;
using Bastion.BuildingBlocks.Domain.Multiempresa;

namespace Bastion.BuildingBlocks.Infrastructure.Idempotencia;

/// <summary>
/// Una petición ya atendida bajo una <c>Idempotency-Key</c>, con la respuesta que se le dio (R10).
/// </summary>
/// <remarks>
/// <para>
/// <b>La invariante de la tabla: la fila existe si y solo si el trabajo ocurrió.</b> La fila se
/// reclama <b>dentro</b> de la misma transacción que hace el trabajo y se completa antes de
/// confirmarla, así que nadie ve nunca una fila a medias: o se confirma todo —el cambio de
/// negocio, su traza, sus eventos y esta fila— o no se confirma nada y la clave vuelve a estar
/// libre para el reintento. Ese es el motivo por el que aquí no hay ninguna columna de «estado»:
/// no hay un estado intermedio que representar.
/// </para>
/// <para>
/// <b>Las columnas de la respuesta son anulables y la invariante sigue en pie.</b> Lo son porque la
/// fila nace antes de que exista la respuesta —hay que reclamar la clave ANTES de trabajar, o dos
/// peticiones simultáneas trabajarían las dos—, y una restricción <c>NOT NULL</c> se comprueba en
/// el instante del <c>INSERT</c>, no al confirmar. Quien garantiza que toda fila <b>confirmada</b>
/// está completa es la transacción, no la columna.
/// </para>
/// <para>
/// <b>El cuerpo se guarda como texto y no como <c>jsonb</c></b>, al revés que la bandeja de salida.
/// No es una incoherencia: <c>jsonb</c> normaliza —reordena claves y se come los espacios—, y lo
/// que hay que devolver en la repetición son <b>los mismos bytes</b> que se enviaron la primera
/// vez. Un cliente que compare respuestas, o que verifique una firma sobre el cuerpo, notaría la
/// diferencia. La bandeja guarda un hecho que se vuelve a serializar al publicarlo; esto guarda
/// una respuesta que se vuelve a emitir tal cual.
/// </para>
/// <para>
/// <b>Solo dos cabeceras se guardan</b>, <c>ETag</c> y <c>Location</c>, cada una en su columna con
/// su nombre. No hay un saco de cabeceras a propósito: con un saco, cualquier cabecera que la
/// tubería añadiera el día de mañana —una cookie de sesión, una autorización renovada— entraría en
/// la tabla sin que nadie lo decidiera. Con dos columnas, meter una tercera es un cambio de
/// esquema que se lee en la revisión.
/// </para>
/// </remarks>
public sealed class RegistroDeIdempotencia : IDeInquilino
{
    // Constructor para EF Core. Las propiedades se rellenan por reflexión al materializar.
    private RegistroDeIdempotencia()
    {
        Metodo = string.Empty;
        Ruta = string.Empty;
        Clave = string.Empty;
        Huella = string.Empty;
    }

    /// <summary>La fila que se acaba de reclamar, para seguirle la pista sin volver a leerla.</summary>
    /// <remarks>
    /// <b>Interno a propósito.</b> Quien la construya la va a insertar, y la única inserción
    /// correcta es la del almacén, que la hace dentro de la transacción del trabajo. Pública, esta
    /// fábrica sería la puerta por la que alguien acabe apuntando una clave sin hacer el trabajo
    /// —o al revés—, que es justo lo que la tabla existe para impedir.
    /// </remarks>
    internal static RegistroDeIdempotencia Reclamada(
        ClaveDeIdempotencia clave, string huella, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(clave);

        return new RegistroDeIdempotencia
        {
            EmpresaId = clave.EmpresaId,
            UsuarioId = clave.UsuarioId,
            Metodo = clave.Metodo,
            Ruta = clave.Ruta,
            Clave = clave.Clave,
            Huella = huella,
            CreadaEn = ahora,
        };
    }

    /// <summary>Empresa activa de quien pidió la operación (R8). Parte de la clave.</summary>
    public Guid EmpresaId { get; private set; }

    /// <summary>Quién pidió la operación. Parte de la clave.</summary>
    public Guid UsuarioId { get; private set; }

    /// <summary>Método HTTP, en mayúsculas. Parte de la clave.</summary>
    public string Metodo { get; private set; }

    /// <summary>Ruta de la petición, sin cadena de consulta. Parte de la clave.</summary>
    public string Ruta { get; private set; }

    /// <summary>La clave que mandó el cliente. Parte de la clave.</summary>
    public string Clave { get; private set; }

    /// <summary>SHA-256 en hexadecimal de los bytes del cuerpo, tal como llegaron.</summary>
    public string Huella { get; private set; }

    /// <summary>Cuándo se reclamó la clave.</summary>
    public DateTimeOffset CreadaEn { get; private set; }

    /// <summary>Código de estado de la respuesta que se dio.</summary>
    public int? CodigoDeEstado { get; private set; }

    /// <summary>Cuerpo de la respuesta, tal cual se envió.</summary>
    public string? Cuerpo { get; private set; }

    /// <summary>Tipo de contenido de la respuesta.</summary>
    public string? TipoDeContenido { get; private set; }

    /// <summary>La cabecera <c>ETag</c> que llevaba la respuesta, si llevaba.</summary>
    public string? Etiqueta { get; private set; }

    /// <summary>La cabecera <c>Location</c> que llevaba la respuesta, si llevaba.</summary>
    public string? Ubicacion { get; private set; }

    /// <summary>Si la fila ya tiene guardada su respuesta.</summary>
    /// <remarks>
    /// Una fila leída desde la base de datos siempre la tiene: ver la invariante de arriba. Esto
    /// existe para que el código que la lee no lo dé por hecho en silencio.
    /// </remarks>
    public bool TieneRespuesta => CodigoDeEstado is not null;

    /// <summary>La respuesta que se guardó, para volver a emitirla.</summary>
    public RespuestaGuardada Respuesta => new(
        CodigoDeEstado ?? throw new InvalidOperationException(
            "Este registro de idempotencia no tiene respuesta guardada. Una fila confirmada " +
            "siempre la tiene, así que llegar aquí significa que alguien ha escrito en la tabla " +
            "fuera de la transacción que hace el trabajo."),
        Cuerpo,
        TipoDeContenido,
        Etiqueta,
        Ubicacion);

    /// <summary>Si el cuerpo de esta petición es el mismo que el de la que se guardó.</summary>
    /// <param name="huella">Huella del cuerpo que llega ahora.</param>
    public bool CoincideElCuerpo(string huella) =>
        string.Equals(Huella, huella, StringComparison.Ordinal);

    /// <summary>Anota la respuesta que se acaba de producir.</summary>
    /// <param name="respuesta">Lo que se le devolvió al cliente.</param>
    public void Guardar(RespuestaGuardada respuesta)
    {
        ArgumentNullException.ThrowIfNull(respuesta);

        CodigoDeEstado = respuesta.CodigoDeEstado;
        Cuerpo = respuesta.Cuerpo;
        TipoDeContenido = respuesta.TipoDeContenido;
        Etiqueta = respuesta.Etiqueta;
        Ubicacion = respuesta.Ubicacion;
    }
}

/// <summary>La respuesta que se guarda y se vuelve a emitir en la repetición.</summary>
/// <param name="CodigoDeEstado">Código de estado HTTP.</param>
/// <param name="Cuerpo">Cuerpo, tal cual se envió.</param>
/// <param name="TipoDeContenido">Tipo de contenido de la respuesta.</param>
/// <param name="Etiqueta">Cabecera <c>ETag</c>, si la había.</param>
/// <param name="Ubicacion">Cabecera <c>Location</c>, si la había.</param>
public sealed record RespuestaGuardada(
    int CodigoDeEstado,
    string? Cuerpo,
    string? TipoDeContenido,
    string? Etiqueta,
    string? Ubicacion);
