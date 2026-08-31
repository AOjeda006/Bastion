using Bastion.BuildingBlocks.Application.Idempotencia;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Microsoft.EntityFrameworkCore;

namespace Bastion.BuildingBlocks.Infrastructure.Idempotencia;

/// <summary>
/// Mapea la tabla de claves de idempotencia. La aplican <b>todos</b> los contextos, apuntando al
/// esquema <c>auditoria</c>, y solo uno de ellos la migra.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es la misma forma que la traza del 0.7 y la bandeja del 0.8, y por el mismo motivo</b>
/// (ADR-0012 punto 1, ADR-0013): que cada contexto de módulo conozca la tabla es lo único que hace
/// que la fila de idempotencia y el cambio de negocio caigan en el mismo <c>SaveChanges</c> y, por
/// tanto, en la misma transacción. Un contexto aparte no serviría: la clave quedaría reclamada
/// aunque el trabajo se deshiciera, o el trabajo confirmado sin clave que lo recuerde, y las dos
/// mitades de ese fallo son invisibles hasta que alguien reintenta.
/// </para>
/// <para>
/// <b>Esquema <c>auditoria</c>, por el mismo argumento del ADR-0013</b>, que no se repite aquí y se
/// cita: el §5 lista dieciséis módulos y ninguno es «idempotencia»; un esquema sin módulo que lo
/// migre no lo crea nadie; y el dueño natural es el módulo que ya guarda las otras dos tablas que
/// se escriben dentro de la transacción de todos. La alternativa —una tabla por módulo, en el
/// esquema de cada uno— se descartó por lo que costaría: la misma tabla repetida dieciséis veces,
/// dieciséis migraciones que mantener a la par, y un barrido de «¿todas iguales?» que hoy no hace
/// falta.
/// </para>
/// </remarks>
public static class ConfiguracionDeIdempotencia
{
    /// <summary>Esquema donde vive la tabla.</summary>
    public const string Esquema = ConfiguracionDeAuditoria.Esquema;

    /// <summary>Tabla de claves de idempotencia.</summary>
    public const string Tabla = "claves_de_idempotencia";

    /// <summary>Aplica el mapeo.</summary>
    /// <param name="modelo">Constructor del modelo.</param>
    /// <param name="migra">
    /// Si este contexto es el dueño de la tabla. Solo el de Auditoría dice que sí.
    /// </param>
    public static void Mapear(ModelBuilder modelo, bool migra)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        modelo.Entity<RegistroDeIdempotencia>(registro =>
        {
            registro.ToTable(Tabla, Esquema, tabla =>
            {
                if (!migra)
                {
                    tabla.ExcludeFromMigrations();
                }
            });

            // LA CLAVE ES LA TUPLA ENTERA, y que sea la clave PRIMARIA es lo que hace que el
            // mecanismo funcione: la unicidad la impone el motor, no un `if` que dos peticiones
            // simultáneas pueden cruzar a la vez. Sin la empresa dentro, dos inquilinos que
            // eligieran la misma clave se pisarían la respuesta; sin el método y la ruta, la misma
            // clave contra dos recursos distintos sería la misma operación.
            registro.HasKey(fila => new
            {
                fila.EmpresaId,
                fila.UsuarioId,
                fila.Metodo,
                fila.Ruta,
                fila.Clave,
            });

            registro.Property(fila => fila.Metodo).HasMaxLength(16).IsRequired();
            registro.Property(fila => fila.Ruta).HasMaxLength(ClaveDeIdempotencia.MaximoDeLaRuta).IsRequired();
            registro.Property(fila => fila.Clave).HasMaxLength(ClaveDeIdempotencia.MaximoDeLaClave).IsRequired();

            registro.Property(fila => fila.Huella)
                .HasMaxLength(ClaveDeIdempotencia.LongitudDeLaHuella)
                .IsRequired();

            registro.Property(fila => fila.CreadaEn).IsRequired();

            // Anulables porque la fila nace antes que la respuesta; ver la invariante escrita en
            // `RegistroDeIdempotencia`. Ninguna fila confirmada las tiene a nulo.
            registro.Property(fila => fila.CodigoDeEstado);
            registro.Property(fila => fila.Cuerpo);
            registro.Property(fila => fila.TipoDeContenido).HasMaxLength(128);
            registro.Property(fila => fila.Etiqueta).HasMaxLength(128);
            registro.Property(fila => fila.Ubicacion).HasMaxLength(ClaveDeIdempotencia.MaximoDeLaRuta);

            // Reclamar una clave no es un cambio de datos del negocio: es la anotación de que una
            // petición ya se atendió. Auditarla duplicaría en la traza cada alta, una vez por el
            // recurso creado y otra por el recibo de haberlo creado.
            registro.NoSeAudita("es el recibo de una petición ya atendida, no un cambio de datos");
        });
    }
}
