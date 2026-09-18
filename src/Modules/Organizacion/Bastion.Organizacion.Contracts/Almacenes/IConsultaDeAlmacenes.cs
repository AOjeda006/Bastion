using Bastion.Organizacion.Contracts.Comun;

namespace Bastion.Organizacion.Contracts.Almacenes;

/// <summary>
/// Lo que otros módulos pueden preguntar sobre los almacenes.
/// </summary>
/// <remarks>
/// <para>
/// Es la lectura entre módulos del §4: <b>interfaz del <c>Contracts</c> del módulo dueño, resuelta
/// en proceso</b>. Ni un <c>JOIN</c> contra <c>organizacion.almacenes</c> ni una llamada HTTP.
/// </para>
/// <para>
/// <b>Un almacén bloqueado contesta <see cref="EstadoDeMaestro.SoloResuelveLoViejo"/>, y no
/// <see cref="EstadoDeMaestro.NoExiste"/></b>, que es lo contrario de lo que contesta un tercero
/// bloqueado. La regla que decide cuál de las dos toca está en el ADR-0037: <b>lo que el bloqueo
/// reserva es la privacidad de una persona, no la existencia de una estantería</b>. De un tercero,
/// que exista ya es un dato sobre alguien; de un almacén no se revela nada de nadie, y a cambio
/// hace falta que un movimiento de hace tres años pueda seguir resolviendo el suyo.
/// </para>
/// <para>
/// <b>Que el puerto distinga no autoriza a que la respuesta HTTP distinga.</b> El invariante 2
/// habla de la respuesta, no del puerto: el alta que se rechaza contra un almacén bloqueado y la
/// que se rechaza contra uno inventado contestan el mismo error. Está escrito en
/// <c>EstadoDelTercero</c> —«estos valores son para que la regla decida, no para que la respuesta
/// HTTP los cuente»— y aquí se cita, no se reinventa.
/// </para>
/// </remarks>
public interface IConsultaDeAlmacenes
{
    /// <summary>En qué estado está ese almacén.</summary>
    /// <remarks>
    /// Un almacén de <b>otra empresa</b> contesta <see cref="EstadoDeMaestro.NoExiste"/>, igual que
    /// uno que no está: son la misma respuesta a propósito, porque distinguirlas diría que ese
    /// identificador es de alguien (R8).
    /// </remarks>
    /// <param name="almacenId">Identificador del almacén.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<EstadoDeMaestro> EstadoDeAsync(Guid almacenId, CancellationToken cancelacion);
}
