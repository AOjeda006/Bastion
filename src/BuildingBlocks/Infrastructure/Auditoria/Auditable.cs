using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.BuildingBlocks.Infrastructure.Auditoria;

/// <summary>
/// Cómo se dice, en la configuración de EF Core, qué se audita y qué no.
/// </summary>
/// <remarks>
/// <para>
/// La clasificación va en el <b>modelo</b>, junto a la línea que ya mapea la propiedad, y no en
/// una tabla aparte con los nombres escritos a mano. Dos motivos, y el segundo es el que importa:
/// quien añade una columna la ve ahí mismo y tiene que decidir en el sitio; y una tabla de cadenas
/// en un proyecto común obligaría al módulo diecisiete a editar los bloques comunes para
/// declararse, que es exactamente la frontera que el §4 no quiere que se cruce.
/// </para>
/// <para>
/// Se lee del modelo <b>ya construido</b>, así que el barrido que exige que no falte ninguna mira
/// lo mismo que mirará el interceptor en tiempo de ejecución. Es la misma forma que
/// <c>CadaEntidadDeclaraSuInquilinatoTests</c> usa para el inquilinato del 0.6, y por el mismo
/// motivo: es lo único que escala a los dieciséis módulos del §5.
/// </para>
/// </remarks>
public static class Auditable
{
    /// <summary>Nombre de la anotación que lleva la clasificación.</summary>
    public const string Anotacion = "Bastion:Auditoria";

    /// <summary>Sus cambios dejan traza.</summary>
    /// <typeparam name="T">Tipo de la entidad.</typeparam>
    /// <param name="entidad">Constructor de la entidad.</param>
    /// <returns>El mismo constructor, para encadenar.</returns>
    public static EntityTypeBuilder<T> SeAudita<T>(this EntityTypeBuilder<T> entidad)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(entidad);

