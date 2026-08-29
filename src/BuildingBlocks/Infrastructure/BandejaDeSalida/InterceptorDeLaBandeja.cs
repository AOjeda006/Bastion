using System.Text.Json;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Eventos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;

/// <summary>
/// Vuelca a la bandeja de salida los eventos que traen los agregados que se están guardando,
/// <b>dentro del mismo <c>SaveChanges</c></b> (R12).
/// </summary>
/// <remarks>
/// <para>
/// <b>Aquí es donde se cumple la primera cláusula del criterio</b>, y se cumple por construcción,
/// no por disciplina. Las filas se añaden al contexto durante <c>SavingChanges</c>, así que salen
/// en el mismo lote de órdenes que el cambio de negocio y, por tanto, en la misma transacción: o
/// entran las dos cosas o no entra ninguna. La ruta que este diseño descarta —publicar después de
/// que el guardado haya ido bien— pasa los dos tests obvios, así que no se descarta con un test
/// obvio: se descarta comparando el <c>xmin</c> de las dos filas, que es el número de transacción
/// que la propia base guarda y que nadie puede fingir.
/// </para>
/// <para>
/// <b>La lista se limpia al terminar bien, no al volcarla.</b> Si se limpiara en
/// <c>SavingChanges</c> y el guardado reventara, el agregado seguiría vivo en el rastreador con la
/// lista ya vacía: el evento se habría perdido sin que nada fallase. Limpiar en
/// <c>SavedChanges</c> deja el peor caso en «se vuelve a intentar y el índice único de
/// <c>evento_id</c> impide que entre dos veces».
/// </para>
/// </remarks>
/// <param name="inquilino">Desde qué empresa se emite, o por qué no hay ninguna.</param>
/// <param name="catalogo">Cómo se llama cada evento en la cola.</param>
/// <param name="reloj">De dónde sale el instante.</param>
public sealed class InterceptorDeLaBandeja(
    IInquilinoActual inquilino,
    CatalogoDeEventos catalogo,
    TimeProvider reloj) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    // CA1725 exige conservar los nombres de los parámetros de la base: `eventData` y `result`.
    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Volcar(eventData);

        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Volcar(eventData);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc/>
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Olvidar(eventData);

        return base.SavedChanges(eventData, result);
    }

    /// <inheritdoc/>
    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        Olvidar(eventData);

        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void Volcar(DbContextEventData datos)
    {
        DbContext? contexto = datos.Context;

        if (contexto is null)
        {
            return;
        }

        List<RaizAgregado> raices =
            [.. ConEventos(contexto).Where(SeEstaGuardando).Select(entrada => entrada.Entity)];

        if (raices.Count == 0)
        {
            return;
        }

        // La misma pareja que en la traza de auditoría, y con la misma regla: o hay empresa, o hay
        // un motivo declarado por el que no la hay. Un evento con empresa nula y sin motivo sería
        // un hecho del que no se sabe de quién es, y la restricción de la tabla lo rechaza.
        Guid? empresaId = inquilino.HayEmpresaActiva ? inquilino.EmpresaDelFiltro : null;
        MotivoSinInquilino? motivo = empresaId.HasValue ? null : DeclararSinInquilino();
        DateTimeOffset ahora = reloj.GetUtcNow();

        foreach (RaizAgregado raiz in raices)
        {
            foreach (EventoDeIntegracion evento in raiz.EventosPendientes)
            {
                contexto.Add(EventoDeLaBandeja.De(
                    evento.EventoId,
                    ahora,
                    empresaId,
                    motivo,
                    catalogo.NombreDe(evento.GetType()),
                    JsonSerializer.Serialize(evento, evento.GetType(), s_json)));
            }
        }
    }

    private static void Olvidar(DbContextEventData datos)
    {
        if (datos.Context is not { } contexto)
        {
            return;
        }

        // SIN filtrar por estado, al contrario que al volcar: el guardado ya ha ido bien, así que
        // lo que se acaba de escribir está en `Unchanged`. Filtrar aquí como allí no encontraría
        // nada y la lista no se limpiaría nunca.
        foreach (RaizAgregado raiz in ConEventos(contexto).Select(entrada => entrada.Entity).ToList())
        {
            raiz.OlvidarEventos();
        }
    }

    private static IEnumerable<EntityEntry<RaizAgregado>> ConEventos(DbContext contexto) =>
        contexto.ChangeTracker
            .Entries<RaizAgregado>()
            .Where(entrada => entrada.Entity.EventosPendientes.Count > 0);

    // Un agregado que alguien haya cargado para leer y al que un caso de uso le registre un evento
    // sin guardarlo NO publica nada, y eso es lo correcto: no ha pasado nada que contar.
    private static bool SeEstaGuardando(EntityEntry<RaizAgregado> entrada) =>
        entrada.State is EntityState.Added or EntityState.Modified or EntityState.Deleted;

    private MotivoSinInquilino DeclararSinInquilino() =>
        inquilino.MotivoDelAmbito ?? throw new FaltaLaEmpresaActivaException();
}
