using Bastion.BuildingBlocks.Domain.Eventos;

namespace Bastion.BuildingBlocks.Application.Eventos;

/// <summary>
/// Quien reacciona a un evento de integración de otro módulo.
/// </summary>
/// <remarks>
/// <para>
/// <b>No es genérica, y eso es a propósito.</b> El despachador recibe del almacén un evento del que
/// solo conoce el tipo en ejecución; con una interfaz genérica tendría que cerrarla por reflexión
/// (<c>MakeGenericType</c>) y resolverla del contenedor por un tipo construido, que es la clase de
/// código que falla en el arranque de producción y no en ningún test. Aquí se resuelven todos los
/// manejadores registrados y cada uno dice a qué evento atiende. La comodidad de escribir el
/// manejador con su tipo concreto la da <see cref="ManejadorDeEvento{T}"/>, que es lo que se hereda.
/// </para>
/// <para>
/// <b>Un manejador no devuelve nada.</b> Quien publica es un trabajo de fondo, no una petición: no
/// hay a quién contestarle. Si el manejador no puede hacer su trabajo, lanza — y entonces el evento
/// se reintenta (ver <c>PublicadorDeLaBandeja</c>).
/// </para>
/// </remarks>
public interface IManejadorDeEvento
{
    /// <summary>
    /// Nombre estable de este consumidor. Es la mitad de la clave de deduplicación.
    /// </summary>
    /// <remarks>
    /// Estable quiere decir que <b>no</b> se deriva del nombre del tipo: renombrar la clase
    /// dejaría de reconocer todo lo que ya había procesado y lo volvería a procesar entero, en
    /// silencio y una sola vez —el peor momento para descubrirlo—.
    /// </remarks>
    string Consumidor { get; }

    /// <summary>A qué evento atiende.</summary>
    Type EventoQueAtiende { get; }

    /// <summary>Reacciona al evento.</summary>
    /// <param name="evento">El hecho ocurrido, ya del tipo que dice <see cref="EventoQueAtiende"/>.</param>
    /// <param name="cancelacion">Cancelación de la parada del trabajo de fondo.</param>
    Task ManejarAsync(EventoDeIntegracion evento, CancellationToken cancelacion);
}

/// <summary>
/// Base cómoda de un manejador: recibe su evento ya del tipo concreto.
/// </summary>
/// <typeparam name="T">Evento al que atiende.</typeparam>
public abstract class ManejadorDeEvento<T> : IManejadorDeEvento
    where T : EventoDeIntegracion
{
    /// <inheritdoc/>
    public abstract string Consumidor { get; }

    /// <inheritdoc/>
    public Type EventoQueAtiende => typeof(T);

    /// <inheritdoc/>
    public Task ManejarAsync(EventoDeIntegracion evento, CancellationToken cancelacion) =>
        AtenderAsync((T)evento, cancelacion);

    /// <summary>Reacciona al evento, ya del tipo concreto.</summary>
    /// <remarks>
    /// Se llama distinto que el método de la interfaz y no es un descuido: dos sobrecargas del
    /// mismo nombre que se diferencian por el tipo del parámetro chocarían el día que alguien
    /// heredara con <c>T</c> igual a <see cref="EventoDeIntegracion"/>.
    /// </remarks>
    /// <param name="evento">El hecho ocurrido.</param>
    /// <param name="cancelacion">Cancelación de la parada del trabajo de fondo.</param>
    protected abstract Task AtenderAsync(T evento, CancellationToken cancelacion);
}
