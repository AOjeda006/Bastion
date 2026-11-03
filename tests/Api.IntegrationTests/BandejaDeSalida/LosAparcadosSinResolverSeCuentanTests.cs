using System.Diagnostics.Metrics;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.BandejaDeSalida;

/// <summary>
/// El hueco 4 del 3.4: <b>cuántos aparcados hay sin resolver</b>, leído de la base en cada vuelta y
/// de todas las empresas.
/// </summary>
/// <remarks>
/// <para>
/// Los aparcados de este caso los aparca el propio caso, a pelo y antes de arrancar el publicador:
/// los aparcó «otro proceso». El contador de aparcados de este proceso no los ha visto nunca, y el
/// indicador tiene que decirlos igual. Es la diferencia entre los dos.
/// </para>
/// <para>
/// <b>Se lee el medidor de esta bandeja, y no el mayor de todos</b>, como hace el de la edad: en el
/// proceso está también el host de la API, con su publicador sobre la base compartida, y los
/// aparcados de esa base son los de cualquiera. El espía apunta los medidores que ya estaban antes
/// de arrancar, y lee solo el que nace después.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor compartido; de él sale a qué servidor conectarse.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LosAparcadosSinResolverSeCuentanTests(PostgresConTodosLosModulos postgres)
{
    private const string Instrumento = "bastion.bandeja_de_salida.aparcados_sin_resolver";

    [Fact]
    public async Task El_indicador_dice_los_aparcados_de_la_base_de_todas_las_empresas_y_baja_al_resolver_uno()
    {
        string cadena = await postgres.CrearBaseNuevaAsync(migrada: true);

        await using BandejaDeVerdad bandeja = new(cadena, publica: true);

        // Tres aparcados: uno de la empresa de las filas de prueba, uno de otra y uno sin empresa. Y
        // uno pendiente, que no cuenta.
        HechoDePrueba deEsta = new("aparcado-de-esta");
        HechoDePrueba deOtra = new("aparcado-de-otra");
        HechoDePrueba sinEmpresa = new("aparcado-sin-empresa");
        HechoDePrueba pendiente = new("pendiente-que-no-cuenta");

        await bandeja.EncolarAsync(deEsta);
        await bandeja.EncolarAsync(deOtra);
        await bandeja.EncolarAsync(sinEmpresa, sinInquilino: MotivoSinInquilino.SemillaDeArranque);
        await bandeja.EncolarAsync(pendiente);

        await EjecutarAsync(
            cadena,
            "UPDATE auditoria.bandeja_de_salida SET empresa_id = @otra WHERE evento_id = @evento",
            ("otra", Guid.CreateVersion7()),
            ("evento", deOtra.EventoId));

        foreach (HechoDePrueba aparcado in new[] { deEsta, deOtra, sinEmpresa })
        {
            await EjecutarAsync(
                cadena,
                "UPDATE auditoria.bandeja_de_salida SET estado = 'Aparcado', intentos = @intentos, " +
                "ultimo_error = 'lo aparcó otro proceso' WHERE evento_id = @evento",
                ("intentos", EventoDeLaBandeja.IntentosAntesDeAparcar),
                ("evento", aparcado.EventoId));
        }

        using EspiaDeMedidas espia = new();

        await bandeja.ArrancarAsync();

        (await BandejaDeVerdad.EsperarAsync(() => Task.FromResult(espia.LeerElNuevo() == 3)))
            .ShouldBeTrue($"con tres aparcados en la base, el indicador marca {espia.LeerElNuevo()}");

        (await BandejaDeVerdad.EsperarAsync(async () =>
                await bandeja.EnLaColaAsync(pendiente.EventoId) is { Estado: EstadoDelEnvio.Publicado }))
            .ShouldBeTrue("lo pendiente sale, y no ha contado");

        // Alguien resuelve uno: lo devuelve a la cola, sale, y la vuelta siguiente lo descuenta.
        await EjecutarAsync(
            cadena,
            "UPDATE auditoria.bandeja_de_salida SET estado = 'Pendiente' WHERE evento_id = @evento",
            ("evento", deOtra.EventoId));

        (await BandejaDeVerdad.EsperarAsync(async () =>
                await bandeja.EnLaColaAsync(deOtra.EventoId) is { Estado: EstadoDelEnvio.Publicado }))
            .ShouldBeTrue("el que se ha devuelto a la cola no ha salido");

        (await BandejaDeVerdad.EsperarAsync(() => Task.FromResult(espia.LeerElNuevo() == 2)))
            .ShouldBeTrue($"resuelto uno de los tres, el indicador marca {espia.LeerElNuevo()}");
    }

    private static async Task EjecutarAsync(
        string cadena,
        string sql,
        params (string Nombre, object Valor)[] parametros)
    {
        await using NpgsqlConnection conexion = new(cadena);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(sql, conexion);

        foreach ((string nombre, object valor) in parametros)
        {
            orden.Parameters.AddWithValue(nombre, valor);
        }

        (await orden.ExecuteNonQueryAsync()).ShouldBe(1, $"la orden no ha tocado su fila: {sql}");
    }

    // Lleva el valor del indicador por medidor, y separa los medidores que ya estaban al estrenarse
    // de los que nacen después: el de esta bandeja nace al arrancar su publicador.
    private sealed class EspiaDeMedidas : IDisposable
    {
        private readonly Lock _cerrojo = new();
        private readonly HashSet<Meter> _deAntes = [];
        private readonly Dictionary<Meter, long> _valores = [];
        private readonly MeterListener _oyente = new();
        private readonly bool _estrenado;

        public EspiaDeMedidas()
        {
            _oyente.InstrumentPublished = (instrumento, oyente) =>
            {
                if (!string.Equals(instrumento.Meter.Name, MetricasDeLaBandeja.Medidor, StringComparison.Ordinal)
                    || !string.Equals(instrumento.Name, Instrumento, StringComparison.Ordinal))
                {
                    return;
                }

                lock (_cerrojo)
                {
                    if (!_estrenado)
                    {
                        _deAntes.Add(instrumento.Meter);
                    }
                }

                oyente.EnableMeasurementEvents(instrumento);
            };

            _oyente.SetMeasurementEventCallback<long>((instrumento, medida, _, _) =>
            {
                lock (_cerrojo)
                {
                    _valores[instrumento.Meter] = medida;
                }
            });

            // Publica los que ya existen antes de volver: esos son los de antes.
            _oyente.Start();

            lock (_cerrojo)
            {
                _estrenado = true;
            }
        }

        // El valor del único medidor nacido después, o nada si todavía no ha nacido. Dos sería no
        // saber cuál es el de esta bandeja, y se dice en vez de elegir uno.
        public long? LeerElNuevo()
        {
            _oyente.RecordObservableInstruments();

            lock (_cerrojo)
            {
                List<long> nuevos = [.. _valores.Where(par => !_deAntes.Contains(par.Key)).Select(par => par.Value)];

                return nuevos.Count switch
                {
                    0 => null,
                    1 => nuevos[0],
                    _ => throw new InvalidOperationException(
                        $"Han nacido {nuevos.Count} medidores de la bandeja, y el de este caso es uno solo."),
                };
            }
        }

        public void Dispose() => _oyente.Dispose();
    }
}
