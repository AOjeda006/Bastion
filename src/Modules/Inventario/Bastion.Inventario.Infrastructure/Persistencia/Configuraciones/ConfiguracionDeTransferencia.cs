using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Inventario.Domain.Transferencias;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Inventario.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeTransferencia : IEntityTypeConfiguration<Transferencia>
{
    /// <summary>El índice que impide un segundo inverso, por su nombre: lo traduce el módulo.</summary>
    internal const string IndiceDelInverso = "ix_transferencias_anula_a_id";

    /// <summary>El origen nunca es el destino (ADR-0053 §8), también en la base.</summary>
    internal const string OrigenDistintoDelDestino = "ck_transferencias_origen_distinto_del_destino";

    /// <summary>La llegada nunca antes de la salida (ADR-0053 §3), también en la base.</summary>
    internal const string RecepcionNoAntesDelEnvio = "ck_transferencias_recepcion_no_antes_del_envio";

    public void Configure(EntityTypeBuilder<Transferencia> transferencia)
    {
        ArgumentNullException.ThrowIfNull(transferencia);

        // LAS DOS REGLAS DE LA CABECERA QUE CABEN EN UNA FILA, repetidas aquí aunque el dominio ya
        // las rechace: un documento que se escribiera por otro camino —una carga, un documento
        // nuevo que copie mal el patrón— no las vería de otra forma. Nunca saltan por una petición,
        // así que no traducen a ningún error: si saltan, es un defecto.
        transferencia.ToTable(
            "transferencias",
            tabla =>
            {
                tabla.HasCheckConstraint(OrigenDistintoDelDestino, "almacen_origen_id <> almacen_destino_id");
                tabla.HasCheckConstraint(
                    RecepcionNoAntesDelEnvio, "fecha_de_recepcion IS NULL OR fecha_de_recepcion >= fecha_de_envio");
            });

        transferencia.HasKey(documento => documento.Id);

        // SE AUDITA Y LLEVA TESTIGO, como el ajuste, y el testigo pesa aquí más: la transferencia
        // cambia DOS veces. Recibir y anular a la vez leen las dos `Enviada`, y sin testigo las dos
        // escribirían su tanda; con él, la segunda choca y recibe el `412` (ADR-0053 §10).
        transferencia.SeAudita();
        transferencia.LlevaTestigoDeConcurrencia();

        ConfiguracionDeEntidadBase.Mapear(transferencia);

        transferencia.Property(documento => documento.EmpresaId).IsRequired().SeAudita();

        // SIN CLAVE AJENA a `organizacion`, como los del ajuste: ninguna cruza de esquema.
        transferencia.Property(documento => documento.AlmacenOrigenId).IsRequired().SeAudita();
        transferencia.Property(documento => documento.AlmacenDestinoId).IsRequired().SeAudita();
        transferencia.Property(documento => documento.SerieId).IsRequired().SeAudita();
        transferencia.Property(documento => documento.Numero).SeAudita();

        // LAS DOS FECHAS DE NEGOCIO (R14), cada una la de su pata: la de envío decide la partición
        // de la salida, y la de recepción la de la entrada (ADR-0053 §3).
        transferencia.Property(documento => documento.FechaDeEnvio).IsRequired().SeAudita();
        transferencia.Property(documento => documento.FechaDeRecepcion).SeAudita();

        transferencia.Property(documento => documento.Motivo)
            .HasMaxLength(Transferencia.LargoDelMotivo)
            .SeAudita();

        transferencia.Property(documento => documento.Divisa)
            .HasMaxLength(3)
            .IsRequired()
            .SeAudita();

        transferencia.Property(documento => documento.Estado)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .SeAudita();

        transferencia.HasMany(documento => documento.Lineas)
            .WithOne()
            .HasForeignKey(linea => linea.TransferenciaId)
            .OnDelete(DeleteBehavior.Cascade);

        transferencia.Navigation(documento => documento.Lineas).AutoInclude();

        // EL ENLACE DEL PAR, como en el ajuste: una sola columna en el inverso, clave ajena a la
        // misma tabla con `Restrict`, e índice único filtrado que impide un segundo inverso.
        transferencia.Property(documento => documento.AnulaAId).SeAudita();

        transferencia.HasOne<Transferencia>()
            .WithMany()
            .HasForeignKey(documento => documento.AnulaAId)
            .OnDelete(DeleteBehavior.Restrict);

        transferencia.HasIndex(documento => documento.AnulaAId)
            .IsUnique()
            .HasFilter("anula_a_id IS NOT NULL")
            .HasDatabaseName(IndiceDelInverso);

        // LO QUE PREGUNTA EL CIERRE (ADR-0053 §12): un documento cuenta por cualquiera de sus dos
        // fechas, así que cada una lleva su índice.
        transferencia.HasIndex(documento => new { documento.EmpresaId, documento.FechaDeEnvio });
        transferencia.HasIndex(documento => new { documento.EmpresaId, documento.FechaDeRecepcion });

        // La mitad de la R5 que cabe en una tabla, como en el ajuste.
        transferencia.HasIndex(documento => new { documento.SerieId, documento.Numero })
            .IsUnique()
            .HasFilter("numero IS NOT NULL");
    }
}
