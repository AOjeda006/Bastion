using Bastion.BuildingBlocks.Contracts.Maestros;
using Bastion.Organizacion.Contracts.Comun;

namespace Bastion.Organizacion.Contracts.Divisas;

/// <summary>
/// Lo que otros módulos pueden preguntar sobre las divisas.
/// </summary>
/// <remarks>
/// <para>
/// Es la lectura entre módulos del §4: <b>interfaz del <c>Contracts</c> del módulo dueño, resuelta
/// en proceso</b>. Ni un <c>JOIN</c> contra <c>organizacion.divisas</c> ni una llamada HTTP.
/// </para>
/// <para>
/// Lo necesita la <b>tarifa</b> del §7.3, que se expresa en una divisa, y detrás de ella todo lo que
/// lleve importe. No devuelve los decimales: cuántos tiene un euro lo dice el catálogo del código
/// —no una fila editable—, y esa decisión ya está escrita en <c>DivisaDto</c>.
/// </para>
/// </remarks>
public interface IConsultaDeDivisas
{
    /// <summary>En qué estado está esa divisa.</summary>
    /// <remarks>
    /// <see cref="EstadoDeMaestro.SoloResuelveLoViejo"/> es el estado de una divisa <b>retirada</b>
    /// (ADR-0023): una factura emitida en pesetas tiene que poder seguir diciendo en qué se emitió
    /// mucho después de que nadie pueda emitir una nueva. Hoy ninguna fila puede estar ahí porque la
    /// retirada llega en el ítem 1.7.
    /// </remarks>
    /// <param name="divisaId">Identificador de la divisa.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<EstadoDeMaestro> EstadoDeAsync(Guid divisaId, CancellationToken cancelacion);

    /// <summary>El código y el nombre de cada divisa, en una sola consulta.</summary>
    /// <remarks>
    /// <para>
    /// <b>Nombra lo que enseña su ficha</b>, la de
    /// <c>GET /api/v1/organizacion/divisas/{id}</c> (ADR-0074). Una divisa <b>retirada</b>
    /// vuelve, porque sigue resolviendo lo viejo y su ficha la enseña. Es un maestro de
    /// instalación (R8): no hay empresa que filtrar.
    /// </para>
    /// <para>
    /// <b>No mira el permiso de quien pregunta</b>: lo mira quien pregunta, antes de preguntar, como
    /// la pantalla. Lo que no vuelve, se escribe con su identificador.
    /// </para>
    /// </remarks>
    /// <param name="divisas">Las divisas por las que se pregunta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>El código y el nombre de cada una que vuelve.</returns>
    Task<IReadOnlyDictionary<Guid, NombreDeMaestro>> NombresDeAsync(
        IReadOnlyCollection<Guid> divisas, CancellationToken cancelacion);
}
