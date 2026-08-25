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
        serie.HasKey(fila => fila.Id);

        serie.Property(fila => fila.EmpresaId).IsRequired();
        serie.Property(fila => fila.EjercicioId).IsRequired();

        serie.Property(fila => fila.TipoDeDocumento)
            .HasConversion<string>()
            .IsRequired();

        serie.Property(fila => fila.Estado)
            .HasConversion<string>()
            .IsRequired();

        serie.Property(fila => fila.Codigo)
            .HasMaxLength(Serie.LongitudMaximaDeCodigo)
            .IsRequired();

        // El formato es una plantilla, no un identificador: no tiene tope legal, así que `text`.
        serie.Property(fila => fila.Formato).IsRequired();

        // EL CONTADOR ES UNA COLUMNA, y esto es una decisión de esquema sin segunda oportunidad.
        // Una secuencia de PostgreSQL sería lo cómodo, pero `nextval` NO se revierte al deshacer
        // la transacción: una confirmación que falla dejaría un hueco permanente, y R5 dice
        // «correlativa y sin huecos». El número lo asigna Facturación (fase 5) bloqueando esta
        // fila dentro de la transacción de confirmación.
        serie.Property(fila => fila.Contador).IsRequired();

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
