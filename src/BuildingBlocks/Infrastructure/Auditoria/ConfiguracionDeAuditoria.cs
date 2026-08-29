using Microsoft.EntityFrameworkCore;

namespace Bastion.BuildingBlocks.Infrastructure.Auditoria;

/// <summary>
/// Mapea la tabla de traza. La aplican <b>todos</b> los contextos de módulo, apuntando al esquema
/// <c>auditoria</c>, y solo uno de ellos la migra.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué la conoce cada contexto de módulo.</b> Es lo que hace que la traza y el cambio caigan
/// en el mismo <c>SaveChanges</c> y, por tanto, en la misma transacción, sin abrir transacción
/// explícita en ningún caso de uso. Las dos alternativas se miraron y cuestan más: un contexto de
/// auditoría aparte alistado en la transacción del módulo obligaría a que <b>todos</b> los caminos
/// de escritura de <b>todos</b> los módulos pasaran de un <c>SaveChangesAsync</c> pelado a una
/// transacción explícita; y un <c>INSERT</c> a mano sobre la conexión sería SQL crudo, esquivaría
/// el barrido de prohibiciones del 0.6 por la puerta de atrás y dejaría la forma de la tabla sin
/// describir en el modelo.
/// </para>
/// <para>
/// <b>El esquema va explícito.</b> Cada contexto de módulo llama a <c>HasDefaultSchema</c> con el
/// suyo, así que sin decirlo esta tabla acabaría en <c>organizacion</c> desde un contexto y en
/// <c>identidad</c> desde otro: dos tablas distintas con el mismo nombre y media traza en cada una.
/// </para>
/// <para>
/// <b>Sin claves ajenas hacia fuera.</b> La traza apunta a filas de otros esquemas por
/// identificador y en texto, que es lo que manda la regla 4 del §4 — y además es lo único que
/// funciona: una clave ajena obligaría a que la fila referida siguiera existiendo, y una traza que
/// desaparece cuando desaparece lo que traza no es una traza.
/// </para>
/// </remarks>
public static class ConfiguracionDeAuditoria
{
    /// <summary>Esquema del módulo Auditoría.</summary>
    public const string Esquema = "auditoria";

    /// <summary>Tabla de traza.</summary>
    public const string Tabla = "registros";

    /// <summary>Nombre de la restricción que exige empresa o motivo, y solo uno de los dos.</summary>
    public const string RestriccionDeEmpresa = "ck_registros_empresa_o_motivo";

    /// <summary>Aplica el mapeo.</summary>
    /// <param name="modelo">Constructor del modelo.</param>
    /// <param name="migra">
    /// Si este contexto es el dueño de la tabla. Solo el de Auditoría dice que sí; los demás la
    /// mapean para poder escribir en ella, pero no la crean —si la crearan, cada módulo tendría su
    /// propia versión de la misma tabla en su cadena de migraciones y la primera en aplicarse
    /// ganaría—.
    /// </param>
    public static void Mapear(ModelBuilder modelo, bool migra)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        modelo.Entity<RegistroDeAuditoria>(registro =>
        {
            registro.ToTable(Tabla, Esquema, tabla =>
            {
                if (!migra)
                {
                    tabla.ExcludeFromMigrations();
                }

                // La invariante del constructor, otra vez, aquí abajo. No es duplicidad: el
                // constructor protege el camino de la aplicación y esta protege la tabla, que es
                // la que va a seguir ahí cuando alguien inserte por otra vía.
                tabla.HasCheckConstraint(
                    RestriccionDeEmpresa,
                    "(empresa_id IS NULL) <> (sin_inquilino IS NULL)");
            });

            registro.HasKey(fila => fila.Id);

            registro.Property(fila => fila.CorrelacionId).IsRequired();
            registro.Property(fila => fila.OcurridoEn).IsRequired();
            registro.Property(fila => fila.EmpresaId);
            registro.Property(fila => fila.UsuarioId);

            // Enumerados como TEXTO, igual que en el resto del sistema: guardados por su número
            // dejan de significar nada en cuanto alguien reordena el enumerado, y estos datos, por
            // definición, duran más que el código.
            registro.Property(fila => fila.SinInquilino).HasConversion<string>().HasMaxLength(64);
            registro.Property(fila => fila.Cambio).HasConversion<string>().HasMaxLength(32).IsRequired();

            registro.Property(fila => fila.Entidad).HasMaxLength(128).IsRequired();
            registro.Property(fila => fila.EntidadId).HasMaxLength(256).IsRequired();

            // `jsonb` y no `text`: es lo que permite preguntar «qué cambios tocaron esta columna»
            // con un índice, que es la consulta que justifica haber elegido una fila por entidad
            // cambiada en vez de una por propiedad.
            registro.Property(fila => fila.Valores).HasColumnType("jsonb").IsRequired();

            // Por dónde se lee una traza: por la fila de la que se habla, y por cuándo.
            registro.HasIndex(fila => new { fila.Entidad, fila.EntidadId });
            registro.HasIndex(fila => fila.OcurridoEn);
            registro.HasIndex(fila => fila.CorrelacionId);

            // El interceptor no se audita a sí mismo. Parece obvio hasta que la recursión lo
            // recuerda: cada fila de traza sería un cambio, que generaría su fila de traza.
            registro.NoSeAudita("es la traza; auditarla sería recursión, no información");
        });
    }
}
