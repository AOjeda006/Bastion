using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shouldly;

namespace Bastion.Api.IntegrationTests.BandejaDeSalida;

/// <summary>
/// Si no se puede guardar la marca de publicado, la fila sigue pendiente, con su error, y la vuelta
/// siguiente la publica.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo que guarda.</b> El publicador marca cada fila en un ámbito propio, releyéndola, y apunta
/// un fallo en otro (ADR-0078). El contexto de la vuelta lee el lote sin rastrearlo y no escribe en
/// la cola. Si la marca se hiciera en ese contexto, un fallo al guardarla dejaría la fila como
/// publicada en su rastreador, y el guardado de la marca de la fila siguiente del lote la
/// escribiría: publicada, y con el error que se apuntó entre medias. Es la mutación 1125, que salió
/// verde en la tanda del cierre del 3.4 porque ningún caso sabía hacer fallar ese guardado.
/// </para>
/// <para>
/// <b>El arnés.</b> Un interceptor del contexto de la cola, solo de test, que lanza una vez, y solo
/// en el guardado que lleva la fila del evento marcada como publicada. Lo demás —el despacho, los
/// manejadores, el fallo que se apunta— es el cableado de producción. El caso afirma antes que nada
/// que el arnés ha inyectado el fallo: si dejara de hacerlo, lo demás saldría verde sin haber
/// probado nada.
/// </para>
/// <para>
/// <b>Dos publicadores, uno detrás de otro, sobre la misma base.</b> El primero da una sola vuelta,
/// con una hora hasta la siguiente, y el caso mira cómo deja la cola. La vuelta siguiente es la
/// primera del segundo, que arranca cuando el primero ya ha parado. Con un solo publicador y vueltas
/// cortas, el estado entre las dos sería una ventana que el caso podría no llegar a ver.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor compartido, con las migraciones aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class UnaMarcaQueNoSeGuardaTests(PostgresConTodosLosModulos postgres)
{
    [Fact]
    public async Task La_fila_sigue_pendiente_con_su_error_y_la_vuelta_siguiente_la_publica()
    {
        HechoDePrueba marcado = new(Marca());
        HechoDePrueba detras = new(Marca());
        FallaUnaVezAlMarcar arnes = new(marcado.EventoId);

        await using (BandejaDeVerdad primera = new(
            postgres.CadenaDeConexion,
            publica: true,
            servicios => servicios.AgregarManejadorDeEvento<ManejadorQueCuenta, ContextoDeLaBandeja>(),
            intervalo: TimeSpan.FromHours(1),
            alGuardarLaCola: arnes))
        {
            await primera.EncolarAsync(marcado);
            await primera.EncolarAsync(detras);
            await primera.ArrancarAsync();

            // El de detrás va en el mismo lote y se marca después: si está publicado, la vuelta ya
            // ha pasado por los dos.
            (await BandejaDeVerdad.EsperarAsync(async () =>
                await primera.EnLaColaAsync(detras.EventoId) is { Estado: EstadoDelEnvio.Publicado }))
                .ShouldBeTrue("la primera vuelta no ha llegado a publicar el evento de detrás");

            arnes.Inyectados.ShouldBe(
                1,
                "el arnés no ha hecho fallar el guardado de la marca, así que este caso no ha probado nada");

            EventoDeLaBandeja fila = (await primera.EnLaColaAsync(marcado.EventoId)).ShouldNotBeNull();

            fila.Estado.ShouldBe(
                EstadoDelEnvio.Pendiente,
                "la marca no se guardó y la fila ha quedado publicada igual: la ha escrito otro guardado");
            fila.PublicadoEn.ShouldBeNull();
            fila.Intentos.ShouldBe(1, "el fallo al marcar se apunta en el acto, como cualquier otro");
            fila.UltimoError.ShouldNotBeNull().ShouldContain(FallaUnaVezAlMarcar.Motivo);
        }

        await using BandejaDeVerdad segunda = new(
            postgres.CadenaDeConexion,
            publica: true,
            servicios => servicios.AgregarManejadorDeEvento<ManejadorQueCuenta, ContextoDeLaBandeja>());

        await segunda.ArrancarAsync();

        (await BandejaDeVerdad.EsperarAsync(async () =>
            await segunda.EnLaColaAsync(marcado.EventoId) is { Estado: EstadoDelEnvio.Publicado }))
            .ShouldBeTrue("la vuelta siguiente no ha publicado la fila cuya marca no se guardó");

        EventoDeLaBandeja resuelta = (await segunda.EnLaColaAsync(marcado.EventoId)).ShouldNotBeNull();

        resuelta.PublicadoEn.ShouldNotBeNull();
        resuelta.Intentos.ShouldBe(1);
        resuelta.UltimoError.ShouldBeNull("al publicarse se limpia el error de la marca que no se guardó");

        // Se ha vuelto a entregar, y el consumidor ya lo había procesado: reintentar no es duplicar.
        Efectos.Veces($"{marcado.Marca}/pruebas.el-que-cuenta").ShouldBe(1);
    }

    private static string Marca() => Guid.CreateVersion7().ToString();

    /// <summary>
    /// Hace fallar, una sola vez, el guardado que lleva la fila de un evento marcada como publicada.
    /// </summary>
    /// <param name="eventoId">El evento cuya marca falla.</param>
    private sealed class FallaUnaVezAlMarcar(Guid eventoId) : SaveChangesInterceptor
    {
        /// <summary>Lo que dice el fallo, para encontrarlo en el error de la fila.</summary>
        public const string Motivo = "Fallo inyectado al guardar la marca de publicado.";

        private int _inyectados;

        /// <summary>Cuántas veces ha lanzado: una, o el caso no ha probado nada.</summary>
        public int Inyectados => Volatile.Read(ref _inyectados);

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            bool esLaMarca = eventData.Context is { } contexto
                && contexto.ChangeTracker.Entries<EventoDeLaBandeja>().Any(fila =>
                    fila.State == EntityState.Modified
                    && fila.Entity.EventoId == eventoId
                    && fila.Entity.Estado == EstadoDelEnvio.Publicado);

            if (esLaMarca && Interlocked.CompareExchange(ref _inyectados, 1, 0) == 0)
            {
                throw new DbUpdateException(Motivo);
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