        return entidad.HasAnnotation(Anotacion, Escribir(ClasificacionDeAuditoria.Auditada, string.Empty));
    }

    /// <summary>Sus cambios NO dejan traza, y aquí se dice por qué.</summary>
    /// <typeparam name="T">Tipo de la entidad.</typeparam>
    /// <param name="entidad">Constructor de la entidad.</param>
    /// <param name="motivo">Por qué queda fuera. No puede estar vacío.</param>
    /// <returns>El mismo constructor, para encadenar.</returns>
    public static EntityTypeBuilder<T> NoSeAudita<T>(this EntityTypeBuilder<T> entidad, string motivo)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(entidad);
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);

        return entidad.HasAnnotation(Anotacion, Escribir(ClasificacionDeAuditoria.NoAuditada, motivo));
    }

    /// <summary>Su valor viejo y su valor nuevo van a la traza.</summary>
    /// <typeparam name="T">Tipo de la propiedad.</typeparam>
    /// <param name="propiedad">Constructor de la propiedad.</param>
    /// <returns>El mismo constructor, para encadenar.</returns>
    public static PropertyBuilder<T> SeAudita<T>(this PropertyBuilder<T> propiedad)
    {
        ArgumentNullException.ThrowIfNull(propiedad);

        return propiedad.HasAnnotation(Anotacion, Escribir(ClasificacionDeAuditoria.Auditada, string.Empty));
    }

    /// <summary>Queda fuera de la traza a propósito.</summary>
    /// <typeparam name="T">Tipo de la propiedad.</typeparam>
    /// <param name="propiedad">Constructor de la propiedad.</param>
    /// <param name="motivo">Por qué queda fuera. No puede estar vacío.</param>
    /// <returns>El mismo constructor, para encadenar.</returns>
    public static PropertyBuilder<T> NoSeAudita<T>(this PropertyBuilder<T> propiedad, string motivo)
    {
        ArgumentNullException.ThrowIfNull(propiedad);
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);

        return propiedad.HasAnnotation(Anotacion, Escribir(ClasificacionDeAuditoria.NoAuditada, motivo));
    }

    /// <summary>
    /// Es un secreto: no puede acabar en la traza por ningún camino.
    /// </summary>
    /// <remarks>
    /// La diferencia con <see cref="NoSeAudita{T}(PropertyBuilder{T}, string)"/> no es de grado. Una
    /// tabla que por diseño no se puede limpiar y que guarda el valor viejo y el nuevo de cada
    /// propiedad es, sin que nadie lo decida, el historial completo de resúmenes de contraseña de
    /// todo el mundo. Marcar así una propiedad la mete además en el test que comprueba, por el
    /// valor y no por el nombre, que no aparece en ninguna fila.
    /// </remarks>
    /// <typeparam name="T">Tipo de la propiedad.</typeparam>
    /// <param name="propiedad">Constructor de la propiedad.</param>
    /// <param name="motivo">Qué secreto es. No puede estar vacío.</param>
    /// <returns>El mismo constructor, para encadenar.</returns>
    public static PropertyBuilder<T> EsSecreta<T>(this PropertyBuilder<T> propiedad, string motivo)
    {
        ArgumentNullException.ThrowIfNull(propiedad);
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);

        return propiedad.HasAnnotation(Anotacion, Escribir(ClasificacionDeAuditoria.Secreta, motivo));
    }

    /// <summary>Sus cambios dejan traza. Para una propiedad DENTRO de un tipo complejo.</summary>
    /// <remarks>
    /// <para>
    /// Existe porque <c>ComplexPropertyBuilder&lt;T&gt;.Property(…)</c> no devuelve un
    /// <see cref="PropertyBuilder{T}"/> sino un <see cref="ComplexTypePropertyBuilder{T}"/>, que
    /// es otro tipo sin ancestro común útil. Sin esta sobrecarga, mover un objeto de valor de
    /// tipo poseído a tipo complejo <b>deja de compilar</b> — y eso es lo mejor que puede pasar:
    /// la alternativa era que compilara y que las propiedades se quedaran sin clasificar.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">Tipo de la propiedad.</typeparam>
    /// <param name="propiedad">Constructor de la propiedad.</param>
    /// <returns>El mismo constructor, para encadenar.</returns>
    public static ComplexTypePropertyBuilder<T> SeAudita<T>(this ComplexTypePropertyBuilder<T> propiedad)
    {
        ArgumentNullException.ThrowIfNull(propiedad);

        return propiedad.HasAnnotation(Anotacion, Escribir(ClasificacionDeAuditoria.Auditada, string.Empty));
    }

    /// <summary>Queda fuera de la traza a propósito. Para una propiedad de un tipo complejo.</summary>
    /// <typeparam name="T">Tipo de la propiedad.</typeparam>
    /// <param name="propiedad">Constructor de la propiedad.</param>
    /// <param name="motivo">Por qué queda fuera. No puede estar vacío.</param>
    /// <returns>El mismo constructor, para encadenar.</returns>
    public static ComplexTypePropertyBuilder<T> NoSeAudita<T>(
        this ComplexTypePropertyBuilder<T> propiedad,
        string motivo)
    {
        ArgumentNullException.ThrowIfNull(propiedad);
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);

        return propiedad.HasAnnotation(Anotacion, Escribir(ClasificacionDeAuditoria.NoAuditada, motivo));
    }

    /// <summary>Es un secreto. Para una propiedad de un tipo complejo.</summary>
    /// <typeparam name="T">Tipo de la propiedad.</typeparam>
    /// <param name="propiedad">Constructor de la propiedad.</param>
    /// <param name="motivo">Qué secreto es. No puede estar vacío.</param>
    /// <returns>El mismo constructor, para encadenar.</returns>
    public static ComplexTypePropertyBuilder<T> EsSecreta<T>(
        this ComplexTypePropertyBuilder<T> propiedad,
        string motivo)
    {
        ArgumentNullException.ThrowIfNull(propiedad);
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);

        return propiedad.HasAnnotation(Anotacion, Escribir(ClasificacionDeAuditoria.Secreta, motivo));
    }

    /// <summary>Qué se hace con esta entidad cuando cambia.</summary>
    /// <param name="tipo">Tipo de entidad del modelo ya construido.</param>
    /// <returns>Su clasificación y el motivo, si lo lleva.</returns>
    public static (ClasificacionDeAuditoria Que, string Motivo) Auditoria(this IReadOnlyEntityType tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        return Leer(tipo.FindAnnotation(Anotacion)?.Value as string);
    }

    /// <summary>Qué se hace con esta propiedad cuando cambia.</summary>
    /// <param name="propiedad">Propiedad del modelo ya construido.</param>
    /// <returns>Su clasificación y el motivo, si lo lleva.</returns>
    public static (ClasificacionDeAuditoria Que, string Motivo) Auditoria(this IReadOnlyProperty propiedad)
    {
        ArgumentNullException.ThrowIfNull(propiedad);

        return Leer(propiedad.FindAnnotation(Anotacion)?.Value as string);
    }

    /// <summary>
    /// Todas las propiedades escalares de un tipo, <b>incluidas las que viven dentro de un tipo
    /// complejo</b>, con el camino por el que se llega a cada una.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué hace falta un recorrido y no basta <c>GetProperties()</c>.</b> Un tipo poseído
    /// es, para EF Core, un tipo de entidad más: sus propiedades salen en
    /// <c>modelo.GetEntityTypes()</c> y los barridos las ven sin hacer nada. Un tipo complejo no:
    /// sus propiedades están en <c>GetComplexProperties()</c> y <c>GetProperties()</c> no las
    /// devuelve. El día que un objeto de valor pase de poseído a complejo —que es lo que pide la
    /// convención de .NET— sus columnas desaparecerían de todos los barridos <b>a la vez y sin un
    /// solo rojo</b>. Medido en el 0.10 antes de cambiar nada: 152 propiedades escalares con la
    /// dirección poseída, 138 con la dirección compleja, y los catorce barridos en verde.
    /// </para>
    /// <para>
    /// El camino se compone con puntos —<c>Direccion.Calle</c>— y es el mismo que la traza escribe
    /// para un tipo poseído, así que el formato de la tabla no cambia al cambiar el mapeo.
    /// </para>
    /// </remarks>
    /// <param name="tipo">Tipo de entidad o tipo complejo del modelo ya construido.</param>
    /// <returns>Cada propiedad con su camino desde la entidad.</returns>
    public static IEnumerable<(string Camino, IReadOnlyProperty Propiedad)> PropiedadesConCamino(
        this IReadOnlyTypeBase tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        return ConPrefijo(tipo, string.Empty);
    }

    private static IEnumerable<(string Camino, IReadOnlyProperty Propiedad)> ConPrefijo(
        IReadOnlyTypeBase tipo,
        string prefijo)
    {
        foreach (IReadOnlyProperty propiedad in tipo.GetProperties())
        {
            yield return (prefijo + propiedad.Name, propiedad);
        }

        foreach (IReadOnlyComplexProperty compleja in tipo.GetComplexProperties())
        {
            foreach ((string camino, IReadOnlyProperty propiedad) in
                ConPrefijo(compleja.ComplexType, $"{prefijo}{compleja.Name}."))
            {
                yield return (camino, propiedad);
            }
        }
    }

    private static string Escribir(ClasificacionDeAuditoria que, string motivo) => $"{que}|{motivo}";

    private static (ClasificacionDeAuditoria Que, string Motivo) Leer(string? anotacion)
    {
        if (anotacion is null)
        {
            return (ClasificacionDeAuditoria.SinClasificar, string.Empty);
        }

        string[] partes = anotacion.Split('|', 2);

        return Enum.TryParse(partes[0], out ClasificacionDeAuditoria que)
            ? (que, partes.Length > 1 ? partes[1] : string.Empty)
            : (ClasificacionDeAuditoria.SinClasificar, string.Empty);
    }
}
