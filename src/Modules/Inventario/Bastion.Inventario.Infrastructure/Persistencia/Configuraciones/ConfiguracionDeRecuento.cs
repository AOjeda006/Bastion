using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Inventario.Domain.Recuentos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Inventario.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeRecuento : IEntityTypeConfiguration<Recuento>
{
    /// <summary>Uno en curso por almacén (ADR-0055 §1.7), por su nombre: lo traduce el módulo.</summary>
    internal const string UnoEnCursoPorAlmacen = "ix_recuentos_uno_en_curso_por_almacen";

    /// <summary>Número y fecha de confirmación, juntos y solo en los que se confirmaron.</summary>
    internal const string NumeradoSiSeConfirmo = "ck_recuentos_numerado_si_se_confirmo";

    /// <summary>La confirmación nunca antes de la apertura, también en la base.</summary>
    internal const string ConfirmacionNoAntesDeLaApertura = "ck_recuentos_confirmacion_no_antes_de_la_apertura";

    public void Configure(EntityTypeBuilder<Recuento> recuento)
    {
        ArgumentNullException.ThrowIfNull(recuento);

        // LAS DOS REGLAS DE LA CABECERA QUE CABEN EN UNA FILA, como en la transferencia: el dominio
        // ya las rechaza, y esto es para lo que se escriba por otro camino. Nunca saltan por una
        // petición, así que no traducen a ningún error: si saltan, es un defecto. La primera lee
        // el estado por su texto, que es como se guarda.
        recuento.ToTable(
            "recuentos",
            tabla =>
            {
                tabla.HasCheckConstraint(
                    NumeradoSiSeConfirmo,
                    "(numero IS NOT NULL AND fecha_de_confirmacion IS NOT NULL) = " +
                    "(estado IN ('Confirmado', 'Anulado'))");
                tabla.HasCheckConstraint(
                    ConfirmacionNoAntesDeLaApertura,
                    "fecha_de_confirmacion IS NULL OR fecha_de_confirmacion >= fecha_de_apertura");
            });

        recuento.HasKey(documento => documento.Id);

        // SE AUDITA Y LLEVA TESTIGO, como el ajuste y la transferencia. Aquí el testigo hace dos
        // trabajos: el de siempre —confirmar, anular y descartar leen el estado y luego escriben— y
        // el de resumir el documento entero, porque toda escritura en una línea lo toca. La versión
        // que exige la confirmación dice que nadie ha contado, añadido ni quitado nada desde que el
        // usuario la leyó (ADR-0055 §4).
        recuento.SeAudita();
        recuento.LlevaTestigoDeConcurrencia();

        ConfiguracionDeEntidadBase.Mapear(recuento);

        recuento.Property(documento => documento.EmpresaId).IsRequired().SeAudita();

        // SIN CLAVE AJENA a `organizacion`, como en los otros dos documentos: ninguna cruza de
        // esquema. Las dos series las comprueba el `WHERE` del numerador al confirmar.
        recuento.Property(documento => documento.AlmacenId).IsRequired().SeAudita();
        recuento.Property(documento => documento.SerieId).IsRequired().SeAudita();
        recuento.Property(documento => documento.SerieDelAjusteId).IsRequired().SeAudita();

        // NULO HASTA CONFIRMAR, porque se numera al confirmar (ADR-0055 §1.4).
        recuento.Property(documento => documento.Numero).SeAudita();

        // LAS DOS FECHAS DE NEGOCIO (R14). La que cuenta para el ejercicio es la de confirmación:
        // uno en curso no cuenta, y el ajuste que genera lleva esa misma (ADR-0055 §1.5).
        recuento.Property(documento => documento.FechaDeApertura).IsRequired().SeAudita();
        recuento.Property(documento => documento.FechaDeConfirmacion).SeAudita();

        recuento.Property(documento => documento.Motivo)
            .HasMaxLength(Recuento.LargoDelMotivo)
            .IsRequired()
            .SeAudita();

        recuento.Property(documento => documento.Divisa)
            .HasMaxLength(3)
            .IsRequired()
            .SeAudita();

        recuento.Property(documento => documento.MotivoDelDescarte)
            .HasMaxLength(Recuento.LargoDelMotivo)
            .SeAudita();

        recuento.Property(documento => documento.MotivoDeLaAnulacion)
            .HasMaxLength(Recuento.LargoDelMotivo)
            .SeAudita();

        recuento.Property(documento => documento.Estado)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .SeAudita();

        recuento.HasMany(documento => documento.Lineas)
            .WithOne()
            .HasForeignKey(linea => linea.RecuentoId)
            .OnDelete(DeleteBehavior.Cascade);

        recuento.Navigation(documento => documento.Lineas).AutoInclude();

        // UNO EN CURSO POR ALMACÉN (ADR-0055 §1.7). Mirar antes de abrir deja la ventana de siempre,
        // y dos altas a la vez la cruzarían juntas; el índice único parcial espera a la transacción
        // que tenga la otra fila y rechaza la segunda. Lo traduce el módulo a un `409` por su nombre.
        // El filtro es la mitad de la regla: sin él, un almacén solo se podría contar una vez en la
        // vida.
        recuento.HasIndex(documento => new { documento.EmpresaId, documento.AlmacenId })
            .IsUnique()
            .HasFilter("estado = 'EnCurso'")
            .HasDatabaseName(UnoEnCursoPorAlmacen);

        // LO QUE PREGUNTA EL CIERRE: un recuento cuenta por su fecha de confirmación (ADR-0055 §13).
        recuento.HasIndex(documento => new { documento.EmpresaId, documento.FechaDeConfirmacion });

        // La mitad de la R5 que cabe en una tabla, como en los otros dos documentos.
        recuento.HasIndex(documento => new { documento.SerieId, documento.Numero })
            .IsUnique()
            .HasFilter("numero IS NOT NULL");
    }
}
