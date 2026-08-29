using System.Diagnostics.Metrics;
using Bastion.Api.FunctionalTests.Salud;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Bastion.Api.FunctionalTests.BandejaDeSalida;

/// <summary>
/// Cómo se vigila la bandeja: con una métrica, y <b>no</b> con una sonda.
/// </summary>
/// <remarks>
/// <para>
/// La lección del 0.2, otra vez y por escrito: una cola atrasada no significa que el proceso esté
/// colgado —meterla en la sonda de vida haría que el orquestador reiniciara la API en bucle, y
/// reiniciar la API no vacía la cola— ni significa que la API no pueda atender tráfico —meterla en
/// la de disponibilidad convertiría un retraso de fondo en una caída de servicio—. Se vigila con
/// una alerta sobre la métrica, que es la herramienta que existe para eso.
/// </para>
/// <para>
/// Y la métrica es <b>la edad del pendiente más viejo</b>, no cuántos hay: el tamaño de la cola
/// sube y baja con el tráfico y no distingue mil eventos que se van a publicar en dos segundos de
/// uno atascado desde ayer.
/// </para>
/// </remarks>
public sealed class LaBandejaSeMideYNoSeSondeaTests : IDisposable
{
    // La lista ENTERA de comprobaciones registradas, como todas las listas de este proyecto: si
    // alguien añade una de la bandeja, esto se pone rojo y hay que venir a decidirlo aquí.
    private static readonly string[] s_sondasEsperadas = ["base-de-datos"];

    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void La_bandeja_no_esta_en_ninguna_sonda()
    {
        HealthCheckServiceOptions opciones = _api.Services
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value;

        opciones.Registrations
            .Select(comprobacion => comprobacion.Name)
            .OrderBy(nombre => nombre, StringComparer.Ordinal)
            .ShouldBe(s_sondasEsperadas);
    }

    [Fact]
    public void El_medidor_publica_la_edad_del_mas_viejo_en_segundos()
    {
        using MetricasDeLaBandeja metricas = new();
        using EspiaDeMedidas espia = new();

        metricas.AnotarAntiguedad(42.5);

        espia.Leer("bastion.bandeja_de_salida.antiguedad_del_mas_viejo").ShouldBe(42.5);
        espia.Unidad("bastion.bandeja_de_salida.antiguedad_del_mas_viejo").ShouldBe("s");
    }

    [Fact]
    public void Y_cuenta_lo_publicado_y_lo_aparcado_por_separado()
    {
        // Dos contadores y no uno con etiqueta: sobre el de aparcados se pone una alerta que salta
        // con el primero, y sobre el de publicados no se pone ninguna. Mezclarlos obligaría a
        // filtrar en cada consulta, y la que se olvide de filtrar avisará por lo que va bien.
        using MetricasDeLaBandeja metricas = new();
        using EspiaDeMedidas espia = new();

        metricas.AnotarPublicado();
        metricas.AnotarPublicado();
        metricas.AnotarAparcado();

        espia.Leer("bastion.bandeja_de_salida.publicados").ShouldBe(2);
        espia.Leer("bastion.bandeja_de_salida.aparcados").ShouldBe(1);
    }

    // Un oyente de los instrumentos del medidor de la bandeja. Lee lo que se publica de verdad
    // —nombre, unidad y valor—, que es lo único que llega a un recolector.
    private sealed class EspiaDeMedidas : IDisposable
    {
        private readonly Dictionary<string, double> _valores = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string?> _unidades = new(StringComparer.Ordinal);
        private readonly MeterListener _oyente = new();

        public EspiaDeMedidas()
        {
            _oyente.InstrumentPublished = (instrumento, oyente) =>
            {
                if (string.Equals(instrumento.Meter.Name, MetricasDeLaBandeja.Medidor, StringComparison.Ordinal))
                {
                    _unidades[instrumento.Name] = instrumento.Unit;
                    oyente.EnableMeasurementEvents(instrumento);
                }
            };

            // Se queda con el MAYOR, no con el último. Otros tests de este ensamblado tienen su
            // propio host levantado, con su propio medidor y el mismo nombre de instrumento; sus
            // medidores no publican nada —sin base de datos no hay publicador— así que informan
            // cero, y quedarse con el último dejaría este test a merced del orden de recolección.
            _oyente.SetMeasurementEventCallback<double>(
                (instrumento, medida, _, _) => _valores[instrumento.Name] =
                    Math.Max(_valores.GetValueOrDefault(instrumento.Name), medida));

            _oyente.SetMeasurementEventCallback<long>(
                (instrumento, medida, _, _) => _valores[instrumento.Name] =
                    _valores.GetValueOrDefault(instrumento.Name) + medida);

            _oyente.Start();
        }

        public double Leer(string instrumento)
        {
            _oyente.RecordObservableInstruments();

            return _valores.GetValueOrDefault(instrumento);
        }

        public string? Unidad(string instrumento) => _unidades.GetValueOrDefault(instrumento);

        public void Dispose() => _oyente.Dispose();
    }
}
