using Bastion.BuildingBlocks.Application.Eventos;
using Bastion.BuildingBlocks.Domain.Eventos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;

/// <summary>
/// Entrega cada evento a los manejadores que lo escuchan, saltándose los que ya lo procesaron.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es donde vive «reprocesar no duplica».</b> El publicador entrega al menos una vez a
/// propósito; lo que hace que el <b>efecto</b> ocurra una sola vez es esta clase, que antes de
/// llamar a un manejador mira si el par (evento, consumidor) ya está apuntado, y después de que el
/// manejador termine lo apunta. La segunda vuelta no vuelve a ejecutar nada.
/// </para>
/// <para>
/// <b>El hueco que queda, escrito.</b> La huella se graba en su propia transacción, no en la del
/// efecto del manejador: si el proceso se cae entre «el manejador terminó» y «la huella está
/// grabada», ese manejador se ejecutará otra vez. Cerrarlo del todo exige que el manejador escriba
/// su efecto y su huella en el mismo <c>SaveChanges</c>, y para eso la tabla de huellas está
/// mapeada también en los contextos de módulo — la puerta queda abierta y sin usar, porque la
/// fase 0 no tiene ningún manejador con efecto de negocio. El día que lo haya, esto se decide con
/// un caso delante y no en abstracto.
/// </para>
/// <para>
/// <b>Cero manejadores no es un error.</b> Un hecho que hoy no le interesa a nadie se publica
/// igual: es el emisor quien decide contar lo que le ha pasado, no el receptor quien decide qué se
/// cuenta. Al revés, añadir un consumidor obligaría a tocar el módulo que emite.
/// </para>
/// </remarks>
/// <param name="manejadores">Todos los manejadores registrados.</param>
/// <param name="contexto">Contexto donde se apunta la huella de lo ya procesado.</param>
/// <param name="reloj">De dónde sale el instante.</param>
/// <param name="registro">Dónde se anota lo que hace el despachador.</param>
internal sealed partial class DespachadorDeEventos(
    IEnumerable<IManejadorDeEvento> manejadores,
    ContextoDeLaBandeja contexto,
    TimeProvider reloj,
    ILogger<DespachadorDeEventos> registro) : IDespachadorDeEventos
{
    public async Task<int> DespacharAsync(EventoDeIntegracion evento, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(evento);

        int atendidos = 0;

        foreach (IManejadorDeEvento manejador in manejadores
            .Where(candidato => candidato.EventoQueAtiende.IsInstanceOfType(evento)))
        {
            if (await YaPasoAsync(evento.EventoId, manejador.Consumidor, cancelacion).ConfigureAwait(false))
            {
                Repetido(registro, manejador.Consumidor, evento.EventoId);

                continue;
            }

            await manejador.ManejarAsync(evento, cancelacion).ConfigureAwait(false);

            contexto.Procesados.Add(
                EventoProcesado.De(evento.EventoId, manejador.Consumidor, reloj.GetUtcNow()));

            await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

            atendidos++;
        }

        return atendidos;
    }

    private Task<bool> YaPasoAsync(Guid eventoId, string consumidor, CancellationToken cancelacion) =>
        contexto.Procesados.AnyAsync(
            huella => huella.EventoId == eventoId && huella.Consumidor == consumidor,
            cancelacion);

    [LoggerMessage(
        EventId = 8200,
        Level = LogLevel.Debug,
        Message = "El consumidor {Consumidor} ya había procesado el evento {EventoId}; no se repite.")]
    private static partial void Repetido(ILogger registro, string consumidor, Guid eventoId);
}
