using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Recuentos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Inventario.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeLineaDeRecuento : IEntityTypeConfiguration<LineaDeRecuento>
{
    /// <summary>Una línea por clave en cada recuento (ADR-0055 §13), por su nombre: lo traduce el módulo.</summary>
    internal const string UnaPorClave = "ix_lineas_recuento_una_por_clave";

    /// <summary>Un número de serie una sola vez en cada recuento, esté en la ubicación que esté.</summary>
    internal const string UnaPorSerie = "ix_lineas_recuento_una_por_serie";

    public void Configure(EntityTypeBuilder<LineaDeRecuento> linea)
    {
        ArgumentNullException.ThrowIfNull(linea);

        // LO QUE EL DOMINIO YA RECHAZA, escrito también en la base, como en las líneas del ajuste:
        // una línea que el documento acepta y la base no dejaría el fallo lejos de donde se escribió.
        // Ninguna salta por una petición, así que no traducen a ningún error.
        //
        // - Lo contado y su teórico van juntos: contar anota los dos (ADR-0055 §2).
        // - Sin contar no es cero, y contado no es negativo (§5).
        // - Un lote o un número de serie, nunca los dos (ADR-0048 §1).
        // - Un número de serie se cuenta en cero o en uno (§6).
        // - El coste solo lo lleva una clave añadida (§6). El origen se lee por su texto.
        linea.ToTable(
            "lineas_recuento",
            tabla =>
            {
                tabla.HasCheckConstraint("ck_lineas_recuento_numero_desde_uno", "numero > 0");
                tabla.HasCheckConstraint(
                    "ck_lineas_recuento_contado_con_su_teorico",
                    "(contado IS NULL) = (teorico_al_contar IS NULL)");
                tabla.HasCheckConstraint(
                    "ck_lineas_recuento_contado_no_negativo", "contado IS NULL OR contado >= 0");
                tabla.HasCheckConstraint(
                    "ck_lineas_recuento_lote_o_serie", "codigo_de_lote IS NULL OR numero_de_serie IS NULL");
                tabla.HasCheckConstraint(
                    "ck_lineas_recuento_serie_en_cero_o_uno",
                    "numero_de_serie IS NULL OR contado IS NULL OR contado IN (0, 1)");
                tabla.HasCheckConstraint(
                    "ck_lineas_recuento_coste_solo_al_anadir",
                    "coste_unitario IS NULL OR (origen = 'Anadida' AND coste_unitario >= 0)");
            });

        linea.HasKey(fila => fila.Id);

        // SE AUDITA, como su documento: lo contado es la decisión que el recuento registra.
        linea.SeAudita();

        // Y LLEVA TESTIGO, que es lo contrario de las líneas del ajuste y de la transferencia
        // (ADR-0055 §4). Aquí una línea SÍ se escribe sola, por su ruta y con su `If-Match`: dos
        // personas que cuentan dos estanterías del mismo almacén no se están pisando, y con un solo
        // testigo en la cabecera la segunda se llevaría un `412` por contar otra fila. Es el
        // argumento de la línea de tarifa. La cabecera sigue resumiendo el documento, porque toda
        // escritura en una línea la toca.
        linea.LlevaTestigoDeConcurrencia();

        ConfiguracionDeEntidadBase.Mapear(linea);

        linea.Property(fila => fila.RecuentoId).IsRequired().SeAudita();

        // EL ORDEN DE LA FICHA, que la precarga reparte por ubicación, artículo, lote y serie
        // (ADR-0055 §13). Único dentro del recuento, y el índice sirve también a la clave ajena.
        linea.Property(fila => fila.Numero).IsRequired().SeAudita();
        linea.Property(fila => fila.UbicacionId).IsRequired().SeAudita();
        linea.Property(fila => fila.ArticuloId).IsRequired().SeAudita();
        linea.Property(fila => fila.UnidadBaseId).IsRequired().SeAudita();

        linea.Property(fila => fila.CodigoDeLote)
            .HasMaxLength(CodigoGs1.LargoMaximo)
            .SeAudita();

        linea.Property(fila => fila.NumeroDeSerie)
            .HasMaxLength(CodigoGs1.LargoMaximo)
            .SeAudita();

        linea.Property(fila => fila.Origen)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .SeAudita();

        // EN LA UNIDAD BASE, con los decimales del libro: lo que difiere es la cantidad de la línea
        // del ajuste, que entra en el libro tal cual, con factor 1.
        linea.Property(fila => fila.Contado)
            .HasPrecision(18, MovimientoStock.DecimalesDeCantidad)
            .SeAudita();

        linea.Property(fila => fila.TeoricoAlContar)
            .HasPrecision(18, MovimientoStock.DecimalesDeCantidad)
            .SeAudita();

        // SIN DIVISA PROPIA: la pone la cabecera, como en el ajuste.
        linea.Property(fila => fila.CosteUnitario)
            .HasPrecision(18, Importe.Decimales)
            .SeAudita();

        // LA MITAD DE LA DOBLE FLECHA QUE VIVE AQUÍ (ADR-0055 §8): la línea del ajuste que movió su
        // diferencia. Clave ajena sí, porque es la misma tabla del mismo esquema; `Restrict`, porque
        // borrar la línea del ajuste no puede dejar al recuento diciendo que movió algo que ya no
        // está. La otra mitad es `ajustes.recuento_id`, y entre las dos no hay ciclo: el recuento no
        // apunta a su ajuste.
        linea.Property(fila => fila.LineaDeAjusteId).SeAudita();

        linea.HasOne<LineaDeAjuste>()
            .WithMany()
            .HasForeignKey(fila => fila.LineaDeAjusteId)
            .OnDelete(DeleteBehavior.Restrict);

        // Una línea del ajuste la mueve una sola línea del recuento. Filtrado, porque las que no
        // difieren no apuntan a ninguna.
        linea.HasIndex(fila => fila.LineaDeAjusteId)
            .IsUnique()
            .HasFilter("linea_de_ajuste_id IS NOT NULL");

        linea.HasIndex(fila => new { fila.RecuentoId, fila.Numero }).IsUnique();

        // UNA LÍNEA POR CLAVE (ADR-0055 §13), con los nulos IGUALES: una clave sin lote ni serie es
        // la misma clave las dos veces, y con los nulos distintos —lo que PostgreSQL hace si no se
        // le dice— el índice no vería nunca la que más se repite. El dominio ya lo mira; esto es lo
        // que queda en pie cuando dos personas añaden la misma clave a la vez.
        linea.HasIndex(fila => new
        {
            fila.RecuentoId,
            fila.ArticuloId,
            fila.UbicacionId,
            fila.CodigoDeLote,
            fila.NumeroDeSerie,
        })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName(UnaPorClave);

        // Y UN NÚMERO DE SERIE UNA SOLA VEZ, esté donde esté: una unidad no está en dos estanterías,
        // y contarla en las dos la sumaría dos veces.
        linea.HasIndex(fila => new { fila.RecuentoId, fila.ArticuloId, fila.NumeroDeSerie })
            .IsUnique()
            .HasFilter("numero_de_serie IS NOT NULL")
            .HasDatabaseName(UnaPorSerie);
    }
}
