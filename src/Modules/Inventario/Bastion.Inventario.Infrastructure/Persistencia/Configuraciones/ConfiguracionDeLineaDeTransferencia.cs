using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Transferencias;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Inventario.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeLineaDeTransferencia : IEntityTypeConfiguration<LineaDeTransferencia>
{
    /// <summary>Las mismas dos condiciones que la línea del ajuste, por la misma razón.</summary>
    internal const string CantidadYFactor = ConfiguracionDeLineaDeAjuste.CantidadYFactor;

    internal const string NumeroDesdeUno = ConfiguracionDeLineaDeAjuste.NumeroDesdeUno;

    public void Configure(EntityTypeBuilder<LineaDeTransferencia> linea)
    {
        ArgumentNullException.ThrowIfNull(linea);

        linea.ToTable(
            "lineas_transferencia",
            tabla =>
            {
                tabla.HasCheckConstraint("ck_lineas_transferencia_cantidad_y_factor", CantidadYFactor);
                tabla.HasCheckConstraint("ck_lineas_transferencia_numero_desde_uno", NumeroDesdeUno);
            });

        linea.HasKey(fila => fila.Id);

        // SE AUDITA Y SIN TESTIGO, como la línea del ajuste: lo que gobierna su edición es el
        // testigo de la TRANSFERENCIA.
        linea.SeAudita();

        ConfiguracionDeEntidadBase.Mapear(linea);

        linea.Property(fila => fila.TransferenciaId).IsRequired().SeAudita();
        linea.Property(fila => fila.Numero).IsRequired().SeAudita();

        // LAS DOS UBICACIONES, una por pata. La del destino va desde el alta (ADR-0053 §1): el
        // tránsito vive en la existencia del destino y la necesita desde el envío.
        linea.Property(fila => fila.UbicacionOrigenId).IsRequired().SeAudita();
        linea.Property(fila => fila.UbicacionDestinoId).IsRequired().SeAudita();
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

        // EL VALOR QUE VIAJA Y EL QUE COMPENSA (ADR-0053 §2 y §6), en la divisa de la cabecera.
        // Nulos mientras no aplican: el primero hasta enviar, el segundo fuera de un inverso. Se
        // auditan porque son lo que une las dos patas: si cambiaran, la recepción metería en el
        // destino otro valor que el que salió del origen.
        linea.Property(fila => fila.Valor)
            .HasPrecision(18, Importe.Decimales)
            .SeAudita();

        linea.Property(fila => fila.ValorQueCompensa)
            .HasPrecision(18, Importe.Decimales)
            .SeAudita();

        // EL CÓDIGO QUE ESCRIBIÓ EL USUARIO, como en el ajuste (ADR-0048 §2): el borrador no crea
        // lotes ni series.
        linea.Property(fila => fila.CodigoDeLote)
            .HasMaxLength(CodigoGs1.LargoMaximo)
            .SeAudita();

        linea.Property(fila => fila.NumeroDeSerie)
            .HasMaxLength(CodigoGs1.LargoMaximo)
            .SeAudita();

        linea.HasIndex(fila => new { fila.TransferenciaId, fila.Numero }).IsUnique();
    }
}
