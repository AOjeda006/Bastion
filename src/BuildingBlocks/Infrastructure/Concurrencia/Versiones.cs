using Bastion.BuildingBlocks.Application.Concurrencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Bastion.BuildingBlocks.Infrastructure.Concurrencia;

/// <summary>
/// <see cref="IVersiones"/> sobre un <see cref="DbContext"/>: lee el testigo de lo que ese
/// contexto rastrea y lo mete en el <c>WHERE</c> del <c>UPDATE</c> cuando se le exige.
/// </summary>
/// <remarks>
/// <para>
/// Se registra UNA POR MÓDULO, sobre el contexto del módulo, igual que la unidad de trabajo. Una
/// compartida leería el testigo de un contexto que no rastrea la entidad, que es justo el caso
/// que esta clase se niega a atender en silencio.
/// </para>
/// <para>
/// <b>Por qué <see cref="De"/> lanza en vez de devolver cero.</b> El testigo es una propiedad de
/// sombra: vive en el rastreador de cambios, no en la entidad. Si la entidad viene de una consulta
/// con <c>AsNoTracking()</c>, <c>Entry(entidad)</c> la ADJUNTA en ese momento y la propiedad de
/// sombra nace a cero —y <c>Entry(...).Property&lt;uint&gt;("Version").CurrentValue</c> devuelve
/// <c>0</c> sin lanzar nada. Comprobado contra PostgreSQL: 756 por el camino rastreado, 756
/// proyectando con <c>EF.Property</c>, y 0 por este. Ese cero compila, pasa los tests rápidos y
/// sale a producción dentro de un <c>ETag</c>, donde convierte todo <c>If-Match</c> en un
/// <c>412</c> perpetuo. Por eso aquí se comprueba el rastreo antes de preguntar, y el fallo es
/// ruidoso: quien necesite el testigo en un camino sin rastreo tiene que proyectarlo
/// (<c>Select(e =&gt; EF.Property&lt;uint&gt;(e, "Version"))</c>), y el mensaje se lo dice.
/// </para>
/// </remarks>
/// <param name="contexto">El contexto del módulo.</param>
public class Versiones(DbContext contexto) : IVersiones
{
    /// <inheritdoc/>
    public VersionDeRecurso De(object entidad) =>
        new((uint)Testigo(entidad).CurrentValue!);

    /// <inheritdoc/>
    public void Exigir(object entidad, VersionDeRecurso version) =>
        Testigo(entidad).OriginalValue = version.Valor;

    private PropertyEntry Testigo(object entidad)
    {
        ArgumentNullException.ThrowIfNull(entidad);

        EntityEntry? entrada = contexto.ChangeTracker.Entries()
            .FirstOrDefault(rastreada => ReferenceEquals(rastreada.Entity, entidad)) ?? throw new InvalidOperationException(
                $"El contexto {contexto.GetType().Name} no rastrea esta entidad de tipo " +
                $"{entidad.GetType().Name}, así que su testigo de concurrencia no sale por aquí: " +
                "EF Core la adjuntaría ahora y devolvería CERO sin avisar. Si viene de una " +
                "consulta con AsNoTracking(), proyecte el testigo en el Select con " +
                $"EF.Property<uint>(entidad, \"{TestigoDeConcurrencia.Nombre}\").");

        return entrada.Property(TestigoDeConcurrencia.Nombre);
    }
}
