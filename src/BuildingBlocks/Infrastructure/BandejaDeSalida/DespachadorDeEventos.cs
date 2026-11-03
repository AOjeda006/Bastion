using System.Diagnostics;
using System.Text.Json;
using Bastion.BuildingBlocks.Application.Eventos;
using Bastion.BuildingBlocks.Domain.Eventos;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Multiempresa;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;

/// <summary>
/// Entrega cada evento a los manejadores que lo escuchan, saltándose los que ya lo procesaron.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es donde vive «reprocesar no duplica».</b> El publicador entrega al menos una vez a
/// propósito; lo que hace que el <b>efecto</b> ocurra una sola vez es esta clase, que antes de
/// llamar a un manejador mira si el par (evento, consumidor) ya está apuntado, y con lo que el
/// manejador confirme lo apunta. La segunda vuelta no vuelve a ejecutar nada.
/// </para>
/// <para>
/// <b>Cada manejador, en su ámbito y en su transacción</b> (3.4, huecos 1 y 5). El ámbito es nuevo
/// por manejador, así que lo que uno deja a medias no lo guarda el siguiente; y la transacción es
/// la del contexto de su módulo, el que declaró al registrarse, que es el mismo que reciben sus
/// repositorios en ese ámbito. Dentro van la mirada a la huella, el efecto, la huella y el evento
/// de respuesta, que el interceptor del contexto escribe en el mismo <c>SaveChanges</c> que el
/// efecto. El hueco que quedaba hasta el 3.4 —la huella en su propia transacción, de modo que una
/// caída entre el efecto y la huella repetía el efecto— queda cerrado: si se cae antes de
/// confirmar, no queda ni lo uno ni lo otro.
/// </para>
/// <para>
/// <b>Y con la empresa de su fila</b> (3.4, hueco 2). Dentro del ámbito del manejador, la empresa
/// activa es la de la fila, y no el ámbito sin inquilino de la vuelta del publicador: lo que lee
/// por EF lo filtra esa empresa, la traza y la respuesta la llevan, y escribir en otra lo para la
/// guarda de la auditoría, como en una petición. Ese ámbito solo lo abre esta clase. Una fila sin
/// empresa —escrita bajo un ámbito sin inquilino, como la de la semilla— no se entrega: lanza con
/// su motivo, se reintenta, y a la quinta se aparca.
/// </para>
/// <para>
/// <b>Y con quién lo causó</b> (3.4, hueco 3). En el mismo ámbito, la causa es la de la fila: el
/// usuario y la correlación de la operación que escribió el evento, y una actividad hija de su
/// traza. El efecto se audita a nombre de ese usuario y con esa correlación, y la respuesta lleva
/// las tres, así que una cadena de eventos se sigue entera desde la petición que la empezó. A una
/// fila de antes del 3.4, sin correlación, se le da una nueva, la misma para todos sus manejadores.
/// </para>
/// <para>
/// <b>Un choque de versión se reintenta en el acto</b> (3.4, hueco 5). Si el manejador choca con
/// otra escritura —un <see cref="DbUpdateConcurrencyException"/>—, su transacción ya se ha
/// deshecho, y se le vuelve a entregar con un ámbito y un contexto nuevos, hasta
/// <see cref="EventoDeLaBandeja.ReintentosPorChoque"/> veces y con una espera que se dobla. Si sigue
/// chocando, el choque sale de aquí, y el publicador deja la fila pendiente sin contar intento.
/// Cualquier otro fallo sale a la primera, y se cuenta.
/// </para>
/// <para>
/// <b>Lo que el manejador no confirma no lo guarda nadie</b>, como en una petición: la huella se
/// añade antes de llamarlo, para que vaya en el mismo <c>SaveChanges</c> que su efecto, y si el
/// manejador termina sin haber guardado nada, el contexto se vacía antes de guardar la huella sola.
/// Un caso de uso que dice que no y deja cambios en memoria se comporta igual que detrás de HTTP.
/// </para>
/// <para>
/// <b>Sin puntos de guardado</b>, como el almacén de idempotencia: con ellos, cada
/// <c>SaveChanges</c> dentro de la transacción abriría una subtransacción, y lo que se escribe en
/// ella lleva otro <c>xmin</c>. Una sola transacción de verdad es lo que se puede comprobar.
/// </para>
/// <para>
/// <b>Cero manejadores no es un error</b>, tampoco con una fila sin empresa. Un hecho que hoy no le
/// interesa a nadie se publica igual: es el emisor quien decide contar lo que le ha pasado, no el
/// receptor quien decide qué se cuenta. Al revés, añadir un consumidor obligaría a tocar el módulo
/// que emite.
/// </para>
/// </remarks>
/// <param name="inscripciones">Los manejadores apuntados, con su evento y su contexto.</param>
/// <param name="catalogo">Qué tipo tiene cada nombre de la cola.</param>
/// <param name="ambitos">Fábrica de ámbitos: uno por manejador.</param>
/// <param name="reloj">De dónde sale el instante.</param>
/// <param name="registro">Dónde se anota lo que hace el despachador.</param>
internal sealed partial class DespachadorDeEventos(
    IEnumerable<InscripcionDeManejador> inscripciones,
    CatalogoDeEventos catalogo,
    IServiceScopeFactory ambitos,
    TimeProvider reloj,
    ILogger<DespachadorDeEventos> registro) : IDespachadorDeEventos
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    public async Task<int> DespacharAsync(EventoDeLaBandeja fila, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(fila);

        EventoDeIntegracion evento = Reconstruir(fila);

        List<InscripcionDeManejador> suyas =
            [.. inscripciones.Where(candidata => candidata.Evento.IsInstanceOfType(evento))];

        if (suyas.Count == 0)
        {
            return 0;
        }

        Guid empresaId = fila.EmpresaId ?? throw new InvalidOperationException(
            $"El evento «{fila.Nombre}» se escribió sin empresa, con el motivo {fila.SinInquilino}, y " +
            "un manejador no corre sin empresa: vería las de todas. Se decide con el primer manejador " +
            "que necesite atender un evento así.");

        Causa deLaFila = new(fila.UsuarioId, fila.CorrelacionId ?? Guid.CreateVersion7(), fila.ContextoDeTraza);

        int atendidos = 0;

        foreach (InscripcionDeManejador inscripcion in suyas)
        {
            if (await EntregarHastaQueNoChoqueAsync(inscripcion, fila, evento, empresaId, deLaFila, cancelacion)
                    .ConfigureAwait(false))
            {
                atendidos++;
            }
        }

        return atendidos;
    }

    // Cada reintento vuelve a `EntregarAsync`, que abre su ámbito: el contexto que chocó no se
    // reutiliza, porque guarda lo que leyó antes del choque y volvería a chocar con lo mismo.
    private async Task<bool> EntregarHastaQueNoChoqueAsync(
        InscripcionDeManejador inscripcion,
        EventoDeLaBandeja fila,
        EventoDeIntegracion evento,
        Guid empresaId,
        Causa deLaFila,
        CancellationToken cancelacion)
    {
        for (int reintento = 1; ; reintento++)
        {
            try
            {
                return await EntregarAsync(inscripcion, fila, evento, empresaId, deLaFila, cancelacion)
                    .ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException choque) when (reintento <= EventoDeLaBandeja.ReintentosPorChoque)
            {
                TimeSpan espera = EsperaAntesDel(reintento);

                Choca(registro, inscripcion.Manejador.Name, fila.EventoId, reintento, espera.TotalMilliseconds, choque);

                await Task.Delay(espera, reloj, cancelacion).ConfigureAwait(false);
            }
        }
    }

    // Exponencial, y con hasta la mitad quitada al azar. No es un secreto ni una clave: el azar solo
    // separa en el tiempo a los que chocan, así que el de `Random` basta.
    private static TimeSpan EsperaAntesDel(int reintento) =>
        EventoDeLaBandeja.EsperaTrasUnChoque * (1 << (reintento - 1)) * (1 - (Random.Shared.NextDouble() / 2));

    // Devuelve si el manejador lo ha atendido ahora: `false` es que ya lo había procesado.
    private async Task<bool> EntregarAsync(
        InscripcionDeManejador inscripcion,
        EventoDeLaBandeja fila,
        EventoDeIntegracion evento,
        Guid empresaId,
        Causa deLaFila,
        CancellationToken cancelacion)
    {
        await using AsyncServiceScope ambito = ambitos.CreateAsyncScope();

        using IDisposable conSuEmpresa = InquilinoActual.ConLaEmpresaDelEvento(empresaId);

        var manejador = (IManejadorDeEvento)ambito.ServiceProvider.GetRequiredService(inscripcion.Manejador);
        var contexto = (DbContext)ambito.ServiceProvider.GetRequiredService(inscripcion.Contexto);

        using Activity? actividad = TrazasDeLaBandeja.Atender(fila, manejador.Consumidor);

        // La traza que hereda lo que escriba el manejador es la de su actividad, que es hija de la
        // de la fila. Sin nadie escuchando no hay actividad, y se queda la de la fila: lo que se
        // guarda no depende de que haya un oyente.
        using IDisposable conSuCausa = CausaDelAmbito.ConLaDelEvento(
            deLaFila with { ContextoDeTraza = actividad?.Id ?? deLaFila.ContextoDeTraza });

        try
        {
            return await AtenderAsync(manejador, contexto, evento, cancelacion).ConfigureAwait(false);
        }
        catch (Exception excepcion)
        {
            actividad?.SetStatus(ActivityStatusCode.Error, excepcion.Message);

            throw;
        }
    }

    private async Task<bool> AtenderAsync(
        IManejadorDeEvento manejador,
        DbContext contexto,
        EventoDeIntegracion evento,
        CancellationToken cancelacion)
    {
        DbSet<EventoProcesado> huellas = HuellasDe(contexto);

        contexto.Database.AutoSavepointsEnabled = false;

        await using IDbContextTransaction transaccion =
            await contexto.Database.BeginTransactionAsync(cancelacion).ConfigureAwait(false);

        if (await huellas.AnyAsync(
                huella => huella.EventoId == evento.EventoId && huella.Consumidor == manejador.Consumidor,
                cancelacion).ConfigureAwait(false))
        {
            Repetido(registro, manejador.Consumidor, evento.EventoId);

            return false;
        }

        var huella = EventoProcesado.De(evento.EventoId, manejador.Consumidor, reloj.GetUtcNow());

        huellas.Add(huella);

        await manejador.ManejarAsync(evento, cancelacion).ConfigureAwait(false);

        // La huella sigue sin guardar: el manejador no ha confirmado nada. Lo que haya dejado en el
        // contexto se descarta, como se descarta al acabar una petición que no confirma, y la
        // huella se guarda sola. Si confirmó, la huella ya entró con su efecto.
        if (contexto.Entry(huella).State != EntityState.Unchanged)
        {
            contexto.ChangeTracker.Clear();
            huellas.Add(huella);

            await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
        }

        // Una excepción antes de aquí la revierte el `await using`: ni efecto, ni huella, ni
        // respuesta, y el evento se reintenta entero.
        await transaccion.CommitAsync(cancelacion).ConfigureAwait(false);

        return true;
    }

    private EventoDeIntegracion Reconstruir(EventoDeLaBandeja fila)
    {
        Type tipo = catalogo.TipoDe(fila.Nombre) ?? throw new InvalidOperationException(
            $"En la cola hay un evento «{fila.Nombre}» que ya no declara nadie. Un nombre que " +
            "deja de estar declarado es un contrato retirado con filas todavía dentro: o se " +
            "vuelve a declarar, o esas filas se resuelven a mano.");

        return (EventoDeIntegracion)JsonSerializer.Deserialize(fila.Cuerpo, tipo, s_json)!;
    }

    // Todos los contextos de módulo mapean la huella (ADR-0013 §3). Uno que no la mapeara no se
    // podría usar para esto, y se dice con su nombre en vez de con el error genérico de EF Core.
    private static DbSet<EventoProcesado> HuellasDe(DbContext contexto) =>
        contexto.Model.FindEntityType(typeof(EventoProcesado)) is null
            ? throw new InvalidOperationException(
                $"El contexto {contexto.GetType().Name} no mapea la huella de lo ya procesado, así que " +
                "un manejador no puede escribir en él su efecto y su huella en la misma transacción. " +
                "Se mapea con ConfiguracionDeLaBandeja.Mapear.")
            : contexto.Set<EventoProcesado>();

    [LoggerMessage(
        EventId = 8200,
        Level = LogLevel.Debug,
        Message = "El consumidor {Consumidor} ya había procesado el evento {EventoId}; no se repite.")]
    private static partial void Repetido(ILogger registro, string consumidor, Guid eventoId);

    [LoggerMessage(
        EventId = 8201,
        Level = LogLevel.Warning,
        Message = "El manejador {Manejador} ha chocado con otra escritura al atender el evento {EventoId}; " +
                  "se reintenta en el acto (reintento {Reintento}), dentro de {Milisegundos} ms.")]
    private static partial void Choca(
        ILogger registro, string manejador, Guid eventoId, int reintento, double milisegundos, Exception choque);
}
