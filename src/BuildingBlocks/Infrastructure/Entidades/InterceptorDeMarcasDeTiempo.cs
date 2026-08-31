using Bastion.BuildingBlocks.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bastion.BuildingBlocks.Infrastructure.Entidades;

/// <summary>
/// Pone <c>ModificadoEn</c> en todo lo que se está modificando, leyendo el reloj inyectado.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué esta marca la pone un interceptor y la de creación no.</b> <c>CreadoEn</c> se pone
/// una vez y en un solo sitio por entidad —su fábrica—, así que el dominio la puede sostener y,
/// sosteniéndola, la entidad nace completa: una prueba unitaria que nunca ve una base de datos
/// tiene delante una entidad con su fecha. <c>ModificadoEn</c> cambia en <b>todos</b> los métodos
/// que tocan algo, presentes y futuros; sostenerla a mano querría decir añadir un
/// <c>DateTimeOffset momento</c> a cada uno de ellos, y el día que alguien escriba un método nuevo
/// y no se acuerde, la marca deja de moverse <b>sin que nada falle</b>. Aquí no hay nada de lo que
/// acordarse.
/// </para>
/// <para>
/// <b>No es un <c>DEFAULT now()</c>.</b> La hora sale del <c>TimeProvider</c> inyectado, que es el
/// mismo que usa el resto del sistema y el que una prueba puede adelantar. Con un <c>DEFAULT</c>
/// la pondría el reloj del servidor de base de datos, y además sería una forma nueva de valor
/// generado por el servidor en un modelo donde lo único que lo genera son los seis testigos de
/// concurrencia (ADR-0015).
/// </para>
/// <para>
/// <b>Escribe por el rastreador y no por el <i>setter</i>.</b> <c>ModificadoEn</c> tiene un
/// <c>set</c> privado y sigue teniéndolo: abrirlo para que esto pudiera escribirlo habría dejado
/// un <i>setter</i> público en el tipo base que cualquiera puede mover a cualquier valor. EF Core
/// escribe la propiedad por su propio acceso, que no necesita permiso de C#.
/// </para>
/// <para>
/// <b>Y no toca las altas.</b> En un <c>Added</c> la fábrica ya dejó las dos marcas puestas en el
/// mismo instante; volver a escribirla aquí las separaría por lo que tarde el caso de uso, y una
/// entidad recién creada diría que se modificó después de crearse.
/// </para>
/// </remarks>
/// <param name="reloj">De dónde sale la hora.</param>
public sealed class InterceptorDeMarcasDeTiempo(TimeProvider reloj) : SaveChangesInterceptor
{
    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Marcar(eventData);

        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Marcar(eventData);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Marcar(DbContextEventData datos)
    {
        ArgumentNullException.ThrowIfNull(datos);

        if (datos.Context is not { } contexto)
        {
            return;
        }

        DateTimeOffset ahora = reloj.GetUtcNow();

        foreach (EntityEntry<EntidadBase> entrada in
            contexto.ChangeTracker.Entries<EntidadBase>().Where(e => e.State == EntityState.Modified))
        {
            entrada.Property(entidad => entidad.ModificadoEn).CurrentValue = ahora;
        }
    }
}
