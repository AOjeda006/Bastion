using System.Diagnostics.Metrics;

namespace Bastion.Inventario.Infrastructure.Corte;

/// <summary>
/// Lo que el trabajo mensual cuenta de sí mismo, en su propio medidor: tres contadores
/// (ADR-0063 §7), lo que tarda cada mes del avance (ADR-0067 §4) y cuántos meses lleva de retraso el
/// corte más viejo (ADR-0064 §3).
/// </summary>
/// <remarks>
/// <para>
/// <b>Tres contadores y no uno con etiqueta</b>, por lo mismo que en la bandeja: sobre los avances
/// fallidos y sobre los descuadres se pone una alerta que salta con el primero, y sobre las empresas
/// avanzadas no se pone ninguna. Mezclarlos obligaría a filtrar en cada consulta, y la que se olvide
/// avisaría por lo que va bien.
/// </para>
/// <para>
/// <b>Sin la empresa como etiqueta.</b> Cada empresa sería una serie más en el recolector, y su
/// número crece con los clientes. Qué empresa ha fallado o no cuadra lo dice el registro, que es
/// donde se mira después de que salte la alerta.
/// </para>
/// <para>
/// <b>La duración, en segundos y con los cubos alrededor de lo que decide</b>: el disparador, 10 s,
/// y el plazo de cada orden, 30 s, que es el techo (ADR-0064 §2). Los cubos por defecto del SDK
/// están pensados para milisegundos, y en segundos pondrían todo avance de menos de 5 s en el
/// mismo. Solo se anota el mes que termina: el que no termina cuenta en los fallidos. Desde el 2.18
/// es un mes y no la puesta al día entera, porque un mes es lo que dura el exclusivo.
/// </para>
/// <para>
/// <b>El retraso es un indicador y no un contador</b>, porque baja: vale cero mientras todas las
/// empresas llegan al mes que toca, y sube un mes por cada mes que el más viejo se queda atrás. Una
/// empresa que se quedara atrás en silencio —el avance falla por plazo cada hora— es justo lo que
/// tiene que verse.
/// </para>
/// </remarks>
internal sealed class MetricasDelCorte : IDisposable
{
    /// <summary>Nombre del medidor. Es lo que hay que registrar en OpenTelemetry.</summary>
    internal const string Medidor = "Bastion.Inventario.Corte";

    /// <summary>
    /// Los límites de los cubos de la duración, en segundos: el disparador es 10 y el techo, 30
    /// (ADR-0064 §2).
    /// </summary>
    internal static IReadOnlyList<double> CubosDeLaDuracion { get; } = [1, 2.5, 5, 7.5, 10, 15, 20, 25, 30, 60];

    private readonly Meter _medidor = new(Medidor);
    private readonly Counter<long> _avanzadas;
    private readonly Counter<long> _fallidos;
    private readonly Counter<long> _descuadres;
    private readonly Histogram<double> _duracionDelAvance;
    private readonly Gauge<int> _mesesDeRetraso;

    /// <summary>Crea los instrumentos.</summary>
    public MetricasDelCorte()
    {
        _avanzadas = _medidor.CreateCounter<long>(
            "bastion.inventario.corte.empresas_avanzadas",
            unit: "{empresa}",
            description: "Empresas cuyo corte de las instantáneas ha avanzado al mes que toca.");

        _fallidos = _medidor.CreateCounter<long>(
            "bastion.inventario.corte.avances_fallidos",
            unit: "{empresa}",
            description: "Avances del corte de una empresa que han fallado. Se reintentan en la hora siguiente.");

        _descuadres = _medidor.CreateCounter<long>(
            "bastion.inventario.corte.descuadres",
            unit: "{descuadre}",
            description: "Descuadres que ha encontrado el cuadre de después de avanzar.");

        _duracionDelAvance = _medidor.CreateHistogram(
            "bastion.inventario.corte.duracion_del_avance",
            unit: "s",
            description: "Lo que tarda un mes del avance del corte de una empresa, de abrir su transacción al COMMIT.",
            tags: null,
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = CubosDeLaDuracion });

        _mesesDeRetraso = _medidor.CreateGauge<int>(
            "bastion.inventario.corte.meses_de_retraso",
            unit: "{mes}",
            description: "Meses que le faltan al corte más viejo para llegar al mes que toca, tras cada vuelta.");
    }

    /// <summary>Apunta una empresa avanzada.</summary>
    public void AnotarAvanzada() => _avanzadas.Add(1);

    /// <summary>Apunta un avance fallido.</summary>
    public void AnotarFallido() => _fallidos.Add(1);

    /// <summary>Apunta los descuadres de un cuadre.</summary>
    /// <param name="cuantos">Cuántos ha encontrado.</param>
    public void AnotarDescuadres(int cuantos) => _descuadres.Add(cuantos);

    /// <summary>Apunta lo que ha tardado un mes del avance de una empresa.</summary>
    /// <param name="duracion">De abrir su transacción al <c>COMMIT</c>.</param>
    public void AnotarDuracionDelAvance(TimeSpan duracion) => _duracionDelAvance.Record(duracion.TotalSeconds);

    /// <summary>Apunta el retraso del corte más viejo al acabar una vuelta.</summary>
    /// <param name="meses">Los meses que le faltan; cero si todos llegan.</param>
    public void AnotarMesesDeRetraso(int meses) => _mesesDeRetraso.Record(meses);

    /// <inheritdoc/>
    public void Dispose() => _medidor.Dispose();
}
