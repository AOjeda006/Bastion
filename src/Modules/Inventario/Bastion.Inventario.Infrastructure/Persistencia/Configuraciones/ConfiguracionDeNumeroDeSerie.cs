using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.Inventario.Domain.LotesYSeries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Inventario.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeNumeroDeSerie : IEntityTypeConfiguration<NumeroDeSerie>
{
    /// <summary>La tabla, nombrada aquí porque la sentencia que resuelve los códigos la escribe en crudo.</summary>
    internal const string Tabla = "numeros_de_serie";

    public void Configure(EntityTypeBuilder<NumeroDeSerie> serie)
    {
        ArgumentNullException.ThrowIfNull(serie);

        serie.ToTable(Tabla);

        // NO SE AUDITA, por lo mismo que el lote.
        serie.NoSeAudita(
            "la crea en crudo la sentencia que resuelve los códigos, que no pasa por el rastreador " +
            "de cambios, y no cambia nunca: dónde estuvo la unidad lo dice el libro, que la apunta");

        serie.HasKey(fila => fila.Id);

        serie.Property(fila => fila.EmpresaId).IsRequired();
        serie.Property(fila => fila.ArticuloId).IsRequired();

        // EL LÍMITE DE GS1 (ADR-0048 §2), el del AI 21.
        serie.Property(fila => fila.Numero)
            .HasMaxLength(CodigoGs1.LargoMaximo)
            .IsRequired();

        // Como el lote: el número es de un artículo, y dos fabricantes pueden repetirlo.
        serie.HasIndex(fila => new { fila.EmpresaId, fila.ArticuloId, fila.Numero }).IsUnique();
    }
}
