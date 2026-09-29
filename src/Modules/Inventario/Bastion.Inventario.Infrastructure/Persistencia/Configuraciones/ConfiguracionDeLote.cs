using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.Inventario.Domain.LotesYSeries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Inventario.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeLote : IEntityTypeConfiguration<Lote>
{
    /// <summary>La tabla, nombrada aquí porque la sentencia que resuelve los códigos la escribe en crudo.</summary>
    internal const string Tabla = "lotes";

    public void Configure(EntityTypeBuilder<Lote> lote)
    {
        ArgumentNullException.ThrowIfNull(lote);

        lote.ToTable(Tabla);

        // NO SE AUDITA: la fila no cambia nunca, y la crea en crudo la sentencia que resuelve los
        // códigos al confirmar. Lo que se sabe del lote está en el libro, que lo apunta.
        lote.NoSeAudita(
            "la crea en crudo la sentencia que resuelve los códigos, que no pasa por el rastreador " +
            "de cambios, y no cambia nunca: cuándo entró y de quién lo dice el libro, que la apunta");

        lote.HasKey(fila => fila.Id);

        lote.Property(fila => fila.EmpresaId).IsRequired();
        lote.Property(fila => fila.ArticuloId).IsRequired();

        // EL LÍMITE DE GS1 (ADR-0048 §2): lo que no cabe en el AI 10 de una etiqueta no entra.
        lote.Property(fila => fila.Codigo)
            .HasMaxLength(CodigoGs1.LargoMaximo)
            .IsRequired();

        // EL CÓDIGO SUELTO NO IDENTIFICA NADA: dos fabricantes pueden usar el mismo texto de lote.
        // El índice es también el que ordena dos primeras entradas simultáneas del mismo lote: la
        // segunda espera en él a que la primera confirme, y el `ON CONFLICT` la deja pasar sin fila.
        lote.HasIndex(fila => new { fila.EmpresaId, fila.ArticuloId, fila.Codigo }).IsUnique();
    }
}
