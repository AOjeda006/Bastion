using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.BuildingBlocks.Infrastructure.Concurrencia;

/// <summary>
/// El testigo de concurrencia optimista (R11), en un solo sitio: qué propiedad es, cómo se
/// declara y cómo se reconoce.
/// </summary>
/// <remarks>
/// <para>
/// <b>El testigo es <c>xmin</c>, la columna de sistema de PostgreSQL</b>, y no una columna
/// nuestra. Toda fila la lleva ya —es el identificador de la transacción que la escribió—, así
/// que no hay columna que crear, ni valor que mantener, ni ningún camino de escritura que pueda
/// olvidarse de incrementarla: la incrementa el motor. Una columna propia habría que subirla en
/// cada <c>UPDATE</c>, y el día que alguien escriba por SQL directo dejaría de subir sin que
/// nadie se entere.
/// </para>
/// <para>
/// <b>Cómo se le dice eso a EF Core.</b> Desde Npgsql 9 no existe <c>UseXminAsConcurrencyToken()</c>.
/// La forma soportada es la de aquí, y la reconoce una convención del proveedor
/// (<c>ProcessRowVersionProperty</c>): toda propiedad <see cref="uint"/> marcada
/// <c>ValueGeneratedOnAddOrUpdate</c> y como testigo de concurrencia se mapea sola a la columna
/// <c>xmin</c> de tipo <c>xid</c>. Comprobado por el efecto: la migración que sale nombra
/// <c>xmin</c>, y el SQL que genera está vacío porque el generador del proveedor no emite nada
/// para las columnas de sistema.
/// </para>
/// <para>
/// <b>Es una propiedad de sombra, y no está en el dominio.</b> Un <c>xmin</c> en una entidad de
/// negocio sería PostgreSQL metido en el modelo: el dominio no tiene por qué saber cómo detecta
/// la base que alguien escribió antes. El precio de tenerla en la sombra está documentado en
/// <see cref="Versiones"/>, y es el motivo de que esa clase lance en vez de devolver cero.
/// </para>
/// </remarks>
public static class TestigoDeConcurrencia
{
    /// <summary>
    /// Nombre de la propiedad de sombra. La columna se llama <c>xmin</c>; esto es el nombre con
    /// el que se la pide a EF Core.
    /// </summary>
    public const string Nombre = "Version";

    /// <summary>Declara que la entidad lleva testigo de concurrencia.</summary>
    /// <typeparam name="T">La entidad.</typeparam>
    /// <param name="entidad">Su constructor de configuración.</param>
    /// <returns>El mismo constructor, para poder encadenar.</returns>
    public static EntityTypeBuilder<T> LlevaTestigoDeConcurrencia<T>(this EntityTypeBuilder<T> entidad)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(entidad);

        entidad.Property<uint>(Nombre)
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken()
            .NoSeAudita(
                "es el testigo de concurrencia (xmin): lo pone PostgreSQL en cada escritura y no " +
                "es un dato del negocio. Auditar su cambio sería anotar que hubo un cambio en la " +
                "misma fila que ya anota cuál fue.");

        return entidad;
    }

    /// <summary>¿Es esta propiedad el testigo de concurrencia?</summary>
    /// <param name="propiedad">La propiedad del modelo.</param>
    /// <remarks>
    /// Lo usan los barridos que enumeran lo que la base genera. Se pregunta por el NOMBRE y
    /// además por lo que la hace un testigo, para que una propiedad llamada <c>Version</c> que
    /// no lo fuera no se colara por parecerse.
    /// </remarks>
    public static bool EsElTestigo(this IReadOnlyProperty propiedad)
    {
        ArgumentNullException.ThrowIfNull(propiedad);

        return string.Equals(propiedad.Name, Nombre, StringComparison.Ordinal)
            && propiedad.IsConcurrencyToken
            && propiedad.ValueGenerated == ValueGenerated.OnAddOrUpdate
            && propiedad.ClrType == typeof(uint);
    }
}
