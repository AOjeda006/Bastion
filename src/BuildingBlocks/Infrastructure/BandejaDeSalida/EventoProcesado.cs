namespace Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;

/// <summary>
/// La huella de que un consumidor concreto ya atendió un evento concreto.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es la cláusula «reprocesar no duplica», escrita como fila.</b> El publicador entrega al menos
/// una vez a propósito, así que un evento puede llegarle dos veces a un consumidor: la primera y
/// otra tras una caída a mitad. Sin esta tabla, la segunda vuelta vuelve a ejecutar el efecto.
/// </para>
/// <para>
/// <b>La clave es (evento, consumidor) y no solo el evento.</b> Un mismo hecho lo escuchan varios
/// módulos, y que Contabilidad ya haya asentado no dice nada sobre si Notificaciones ya avisó.
/// Con la clave solo por evento, el primer consumidor en terminar dejaría a los demás sin su turno.
/// </para>
/// <para>
/// <b>No lleva empresa.</b> No es un dato de negocio: es contabilidad interna del mecanismo, y la
/// empresa del hecho ya está en la fila de la bandeja. Queda declarada como global, con este
/// motivo, en <c>CadaEntidadDeclaraSuInquilinatoTests</c>.
/// </para>
/// </remarks>
public sealed class EventoProcesado
{
    /// <summary>Longitud máxima del nombre de un consumidor.</summary>
    public const int MaximoDelConsumidor = 128;

    // Constructor para EF Core.
    private EventoProcesado() => Consumidor = string.Empty;

    private EventoProcesado(Guid eventoId, string consumidor, DateTimeOffset procesadoEn)
    {
        EventoId = eventoId;
        Consumidor = consumidor;
        ProcesadoEn = procesadoEn;
    }

    /// <summary>Qué evento.</summary>
    public Guid EventoId { get; private set; }

    /// <summary>Qué consumidor. Es el nombre estable que declara el manejador.</summary>
    public string Consumidor { get; private set; }

    /// <summary>Cuándo terminó de atenderlo.</summary>
    public DateTimeOffset ProcesadoEn { get; private set; }

    /// <summary>Apunta que este consumidor ya atendió este evento.</summary>
    /// <param name="eventoId">Identificador del evento.</param>
    /// <param name="consumidor">Nombre estable del consumidor.</param>
    /// <param name="procesadoEn">Instante en que terminó.</param>
    /// <returns>La huella.</returns>
    public static EventoProcesado De(Guid eventoId, string consumidor, DateTimeOffset procesadoEn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumidor);

        return new EventoProcesado(eventoId, consumidor, procesadoEn);
    }
}
