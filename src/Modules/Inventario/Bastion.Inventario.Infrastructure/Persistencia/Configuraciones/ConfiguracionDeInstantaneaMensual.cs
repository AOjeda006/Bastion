using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.Inventario.Domain.Existencias;
using Bastion.Inventario.Domain.Movimientos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Inventario.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeInstantaneaMensual : IEntityTypeConfiguration<InstantaneaMensual>
{
    /// <summary>La tabla, nombrada aquí porque la escriben en crudo el libro y el recálculo.</summary>
    internal const string Tabla = "instantaneas_mensuales";

    /// <summary>El mes es siempre un primer día de mes, que es el límite de la partición del libro.</summary>
    internal const string PrimerDiaDelMes = "extract(day from mes) = 1";

    public void Configure(EntityTypeBuilder<InstantaneaMensual> instantanea)
    {
        ArgumentNullException.ThrowIfNull(instantanea);

        instantanea.ToTable(
            Tabla,
            tabla => tabla.HasCheckConstraint(
                "ck_instantaneas_mensuales_mes_es_primer_dia", PrimerDiaDelMes));

        // NO SE AUDITA: es una copia que se puede tirar y recalcular sin que cambie un número, y la
        // escriben en crudo la sentencia del libro y el recálculo. La traza de lo que la mueve es
        // el libro.
        instantanea.NoSeAudita(
            "es una optimización que se puede borrar y recalcular desde el libro, y la escriben en " +
            "crudo la sentencia que anota el libro y el recálculo, que no pasan por el rastreador");

        // LA CLAVE ES (existencia, mes), y colgar de la existencia no es comodidad: la clave de negocio
        // lleva el lote, que puede ser nulo, y una clave primaria no admite nulos. La existencia ya
        // resuelve eso con su índice, así que aquí basta con su identificador.
        instantanea.HasKey(fila => new { fila.ExistenciaId, fila.Mes });

        instantanea.HasOne<Existencia>()
            .WithMany()
            .HasForeignKey(fila => fila.ExistenciaId)
            .OnDelete(DeleteBehavior.Restrict);

        instantanea.Property(fila => fila.EmpresaId).IsRequired();

        instantanea.Property(fila => fila.Fisico)
            .HasPrecision(18, MovimientoStock.DecimalesDeCantidad)
            .IsRequired();

        // El recálculo borra por empresa y el cuadre lee por empresa.
        instantanea.HasIndex(fila => fila.EmpresaId);
    }
}
