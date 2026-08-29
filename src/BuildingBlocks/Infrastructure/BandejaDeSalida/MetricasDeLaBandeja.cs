using System.Diagnostics.Metrics;

namespace Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;

/// <summary>
/// Lo que la bandeja de salida cuenta de sí misma.
/// </summary>
/// <remarks>
/// <para>
/// <b>La medida que importa es la EDAD del más viejo sin publicar, no cuántos hay.</b> El tamaño de
/// la cola sube y baja con el tráfico y no distingue «mil eventos que se van a publicar en dos
/// segundos» de «uno atascado desde ayer». La edad sí: si el publicador funciona, se queda pegada
/// al intervalo de sondeo pase lo que pase con el volumen; si deja de funcionar, crece sola y sin
/// techo. Es la diferencia entre una alarma que hay que calibrar por entorno y una que dice lo
/// mismo en todos.
/// </para>
/// <para>
/// <b>Y no va en la sonda de vida</b> —la lección del 0.2—: una cola atascada no significa que el
/// proceso esté colgado, y meterla ahí haría que el orquestador reiniciara la API en bucle, que
/// además de no vaciar la cola tira las peticiones que sí se estaban atendiendo. Tampoco va en la
/// de disponibilidad, y esto sí es una decisión y no un olvido: esa sonda contesta «puedo atender
/// tráfico», y una API con la bandeja atrasada atiende tráfico perfectamente. Sacarla de rotación
/// por un problema de fondo convertiría un retraso en una caída. Esto se vigila con una alerta
/// sobre la métrica, que es la herramienta que existe para eso.
/// </para>
/// <para>
/// El valor lo publica el propio publicador en cada vuelta, y el medidor solo lo lee: la devolución
/// de llamada de un instrumento observable es síncrona y la recolecta el exportador en su propio
/// hilo, así que consultar la base desde ahí sería bloquear al exportador contra la red.
/// </para>
/// </remarks>
public sealed class MetricasDeLaBandeja : IDisposable
{
    /// <summary>Nombre del medidor. Es lo que hay que registrar en OpenTelemetry.</summary>
    public const string Medidor = "Bastion.BandejaDeSalida";

    private readonly Meter _medidor = new(Medidor);
    private readonly Counter<long> _publicados;
    private readonly Counter<long> _aparcados;

    private double _antiguedad;

    /// <summary>Crea los instrumentos.</summary>
    public MetricasDeLaBandeja()
    {
        _publicados = _medidor.CreateCounter<long>(
            "bastion.bandeja_de_salida.publicados",
            unit: "{evento}",
            description: "Eventos entregados a todos sus manejadores sin fallo.");

        _aparcados = _medidor.CreateCounter<long>(
            "bastion.bandeja_de_salida.aparcados",
            unit: "{evento}",
            description: "Eventos que han fallado tantas veces seguidas que se han dejado de reintentar.");

        _medidor.CreateObservableGauge(
            "bastion.bandeja_de_salida.antiguedad_del_mas_viejo",
            () => Volatile.Read(ref _antiguedad),
            unit: "s",
            description: "Segundos que lleva esperando el evento pendiente más antiguo.");
    }

    /// <summary>Apunta la edad del pendiente más viejo, en segundos. Cero si no hay ninguno.</summary>
    /// <param name="segundos">Cuánto lleva esperando.</param>
    public void AnotarAntiguedad(double segundos) => Volatile.Write(ref _antiguedad, segundos);

    /// <summary>Apunta un evento publicado.</summary>
    public void AnotarPublicado() => _publicados.Add(1);

    /// <summary>Apunta un evento aparcado.</summary>
    public void AnotarAparcado() => _aparcados.Add(1);

    /// <inheritdoc/>
    public void Dispose() => _medidor.Dispose();
}
