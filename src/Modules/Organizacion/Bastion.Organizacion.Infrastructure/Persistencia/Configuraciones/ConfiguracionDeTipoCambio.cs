using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Organizacion.Domain.Divisas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeTipoCambio : IEntityTypeConfiguration<TipoCambio>
{
    public void Configure(EntityTypeBuilder<TipoCambio> cambio)
    {
        ArgumentNullException.ThrowIfNull(cambio);

        cambio.ToTable("tipos_de_cambio");

        // Se audita, y no por rutina: corregir un tipo de cambio cambia el contravalor de todo lo
        // que se convierta con él, y «quién lo tocó» es la primera pregunta cuando un importe no
        // cuadra.
        cambio.SeAudita();
        cambio.HasKey(fila => fila.Id);

        cambio.LlevaTestigoDeConcurrencia();

        ConfiguracionDeEntidadBase.Mapear(cambio);

        cambio.Property(fila => fila.DivisaOrigenId).IsRequired().SeAudita();
        cambio.Property(fila => fila.DivisaDestinoId).IsRequired().SeAudita();

        // `date`: un tipo de cambio es el de un día, no el de un instante (R14).
        cambio.Property(fila => fila.Fecha).IsRequired().SeAudita();

        // `numeric(19,6)`: seis decimales, los que publica el BCE, y trece dígitos por delante
        // para las divisas con mucha inflación acumulada. R6 — nunca coma flotante.
        cambio.Property(fila => fila.Tasa)
            .HasPrecision(19, TipoCambio.DecimalesDeLaTasa)
            .IsRequired()
            .SeAudita();

        // Un par de divisas y un día dan UN tipo de cambio. Dos filas iguales harían que la
        // conversión dependiera de cuál devolviera antes el plan de ejecución.
        cambio.HasIndex(fila => new { fila.DivisaOrigenId, fila.DivisaDestinoId, fila.Fecha })
            .IsUnique();

        // `Restrict` en las dos: una divisa que se usa en un tipo de cambio no se borra. Con
        // `Cascade`, borrar una divisa se llevaría por delante el histórico de conversiones, que
        // es justo lo que hace falta para reconstruir un contravalor antiguo.
        cambio.HasOne<Divisa>()
            .WithMany()
            .HasForeignKey(fila => fila.DivisaOrigenId)
            .OnDelete(DeleteBehavior.Restrict);

        cambio.HasOne<Divisa>()
            .WithMany()
            .HasForeignKey(fila => fila.DivisaDestinoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
