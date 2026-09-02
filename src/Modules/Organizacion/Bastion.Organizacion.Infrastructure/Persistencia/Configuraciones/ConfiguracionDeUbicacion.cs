using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Bloqueos;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Organizacion.Domain.Almacenes;
using Bastion.Organizacion.Domain.Empresas;
using Bastion.Organizacion.Domain.Ubicaciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeUbicacion : IEntityTypeConfiguration<Ubicacion>
{
    public void Configure(EntityTypeBuilder<Ubicacion> ubicacion)
    {
        ArgumentNullException.ThrowIfNull(ubicacion);

        ubicacion.ToTable("ubicaciones");

        ubicacion.SeAudita();
        ubicacion.HasKey(fila => fila.Id);

        ubicacion.LlevaTestigoDeConcurrencia();

        ConfiguracionDeEntidadBase.Mapear(ubicacion);

        ubicacion.Property(fila => fila.EmpresaId).IsRequired().SeAudita();
        ubicacion.Property(fila => fila.AlmacenId).IsRequired().SeAudita();

        ubicacion.Property(fila => fila.Codigo)
            .HasMaxLength(Ubicacion.LongitudMaximaDeCodigo)
            .IsRequired()
            .SeAudita();

        ubicacion.Property(fila => fila.Pasillo)
            .HasMaxLength(Ubicacion.LongitudMaximaDeCoordenada)
            .SeAudita();

        ubicacion.Property(fila => fila.Estante)
            .HasMaxLength(Ubicacion.LongitudMaximaDeCoordenada)
            .SeAudita();

        ubicacion.Property(fila => fila.Hueco)
            .HasMaxLength(Ubicacion.LongitudMaximaDeCoordenada)
            .SeAudita();

        ubicacion.Property(fila => fila.Descripcion)
            .HasMaxLength(Ubicacion.LongitudMaximaDeDescripcion)
            .SeAudita();

        ubicacion.ComplexProperty(fila => fila.Bloqueo, ConfiguracionDeBloqueo.Mapear);

        // Único DENTRO del almacén y no de la empresa: dos naves distintas pueden tener las dos
        // una estantería «A-01-3», y son sitios diferentes. La etiqueta identifica el hueco
        // dentro de su almacén, que es lo que lee quien la escanea allí de pie.
        ubicacion.HasIndex(fila => new { fila.AlmacenId, fila.Codigo }).IsUnique();

        ubicacion.HasOne<Almacen>()
            .WithMany()
            .HasForeignKey(fila => fila.AlmacenId)
            .OnDelete(DeleteBehavior.Restrict);

        // La empresa también, aunque llegue por el almacén: la columna existe para que el filtro
        // de R8 se evalúe sobre la fila, y una columna sin clave foránea admitiría un valor que
        // no corresponde a ninguna empresa. `Restrict` por lo mismo que en todas: una empresa no
        // se borra, se bloquea.
        ubicacion.HasOne<Empresa>()
            .WithMany()
            .HasForeignKey(fila => fila.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
