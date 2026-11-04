using Bastion.BuildingBlocks.Domain.Fechas;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Bloqueos;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.BuildingBlocks.Infrastructure.Direcciones;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Organizacion.Domain.Almacenes;
using Bastion.Organizacion.Domain.Empresas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeAlmacen : IEntityTypeConfiguration<Almacen>
{
    /// <summary>La zona del almacén, si tiene una, es una de las admitidas.</summary>
    public const string ZonaAdmitida = "ck_almacenes_zona_horaria";

    public void Configure(EntityTypeBuilder<Almacen> almacen)
    {
        ArgumentNullException.ThrowIfNull(almacen);

        almacen.ToTable("almacenes", tabla => tabla.HasCheckConstraint(
            ZonaAdmitida, ConfiguracionDeZona.Columna + " IS NULL OR " + ConfiguracionDeZona.Admitidas()));

        // Maestro: donde esta el stock. Su alta, su codigo y su bloqueo dejan traza.
        almacen.SeAudita();
        almacen.HasKey(fila => fila.Id);

        almacen.LlevaTestigoDeConcurrencia();

        almacen.Property(fila => fila.EmpresaId).IsRequired().SeAudita();

        almacen.Property(fila => fila.Codigo)
            .HasMaxLength(Almacen.LongitudMaximaDeCodigo)
            .IsRequired()
            .SeAudita();

        almacen.Property(fila => fila.Nombre).IsRequired().SeAudita();

        almacen.Property(fila => fila.Tipo)
            .HasConversion<string>()
            .IsRequired()
            .SeAudita();

        // Anulable, y el nulo significa algo: el almacén hereda la zona de su empresa, en vivo.
        PropertyBuilder<ZonaHoraria?> zona = almacen.Property(fila => fila.ZonaHoraria);
        zona.HasConversion(valor => valor!.Iana, iana => ZonaHoraria.De(iana))
            .IsRequired(false)
            .SeAudita();

        ConfiguracionDeEntidadBase.Mapear(almacen);

        almacen.ComplexProperty(fila => fila.Bloqueo, ConfiguracionDeBloqueo.Mapear);

        // Dirección OPCIONAL: un almacén virtual o de tránsito no está en ningún sitio, y
        // exigirle una dirección obligaría a inventarla. Las seis columnas quedan anulables.
        almacen.ComplexProperty(fila => fila.Direccion, direccion =>
        {
            direccion.IsRequired(false);
            ConfiguracionDeDireccion.Mapear(direccion);
        });

        almacen.HasIndex(fila => new { fila.EmpresaId, fila.Codigo }).IsUnique();

        almacen.HasOne<Empresa>()
            .WithMany()
            .HasForeignKey(fila => fila.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
