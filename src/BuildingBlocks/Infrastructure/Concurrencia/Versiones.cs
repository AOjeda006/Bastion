using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Entidades;
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
    /// <remarks>
    /// <para>
    /// <b>Y de paso deja la entidad TOCADA, que no es un extra: es lo que hace que la versión se
    /// mueva.</b> Meter el testigo en el <c>WHERE</c> solo sirve si hay un <c>UPDATE</c> donde
    /// meterlo. Una escritura que solo cambia lo que <b>cuelga</b> del agregado —colgar un
    /// contacto, quitar una cuenta— deja la fila del agregado <c>Unchanged</c>: EF Core arma
    /// entonces un mandato sin ninguna columna que asignar, la sentencia no toca ninguna fila y
    /// el guardado sale por <c>DbUpdateConcurrencyException</c>, que el borde traduce a un
    /// <b>412 perpetuo</b> — la escritura correcta es imposible y el mensaje dice que el recurso
    /// «ha cambiado mientras usted lo editaba», que es mentira.
    /// </para>
    /// <para>
    /// <b>Y esto NO es el arreglo del 412 que salía en esas rutas.</b> Se escribió creyendo que sí
    /// —el run <c>34097268237</c> daba 412 justo en las escrituras que solo tocan hijos, y la
    /// explicación encajaba—, y el run <c>34101097530</c>, con esto ya puesto, volvió a dar los
    /// mismos 412. La atribución era falsa y queda dicho aquí para que nadie la reutilice: el
    /// choque no lo levanta la ficha, porque <c>ManejadorDeVersionObsoleta</c> contestó
    /// «ya no queda recurso», y ese mensaje solo sale cuando <b>ninguna</b> de las entradas del
    /// choque lleva testigo — y la ficha lo lleva.
    /// </para>
    /// <para>
    /// Lo que este método sí sostiene, y por lo que se queda, es el invariante de la etiqueta:
    /// insertar un hijo no cambia el <c>xmin</c> de la fila del padre, así que sin tocar la ficha
    /// su <c>ETag</c> no se movería y dos clientes podrían colgar cosas sobre la misma foto
    /// creyendo cada uno que la tenía al día.
    /// </para>
    /// <para>
    /// <b>Aquí y no en cada caso de uso.</b> Es el mismo argumento que el de
    /// <c>InterceptorDeMarcasDeTiempo</c>: sostenerlo a mano significa que el día que alguien
    /// escriba la séptima ruta que cuelga algo y no se acuerde, la versión deja de moverse. Y no
    /// es magia: quien <b>exige</b> una versión para escribir está diciendo que escribe, así que
    /// la marca de modificación de esa fila tiene que avanzar. Para las escrituras que ya cambian
    /// el agregado no cambia nada —el interceptor ya movía <c>ModificadoEn</c>—, y para un
    /// <c>Eliminar</c> posterior tampoco, porque <c>Remove</c> manda sobre el estado.
    /// </para>
    /// </remarks>
    public void Exigir(object entidad, VersionDeRecurso version)
    {
        EntityEntry entrada = Entrada(entidad);

        entrada.Property(TestigoDeConcurrencia.Nombre).OriginalValue = version.Valor;

        // El `is` y no un `as` a ciegas: no toda entidad con testigo lleva las dos marcas de
        // `EntidadBase`, y forzar la propiedad en una que no la tiene sería una excepción por
        // reflexión en mitad de una escritura buena.
        if (entrada.Entity is EntidadBase)
        {
            entrada.Property(nameof(EntidadBase.ModificadoEn)).IsModified = true;
        }
    }

    private PropertyEntry Testigo(object entidad) =>
        Entrada(entidad).Property(TestigoDeConcurrencia.Nombre);

    private EntityEntry Entrada(object entidad)
    {
        ArgumentNullException.ThrowIfNull(entidad);

        EntityEntry? entrada = contexto.ChangeTracker.Entries()
            .FirstOrDefault(rastreada => ReferenceEquals(rastreada.Entity, entidad)) ?? throw new InvalidOperationException(
                $"El contexto {contexto.GetType().Name} no rastrea esta entidad de tipo " +
                $"{entidad.GetType().Name}, así que su testigo de concurrencia no sale por aquí: " +
                "EF Core la adjuntaría ahora y devolvería CERO sin avisar. Si viene de una " +
                "consulta con AsNoTracking(), proyecte el testigo en el Select con " +
                $"EF.Property<uint>(entidad, \"{TestigoDeConcurrencia.Nombre}\").");

        return entrada;
    }
}
