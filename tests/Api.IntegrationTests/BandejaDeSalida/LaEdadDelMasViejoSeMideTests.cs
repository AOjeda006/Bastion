using System.Diagnostics.Metrics;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Shouldly;

namespace Bastion.Api.IntegrationTests.BandejaDeSalida;

/// <summary>
/// Que la métrica de la bandeja mida <b>lo que dice medir</b>: los segundos que lleva esperando el
/// evento pendiente más antiguo.
/// </summary>
/// <remarks>
/// <para>
/// Que el instrumento exista y se llame así lo comprueba el paso rápido. Lo que se comprueba aquí
/// es lo otro: que quien lo rellena —el publicador, en cada vuelta— ponga de verdad la edad del
/// más viejo. Una métrica con el nombre correcto y el valor equivocado es peor que no tenerla:
/// encima de ella se pone una alerta que no salta.
/// </para>
/// <para>
/// <b>Base propia.</b> La compartida tiene a los demás tests dejando eventos, y el más viejo de la
/// cola sería el de cualquiera. Aquí la cola tiene exactamente una fila, y su antigüedad la pone
/// el test.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor compartido; de él sale a qué servidor conectarse.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaEdadDelMasViejoSeMideTests(PostgresConTodosLosModulos postgres)
{
    private const string Instrumento = "bastion.bandeja_de_salida.antiguedad_del_mas_viejo";

    private static readonly TimeSpan s_esperando = TimeSpan.FromMinutes(10);

    [Fact]
    public async Task El_publicador_publica_la_edad_del_pendiente_mas_viejo()
    {
        string cadena = await postgres.CrearBaseNuevaAsync(migrada: true);

        // Vueltas lentas a propósito: la fila se aparca al quinto intento, y con vueltas de cien
        // milisegundos la ventana en la que hay algo pendiente que medir dura medio segundo.
        await using BandejaDeVerdad bandeja = new(
            cadena, publica: true, intervalo: TimeSpan.FromMilliseconds(400));

        using EspiaDeMedidas espia = new();

        // Un evento que ocurrió hace diez minutos y que no puede salir de la cola: el nombre no lo
        // declara nadie, así que se reintenta —y sigue pendiente— unas cuantas vueltas.
        await bandeja.EncolarAsync(
            new HechoDePrueba("la-edad"),
            "pruebas.hecho-que-no-declara-nadie",
            DateTimeOffset.UtcNow - s_esperando);

        await bandeja.ArrancarAsync();

        (await BandejaDeVerdad.EsperarAsync(
            () => Task.FromResult(espia.Leer(Instrumento) >= s_esperando.TotalSeconds - 30)))
            .ShouldBeTrue(
                "la métrica no dice la edad del más viejo: con un evento esperando desde hace diez " +
                $"minutos, marca {espia.Leer(Instrumento)} segundos");
    }

    [Fact]
    public async Task Y_con_la_cola_vacia_la_edad_vuelve_a_cero()
    {
        // El control del anterior: un instrumento que se quedara con el último valor grande diría
        // que hay un atasco cuando ya no lo hay, y la alerta no bajaría nunca.
        string cadena = await postgres.CrearBaseNuevaAsync(migrada: true);

        await using BandejaDeVerdad bandeja = new(cadena, publica: true);

        HechoDePrueba hecho = new("la-cola-vacia");

        await bandeja.EncolarAsync(hecho, ocurridoEn: DateTimeOffset.UtcNow - s_esperando);
        await bandeja.ArrancarAsync();

        (await BandejaDeVerdad.EsperarAsync(async () =>
            await bandeja.EnLaColaAsync(hecho.EventoId) is { Estado: EstadoDelEnvio.Publicado }))
            .ShouldBeTrue("el evento no ha llegado a publicarse");

        // El espía se estrena DESPUÉS de vaciar la cola, y se queda con el mayor valor que vea a
        // partir de ahí: si el publicador dejara la edad congelada en los diez minutos, cualquier
        // lectura posterior lo delataría. Se reintenta porque entre publicar y volver a medir hay
        // una vuelta de por medio.
        (await BandejaDeVerdad.EsperarAsync(() =>
        {
            using EspiaDeMedidas espia = new();

            return Task.FromResult(espia.Leer(Instrumento) == 0);
        })).ShouldBeTrue("con la cola vacía, la edad del más viejo es cero");
    }

    // El mismo espía que en el paso rápido, con la misma cautela: se queda con el MAYOR valor
    // observado, porque en este proceso hay más de un medidor con este nombre —cada host de test
    // tiene el suyo— y el orden en que se recolectan no está definido.
    private sealed class EspiaDeMedidas : IDisposable
    {
        private readonly Dictionary<string, double> _valores = new(StringComparer.Ordinal);
        private readonly MeterListener _oyente = new();

        public EspiaDeMedidas()
        {
            _oyente.InstrumentPublished = (instrumento, oyente) =>
            {
                if (string.Equals(instrumento.Meter.Name, MetricasDeLaBandeja.Medidor, StringComparison.Ordinal))
                {
                    oyente.EnableMeasurementEvents(instrumento);
                }
            };

            _oyente.SetMeasurementEventCallback<double>(
                (instrumento, medida, _, _) => _valores[instrumento.Name] =
                    Math.Max(_valores.GetValueOrDefault(instrumento.Name), medida));

            _oyente.Start();
        }

        public double Leer(string instrumento)
        {
            _oyente.RecordObservableInstruments();

            return _valores.GetValueOrDefault(instrumento);
        }

        public void Dispose() => _oyente.Dispose();
    }
}
