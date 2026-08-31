using Bastion.BuildingBlocks.Application.Idempotencia;

namespace Bastion.BuildingBlocks.Infrastructure.Idempotencia;

/// <summary>
/// El lado de persistencia de la idempotencia: reclamar una clave, buscar lo que se respondió y
/// anotar la respuesta, todo <b>dentro de la transacción del trabajo</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hay uno por módulo, y se registran con clave.</b> Cada módulo tiene su <c>DbContext</c>, y la
/// transacción tiene que ser la del contexto que va a hacer el trabajo: registrar esto bajo el tipo
/// a secas dejaría que el último módulo registrado desplazara a los demás, y entonces la clave se
/// apuntaría en la transacción de un contexto y el trabajo ocurriría en la de otro —dos
/// transacciones, ninguna atomicidad, y ni un error—. Es el mismo motivo por el que la unidad de
/// trabajo tiene un puerto por módulo desde el 0.4.
/// </para>
/// <para>
/// <b>Vive en Infrastructure y no en Application</b> porque no lo usa ningún caso de uso: lo usa el
/// filtro del borde, que ya es HTTP. Un caso de uso no sabe que existen las cabeceras.
/// </para>
/// </remarks>
public interface IAlmacenDeIdempotencia
{
    /// <summary>Abre la transacción en la que van a caer la clave y el trabajo.</summary>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task AbrirTransaccionAsync(CancellationToken cancelacion);

    /// <summary>Confirma la transacción: el trabajo y su recibo, a la vez.</summary>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task ConfirmarAsync(CancellationToken cancelacion);

    /// <summary>Deshace la transacción: ni trabajo ni recibo, y la clave vuelve a estar libre.</summary>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task DeshacerAsync(CancellationToken cancelacion);

    /// <summary>
    /// Intenta quedarse con la clave. Devuelve si la ha conseguido.
    /// </summary>
    /// <remarks>
    /// Es <b>una sola sentencia</b> y no un «mira si está y si no insértala»: entre mirar e
    /// insertar cabe otra petición con la misma clave, y las dos harían el trabajo. Quien resuelve
    /// el empate es el índice de la clave primaria, dentro del motor.
    /// </remarks>
    /// <param name="clave">La tupla que identifica la petición.</param>
    /// <param name="huella">Huella del cuerpo que llega.</param>
    /// <param name="ahora">Instante de la reclamación.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<bool> ReclamarAsync(
        ClaveDeIdempotencia clave, string huella, DateTimeOffset ahora, CancellationToken cancelacion);

    /// <summary>Lo que se respondió la primera vez, si la clave ya estaba tomada.</summary>
    /// <param name="clave">La tupla que identifica la petición.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<RegistroDeIdempotencia?> BuscarAsync(ClaveDeIdempotencia clave, CancellationToken cancelacion);

    /// <summary>Anota la respuesta en la fila reclamada, sin confirmar todavía.</summary>
    /// <param name="respuesta">Lo que se le ha devuelto al cliente.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task GuardarRespuestaAsync(RespuestaGuardada respuesta, CancellationToken cancelacion);
}
