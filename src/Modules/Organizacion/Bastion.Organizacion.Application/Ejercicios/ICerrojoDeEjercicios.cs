using Bastion.Organizacion.Domain.Ejercicios;

namespace Bastion.Organizacion.Application.Ejercicios;

/// <summary>
/// El cerrojo <b>exclusivo</b> sobre la fila de un ejercicio, con su estado traído en la misma
/// lectura.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué no basta el testigo de concurrencia (R11).</b> La R11 separa dos escrituras sobre la
/// misma fila: quien llega segundo se encuentra la versión cambiada y se lleva un 412. Aquí las dos
/// operaciones que hay que separar no escriben la misma fila —cerrar escribe la del ejercicio,
/// confirmar escribe un documento y no la toca—, así que no hay versión que chocar. Sin este
/// cerrojo, un documento que empezó a confirmarse un instante antes del cierre acaba dentro de un
/// periodo que, cuando su transacción termina, ya era definitivo; y nada habría fallado.
/// </para>
/// <para>
/// <b>Exclusivo aquí, compartido al confirmar</b>, que es el reparto que hace falta: muchas
/// confirmaciones a la vez sobre el mismo ejercicio son la operación normal y no se estorban entre
/// sí; un cierre no convive con ninguna. El exclusivo espera a que los compartidos suelten, y
/// mientras lo tiene, ninguna confirmación nueva pasa de su lectura.
/// </para>
/// <para>
/// <b>Y el estado viene con él, de la misma lectura.</b> Preguntar el estado por un lado y bloquear
/// por otro deja entre las dos órdenes exactamente la ventana que esto viene a cerrar: el estado
/// leído antes del cerrojo es un estado de antes.
/// </para>
/// </remarks>
public interface ICerrojoDeEjercicios
{
    /// <summary>
    /// Bloquea en exclusiva la fila del ejercicio hasta el <c>COMMIT</c> y devuelve el estado que
    /// tenía <b>en el momento de bloquearla</b>.
    /// </summary>
    /// <param name="id">El ejercicio.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>El estado con la fila ya bloqueada, o nulo si no hay tal fila en esta empresa.</returns>
    Task<EstadoDeEjercicio?> TomarEnExclusivaAsync(Guid id, CancellationToken cancelacion);
}
