namespace Bastion.BuildingBlocks.Domain.Eventos;

/// <summary>
/// Un hecho consumado que un módulo cuenta a los demás (§4, R12).
/// </summary>
/// <remarks>
/// <para>
/// <b>Es un hecho, no una orden.</b> Se nombra en pasado —«empresa creada», no «crear empresa»—
/// porque quien lo emite ya no controla lo que pase después: si nadie escucha, no ocurre nada y no
/// hay error que devolver. Un módulo que necesita que otro haga algo <b>ahora</b> no publica un
/// evento: llama al contrato de aquel, que es una llamada a método con su respuesta.
/// </para>
/// <para>
/// <b>Dónde se declara cada uno: en el <c>Contracts</c> del módulo que lo emite</b>, que es lo
/// único público de un módulo (§12). Aquí solo vive la forma común. Y de ahí sale la única
/// asimetría de este mecanismo: el <c>Domain</c> de un módulo no ve su <c>Contracts</c> —las
/// dependencias apuntan hacia dentro—, así que el evento lo construye la capa de aplicación, que
/// ve las dos, y se lo entrega a la raíz de agregado con
/// <see cref="RaizAgregado.Registrar(EventoDeIntegracion)"/>. Los <b>eventos de dominio</b>
/// síncronos dentro de un módulo, que sí nacerían en el dominio, son otra cosa y no son de este
/// ítem.
/// </para>
/// <para>
/// <b><see cref="EventoId"/> es la clave de deduplicación</b>, y por eso se pone al construirlo y
/// no al publicarlo: sobrevive a la serialización, viaja en la fila de la bandeja y es lo que mira
/// un consumidor para saber si este evento ya pasó por él. Se genera en versión 7 como el resto de
/// identificadores del sistema, así que además ordena por el instante en que se creó.
/// </para>
/// </remarks>
public abstract record EventoDeIntegracion
{
    /// <summary>Identificador del evento. Es lo que hace idempotente al consumidor.</summary>
    /// <remarks>
    /// Es <c>init</c> y no de solo lectura a propósito: al releer una fila de la bandeja hay que
    /// devolverle al evento el identificador con el que se guardó. Si se generara uno nuevo al
    /// deserializar, cada reentrega sería un evento distinto para el consumidor y la
    /// deduplicación no vería nunca dos veces lo mismo — que es exactamente el fallo que la
    /// deduplicación existe para evitar, y no daría ningún síntoma.
    /// </remarks>
    public Guid EventoId { get; init; } = Guid.CreateVersion7();
}
