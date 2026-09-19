using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Movimientos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Inventario.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeLineaDeAjuste : IEntityTypeConfiguration<LineaDeAjuste>
{
    /// <summary>Ni la cantidad ni el factor pueden dejar la línea sin efecto.</summary>
    /// <remarks>
    /// Son las mismas dos condiciones que protegen el libro, escritas también aquí: una línea que
    /// el documento acepta y el libro rechaza dejaría un ajuste imposible de confirmar, con el
    /// fallo apareciendo en la transición y no donde se escribió el dato.
    /// </remarks>
    internal const string CantidadYFactor =
        "cantidad_introducida <> 0 AND factor_a_unidad_base > 0";

    public void Configure(EntityTypeBuilder<LineaDeAjuste> linea)
    {
        ArgumentNullException.ThrowIfNull(linea);

        linea.ToTable(
            "lineas_ajuste",
            tabla => tabla.HasCheckConstraint("ck_lineas_ajuste_cantidad_y_factor", CantidadYFactor));

        linea.HasKey(fila => fila.Id);

        // SE AUDITA, como su documento: qué se decidió mover, de qué artículo y en qué estantería
        // es justamente el contenido de la decisión que el ajuste registra.
        linea.SeAudita();

        // Y SIN TESTIGO, a propósito, que es la decisión contraria a la de la línea de tarifa y la
        // misma que la de los tres hijos del tercero. Una línea no es un recurso que se edite por
        // su cuenta: no tiene ruta propia, solo existe mientras el ajuste está en borrador y lo
        // que gobierna su edición es el testigo del AJUSTE. Un testigo por línea dejaría pasar
        // justo el caso que hay que detectar —dos personas tocando el mismo borrador, una
        // añadiendo una línea y otra confirmándolo—.
        ConfiguracionDeEntidadBase.Mapear(linea);

        linea.Property(fila => fila.AjusteId).IsRequired().SeAudita();
        linea.Property(fila => fila.UbicacionId).IsRequired().SeAudita();
        linea.Property(fila => fila.ArticuloId).IsRequired().SeAudita();
        linea.Property(fila => fila.UnidadIntroducidaId).IsRequired().SeAudita();

        linea.Property(fila => fila.CantidadIntroducida)
            .HasPrecision(18, MovimientoStock.DecimalesDeCantidad)
            .IsRequired()
            .SeAudita();

        linea.Property(fila => fila.FactorAUnidadBase)
            .HasPrecision(18, MovimientoStock.DecimalesDeCantidad)
            .IsRequired()
            .SeAudita();

        linea.ComplexProperty(fila => fila.CosteUnitario, coste =>
        {
            coste.IsRequired();

            coste.Property(campo => campo.Cantidad)
                .HasColumnName("coste_unitario_cantidad")
                .HasPrecision(18, Importe.Decimales)
                .SeAudita();

            coste.Property(campo => campo.Divisa)
                .HasColumnName("coste_unitario_divisa")
                .HasMaxLength(3)
                .SeAudita();
        });

        linea.HasIndex(fila => fila.AjusteId);
    }
}
