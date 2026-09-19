using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Organizacion.Domain.Ejercicios;
using Bastion.Organizacion.Domain.Empresas;
using Bastion.Organizacion.Domain.Series;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeSerie : IEntityTypeConfiguration<Serie>
{
    public void Configure(EntityTypeBuilder<Serie> serie)
    {
        ArgumentNullException.ThrowIfNull(serie);

        serie.ToTable("series");

        // Maestro, y el mas delicado de los tres: la numeracion de una serie es legal (R5).
        serie.SeAudita();
        serie.HasKey(fila => fila.Id);

        // EL TESTIGO ES `xmin`, y de ahí sale la decisión de abajo: `xmin` lo mueve PostgreSQL en
        // CADA escritura de esta fila, y este testigo es el `ETag` que publica el GET y el que el
        // PUT exige en `If-Match`. Cualquier cosa que se escriba aquí a diario tira el `ETag` de
        // quien tenga la ficha abierta. Por eso el contador ya no está en esta tabla (ADR-0039).
        serie.LlevaTestigoDeConcurrencia();

        ConfiguracionDeEntidadBase.Mapear(serie);

        serie.Property(fila => fila.EmpresaId).IsRequired().SeAudita();
        serie.Property(fila => fila.EjercicioId).IsRequired().SeAudita();

        serie.Property(fila => fila.TipoDeDocumento)
            .HasConversion<string>()
            .IsRequired()
            .SeAudita();

        serie.Property(fila => fila.Estado)
            .HasConversion<string>()
            .IsRequired()
            .SeAudita();

        serie.Property(fila => fila.Codigo)
            .HasMaxLength(Serie.LongitudMaximaDeCodigo)
            .IsRequired()
            .SeAudita();

        // El formato es una plantilla, no un identificador: no tiene tope legal, así que `text`.
        serie.Property(fila => fila.Formato).IsRequired().SeAudita();

        // LA FILA DEL CONTADOR, que ya no es una columna de aquí (ADR-0039).
        //
        // REQUERIDA Y DE CARGA AUTOMÁTICA, y las dos mitades son la misma decisión: `Serie.Contador`
        // lee de esta navegación, y una serie que llegara sin ella contestaría... nada, porque la
        // propiedad lanza. Que NUNCA llegue sin ella lo garantiza el `AutoInclude`, que no se puede
        // olvidar en una consulta porque no hay que escribirlo en ninguna.
        //
        // EN CASCADA DEL LADO DEL CLIENTE, no de la base. La clave ajena queda `RESTRICT`, como las
        // cuatro del ADR-0007 §8 —en un ERP una cascada en el motor es la forma más rápida de
        // perder un histórico—, y quien borra la hija es el ORM con la fila delante: así ese
        // `DELETE` lleva DENTRO el testigo que se leyó, que es lo único que separa suprimir una
        // serie de suprimirla justo cuando otro le está sacando el primer número.
        serie.HasOne(fila => fila.Numeracion)
            .WithOne()
            .HasForeignKey<ContadorDeSerie>(fila => fila.SerieId)
            .IsRequired()
            .OnDelete(DeleteBehavior.ClientCascade);

        serie.Navigation(fila => fila.Numeracion).AutoInclude();

        serie.HasIndex(fila => new { fila.EmpresaId, fila.EjercicioId, fila.Codigo }).IsUnique();

        serie.HasOne<Empresa>()
            .WithMany()
            .HasForeignKey(fila => fila.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        serie.HasOne<Ejercicio>()
            .WithMany()
            .HasForeignKey(fila => fila.EjercicioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
