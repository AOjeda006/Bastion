using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Organizacion.Domain.Unidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeConversionUM : IEntityTypeConfiguration<ConversionUM>
{
    public void Configure(EntityTypeBuilder<ConversionUM> conversion)
    {
        ArgumentNullException.ThrowIfNull(conversion);

        conversion.ToTable("conversiones_de_unidades");

        // Cambiar un factor cambia cuántas unidades sale de cada caja, y con ello el inventario
        // valorado de todo lo que se compre a partir de entonces.
        conversion.SeAudita();
        conversion.HasKey(fila => fila.Id);

        conversion.LlevaTestigoDeConcurrencia();

        ConfiguracionDeEntidadBase.Mapear(conversion);

        conversion.Property(fila => fila.UnidadOrigenId).IsRequired().SeAudita();
        conversion.Property(fila => fila.UnidadDestinoId).IsRequired().SeAudita();

        conversion.Property(fila => fila.Factor)
            .HasPrecision(19, ConversionUM.DecimalesDelFactor)
            .IsRequired()
            .SeAudita();

        // Un par de unidades da UN factor. Dos filas iguales harían que la conversión dependiera
        // del orden del plan de ejecución, y el descuadre aparecería en el inventario.
        //
        // Ojo con lo que este índice NO dice: CAJA→UD y UD→CAJA son dos pares distintos y las dos
        // filas conviven a propósito, cada una con su factor pensado. La entidad explica por qué
        // el inverso no se calcula solo.
        conversion.HasIndex(fila => new { fila.UnidadOrigenId, fila.UnidadDestinoId }).IsUnique();

        conversion.HasOne<UnidadMedida>()
            .WithMany()
            .HasForeignKey(fila => fila.UnidadOrigenId)
            .OnDelete(DeleteBehavior.Restrict);

        conversion.HasOne<UnidadMedida>()
            .WithMany()
            .HasForeignKey(fila => fila.UnidadDestinoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
