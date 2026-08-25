using Bastion.Organizacion.Domain.Almacenes;
using Bastion.Organizacion.Domain.Empresas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeAlmacen : IEntityTypeConfiguration<Almacen>
{
    public void Configure(EntityTypeBuilder<Almacen> almacen)
    {
        ArgumentNullException.ThrowIfNull(almacen);

        almacen.ToTable("almacenes");
        almacen.HasKey(fila => fila.Id);

        almacen.Property(fila => fila.EmpresaId).IsRequired();

        almacen.Property(fila => fila.Codigo)
            .HasMaxLength(Almacen.LongitudMaximaDeCodigo)
            .IsRequired();

        almacen.Property(fila => fila.Nombre).IsRequired();

        almacen.Property(fila => fila.Tipo)
            .HasConversion<string>()
            .IsRequired();

        almacen.Property(fila => fila.Estado)
            .HasConversion<string>()
            .IsRequired();

        almacen.Property(fila => fila.BloqueadoEn);

        // Dirección OPCIONAL: un almacén virtual o de tránsito no está en ningún sitio, y
        // exigirle una dirección obligaría a inventarla. Las seis columnas quedan anulables.
        almacen.OwnsOne(fila => fila.Direccion, ConfiguracionDeDireccion.Mapear);
        almacen.Navigation(fila => fila.Direccion).IsRequired(false);

        almacen.HasIndex(fila => new { fila.EmpresaId, fila.Codigo }).IsUnique();

        almacen.HasOne<Empresa>()
            .WithMany()
            .HasForeignKey(fila => fila.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
