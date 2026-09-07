using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.BuildingBlocks.Infrastructure.Retiradas;
using Bastion.Organizacion.Domain.Unidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeUnidadMedida : IEntityTypeConfiguration<UnidadMedida>
{
    public void Configure(EntityTypeBuilder<UnidadMedida> unidad)
    {
        ArgumentNullException.ThrowIfNull(unidad);

        unidad.ToTable("unidades_de_medida");

        unidad.SeAudita();
        unidad.HasKey(fila => fila.Id);

        unidad.LlevaTestigoDeConcurrencia();

        ConfiguracionDeEntidadBase.Mapear(unidad);

        unidad.Property(fila => fila.Codigo)
            .HasMaxLength(UnidadMedida.LongitudMaximaDeCodigo)
            .IsRequired()
            .SeAudita();

        unidad.Property(fila => fila.Nombre)
            .HasMaxLength(UnidadMedida.LongitudMaximaDeNombre)
            .IsRequired()
            .SeAudita();

        // Aquí SÍ es columna, al revés que los decimales de una divisa: a cuántos decimales se
        // pesa no lo dice ninguna norma, lo decide quien monta el almacén. El motivo entero está
        // escrito en la propia entidad.
        unidad.Property(fila => fila.Decimales).IsRequired().SeAudita();

        // ADR-0023: no se ofrece para lo nuevo, sigue resolviendo lo viejo. La columna es de
        // este maestro y no de `EntidadBase`: retirarse no le pasa a todo el mundo.
        ConfiguracionDeRetirada.Mapear(unidad);

        unidad.HasIndex(fila => fila.Codigo).IsUnique();
    }
}
