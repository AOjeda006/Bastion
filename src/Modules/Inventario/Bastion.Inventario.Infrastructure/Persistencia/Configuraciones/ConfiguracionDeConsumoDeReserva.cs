using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Reservas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Inventario.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeConsumoDeReserva : IEntityTypeConfiguration<ConsumoDeReserva>
{
    /// <summary>Un documento consume una reserva una sola vez (ADR-0059 §12).</summary>
    internal const string UnoPorDocumento = "ix_consumos_de_reserva_uno_por_documento";

    internal const string CantidadPositiva = "ck_consumos_de_reserva_cantidad_positiva";

    public void Configure(EntityTypeBuilder<ConsumoDeReserva> consumo)
    {
        ArgumentNullException.ThrowIfNull(consumo);

        consumo.ToTable(
            "consumos_de_reserva",
            tabla => tabla.HasCheckConstraint(CantidadPositiva, "cantidad > 0"));

        consumo.HasKey(fila => fila.Id);

        // SE AUDITA, como la línea del ajuste: qué documento sacó cuánto de qué reserva es el
        // contenido del consumo. Y SIN TESTIGO, porque nace al consumir y no cambia nunca: un
        // testigo sobre una fila que nadie escribe dos veces no protege de nada (ADR-0059 §10).
        consumo.SeAudita();

        ConfiguracionDeEntidadBase.Mapear(consumo);

        consumo.Property(fila => fila.ReservaId).IsRequired().SeAudita();

        consumo.Property(fila => fila.DocumentoTipo)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .SeAudita();

        // SIN CLAVE AJENA: el albarán vive en Ventas. Es la otra punta de la doble flecha de sus
        // filas del libro, que se cierra contra esta columna (ADR-0059 §8).
        consumo.Property(fila => fila.DocumentoId).IsRequired().SeAudita();

        // `date`: es el día de la salida, el de sus filas del libro (R14).
        consumo.Property(fila => fila.FechaDeOperacion).IsRequired().SeAudita();

        consumo.Property(fila => fila.Cantidad)
            .HasPrecision(18, MovimientoStock.DecimalesDeCantidad)
            .IsRequired()
            .SeAudita();

        // EL MISMO DOCUMENTO NO CONSUME DOS VECES LA MISMA RESERVA, y es lo que respalda el `409`
        // que el dominio ya da: el índice queda en pie para lo que se escriba por otro camino. Sirve
        // también a la clave ajena, que empieza por la misma columna.
        consumo.HasIndex(fila => new { fila.ReservaId, fila.DocumentoTipo, fila.DocumentoId })
            .IsUnique()
            .HasDatabaseName(UnoPorDocumento);

        // LA IDA DE LA DOBLE FLECHA busca los consumos del documento de cada fila del libro, y los
        // une a su reserva, que es la que lleva la empresa (ADR-0059 §8).
        consumo.HasIndex(fila => new { fila.DocumentoTipo, fila.DocumentoId });
    }
}
