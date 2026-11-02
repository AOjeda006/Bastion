using System.Diagnostics;

namespace Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;

/// <summary>
/// Las actividades de la bandeja de salida: una por manejador que atiende un evento.
/// </summary>
/// <remarks>
/// <para>
/// <b>La del manejador es hija de la que escribió el evento, y no un enlace</b> (3.4, hueco 3). La
/// fila guarda el <c>traceparent</c> de la actividad en la que se escribió, y de él cuelga esta: la
/// petición que da de alta una empresa y lo que hacen después sus manejadores se leen en el visor
/// como una sola traza, que es la pregunta que se hace quien la mira («¿qué pasó por culpa de
/// esto?»). Un enlace contestaría a otra, la de un lote que junta varias causas, y la bandeja
/// entrega de una en una.
/// </para>
/// <para>
/// Es una fuente <c>static</c>, como la recomienda .NET: sin nadie escuchando, crear una actividad
/// no cuesta nada y devuelve <c>null</c>. Por eso la causa que hereda el manejador no depende de
/// que haya un oyente: si no lo hay, su traza es la de la fila.
/// </para>
/// </remarks>
public static class TrazasDeLaBandeja
{
    /// <summary>Nombre de la fuente. Es lo que hay que registrar en OpenTelemetry.</summary>
    public const string Fuente = "Bastion.BandejaDeSalida";

    /// <summary>El sistema de mensajería, como lo nombran las convenciones semánticas.</summary>
    public const string Sistema = "bastion.bandeja_de_salida";

    private static readonly ActivitySource s_fuente = new(Fuente);

    /// <summary>Abre la actividad de un manejador que atiende el evento de una fila.</summary>
    /// <param name="fila">La fila del evento, con el <c>traceparent</c> en el que se escribió.</param>
    /// <param name="consumidor">Quién lo atiende.</param>
    /// <returns>La actividad, o <c>null</c> si nadie la escucha.</returns>
    internal static Activity? Atender(EventoDeLaBandeja fila, string consumidor)
    {
        // El padre llega de fuera de la memoria del proceso —de la base, quizá de otra réplica—, así
        // que es remoto. Sin traza de origen —una fila de antes del 3.4, o escrita fuera de una
        // actividad—, el padre es el que haya en curso, como en cualquier actividad: en la vuelta
        // del publicador no hay ninguno, y la del manejador nace raíz.
        ActivityContext padre = ActivityContext.TryParse(
            fila.ContextoDeTraza,
            traceState: null,
            isRemote: true,
            out ActivityContext deLaFila)
            ? deLaFila
            : default;

        Activity? actividad = s_fuente.StartActivity(
            ActivityKind.Consumer,
            padre,
            name: $"process {fila.Nombre}");

        actividad?.SetTag("messaging.system", Sistema);
        actividad?.SetTag("messaging.operation.type", "process");
        actividad?.SetTag("messaging.destination.name", fila.Nombre);
        actividad?.SetTag("messaging.message.id", fila.EventoId.ToString());
        actividad?.SetTag("messaging.consumer.group.name", consumidor);

        return actividad;
    }
}
