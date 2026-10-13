using System.Diagnostics.Metrics;

namespace Bastion.Inventario.Infrastructure.Corte;

/// <summary>
/// Lo que el trabajo mensual cuenta de sí mismo: tres contadores en su propio medidor
/// (ADR-0063 §7).
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
/// </remarks>
internal sealed class MetricasDelCorte : IDisposable
{
    /// <summary>Nombre del medidor. Es lo que hay que registrar en OpenTelemetry.</summary>
    internal const string Medidor = "Bastion.Inventario.Corte";

    private readonly Meter _medidor = new(Medidor);
    private readonly Counter<long> _avanzadas;
    private readonly Counter<long> _fallidos;
    private readonly Counter<long> _descuadres;

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
    }

    /// <summary>Apunta una empresa avanzada.</summary>
    public void AnotarAvanzada() => _avanzadas.Add(1);

    /// <summary>Apunta un avance fallido.</summary>
    public void AnotarFallido() => _fallidos.Add(1);

    /// <summary>Apunta los descuadres de un cuadre.</summary>
    /// <param name="cuantos">Cuántos ha encontrado.</param>
    public void AnotarDescuadres(int cuantos) => _descuadres.Add(cuantos);

    /// <inheritdoc/>
    public void Dispose() => _medidor.Dispose();
}
