using Bastion.BuildingBlocks.Domain.Eventos;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Microsoft.EntityFrameworkCore;

namespace Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;

/// <summary>
/// Mapea la bandeja de salida y el registro de lo ya procesado. Las aplican <b>todos</b> los
/// contextos, apuntando al esquema <c>auditoria</c>, y solo uno de ellos las migra.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es la misma forma que la traza del 0.7, y por el mismo motivo</b> (ADR-0012, punto 1): que
/// cada contexto de módulo conozca la tabla es lo único que hace que el evento y el cambio caigan
/// en el mismo <c>SaveChanges</c> y, por tanto, en la misma transacción, sin obligar a ningún caso
/// de uso a abrir una transacción explícita. Un contexto aparte alistado en la transacción del
/// módulo obligaría a cambiar <b>todos</b> los caminos de escritura de <b>todos</b> los módulos; un
/// <c>INSERT</c> a mano sobre la conexión sería SQL crudo esquivando el barrido del 0.6.
/// </para>
/// <para>
/// <b>Por qué en el esquema <c>auditoria</c> y no en uno propio.</b> El §5 lista dieciséis módulos y
/// ninguno es la bandeja: no hay módulo del que pudiera ser el esquema, y la convención de
/// <c>docs/PLAN.md</c> dice que el esquema es el nombre del módulo. Inventar un decimoséptimo
/// módulo reabriría el §5, y un esquema sin módulo que lo migre no lo puede crear nadie. Queda el
/// mismo dueño que ya tiene la otra tabla que se escribe en la transacción de todos: el módulo
/// Auditoría. La decisión está escrita entera en el ADR-0013.
/// </para>
/// </remarks>
public static class ConfiguracionDeLaBandeja
{
    /// <summary>Esquema donde viven las dos tablas.</summary>
    public const string Esquema = ConfiguracionDeAuditoria.Esquema;

    /// <summary>Tabla de la bandeja de salida.</summary>
    public const string Tabla = "bandeja_de_salida";

    /// <summary>Tabla del registro de eventos ya procesados por cada consumidor.</summary>
    public const string TablaDeProcesados = "eventos_procesados";

    /// <summary>Nombre de la restricción que exige empresa o motivo, y solo uno de los dos.</summary>
    public const string RestriccionDeEmpresa = "ck_bandeja_empresa_o_motivo";

    /// <summary>Nombre del índice único sobre el identificador del evento.</summary>
    public const string IndiceDelEvento = "ix_bandeja_evento_id";

    /// <summary>Aplica el mapeo.</summary>
    /// <param name="modelo">Constructor del modelo.</param>
    /// <param name="migra">
    /// Si este contexto es el dueño de las tablas. Solo el de Auditoría dice que sí.
    /// </param>
    public static void Mapear(ModelBuilder modelo, bool migra)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        // Los eventos que un agregado lleva en la mano NO son una entidad: viajan en memoria desde
        // que el caso de uso los registra hasta que el interceptor los vuelca, y de la base salen
        // deserializados de una columna. Sin esta línea, EF Core intentaría mapear
        // `RaizAgregado.EventosPendientes` como navegación y exigiría una clave primaria para
        // `EventoDeIntegracion`, que es un tipo del dominio y no la tiene.
        modelo.Ignore<EventoDeIntegracion>();

        modelo.Entity<EventoDeLaBandeja>(evento =>
        {
            evento.ToTable(Tabla, Esquema, tabla =>
            {
                if (!migra)
                {
                    tabla.ExcludeFromMigrations();
                }

                // La invariante del constructor, otra vez, aquí abajo: protege la tabla de quien
                // inserte por otra vía, que es de lo que no protege una comprobación en C#.
                tabla.HasCheckConstraint(
                    RestriccionDeEmpresa,
                    "(empresa_id IS NULL) <> (sin_inquilino IS NULL)");
            });

            evento.HasKey(fila => fila.Id);

            evento.Property(fila => fila.EventoId).IsRequired();
            evento.Property(fila => fila.OcurridoEn).IsRequired();
            evento.Property(fila => fila.EmpresaId);

            evento.Property(fila => fila.SinInquilino).HasConversion<string>().HasMaxLength(64);
            evento.Property(fila => fila.Estado).HasConversion<string>().HasMaxLength(32).IsRequired();

            evento.Property(fila => fila.Nombre).HasMaxLength(128).IsRequired();
            evento.Property(fila => fila.Cuerpo).HasColumnType("jsonb").IsRequired();

            evento.Property(fila => fila.PublicadoEn);
            evento.Property(fila => fila.Intentos).IsRequired();
            evento.Property(fila => fila.UltimoError).HasMaxLength(EventoDeLaBandeja.MaximoDelError);

            // El mismo hecho no puede entrar dos veces en la cola. No es un lujo: el interceptor
            // vuelca los eventos del agregado, y un agregado que se guarde dos veces sin que nadie
            // le haya limpiado la lista publicaría el mismo hecho dos veces.
            evento.HasIndex(fila => fila.EventoId).IsUnique().HasDatabaseName(IndiceDelEvento);

            // Por donde se lee: la cola. El publicador pide lo pendiente en orden de llegada, y
            // esa es la única consulta caliente de la tabla —una cada pocos segundos, siempre—.
            evento.HasIndex(fila => new { fila.Estado, fila.Id });

            // Marcar una fila como publicada es una escritura, y auditarla sería anotar en la traza
            // el trabajo del cartero. Ruido con forma de dato: una fila de auditoría por evento
            // publicado, firmada por nadie, en la tabla que existe para contar quién cambió qué.
            evento.NoSeAudita("es la cola de eventos; publicarlos es fontanería, no un cambio de datos");
        });

        modelo.Entity<EventoProcesado>(procesado =>
        {
            procesado.ToTable(TablaDeProcesados, Esquema, tabla =>
            {
                if (!migra)
                {
                    tabla.ExcludeFromMigrations();
                }
            });

            // La clave ES la deduplicación: que sea primaria significa que el segundo intento de
            // apuntar lo mismo choca contra el motor, no contra un `if`. La comprobación previa
            // evita el choque en el caso normal; esto lo cierra en el caso de dos publicadores.
            procesado.HasKey(fila => new { fila.EventoId, fila.Consumidor });

            procesado.Property(fila => fila.Consumidor)
                .HasMaxLength(EventoProcesado.MaximoDelConsumidor)
                .IsRequired();

            procesado.Property(fila => fila.ProcesadoEn).IsRequired();

            procesado.NoSeAudita("es la huella de que un consumidor ya pasó; no es un cambio de datos");
        });
    }
}
