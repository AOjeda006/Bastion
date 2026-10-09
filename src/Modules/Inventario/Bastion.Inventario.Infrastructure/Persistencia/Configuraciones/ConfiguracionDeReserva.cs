using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Reservas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Inventario.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeReserva : IEntityTypeConfiguration<Reserva>
{
    /// <summary>Una reserva por línea de origen, en todos sus estados (ADR-0059 §1.6).</summary>
    internal const string UnaPorOrigen = "ix_reservas_una_por_origen";

    /// <summary>El índice que lee lo reservado de una clave (ADR-0059 §3).</summary>
    internal const string ActivasPorClave = "ix_reservas_activas_por_clave";

    /// <summary>El nombre de la propiedad del estado guardado, que no es pública (ADR-0059 §4).</summary>
    internal const string Estado = "Estado";

    internal const string CantidadPositiva = "ck_reservas_cantidad_positiva";

    internal const string LineaDesdeUno = "ck_reservas_origen_linea_desde_uno";

    internal const string LiberadaConCausaYFecha = "ck_reservas_liberada_con_causa_y_fecha";

    internal const string MotivoSoloAMano = "ck_reservas_motivo_solo_a_mano";

    internal const string CaducidadEnSuFecha = "ck_reservas_caducidad_en_su_fecha";

    public void Configure(EntityTypeBuilder<Reserva> reserva)
    {
        ArgumentNullException.ThrowIfNull(reserva);

        // LAS REGLAS DE LA FILA QUE EL DOMINIO YA RESPETA (ADR-0059 §12), para lo que se escriba por
        // otro camino. Nunca saltan por una petición, así que no traducen a ningún error: si saltan,
        // es un defecto. Las comparaciones van envueltas en `COALESCE` porque una restricción que da
        // NULL se cumple, y una fecha nula no puede abrir ese hueco.
        reserva.ToTable(
            "reservas",
            tabla =>
            {
                tabla.HasCheckConstraint(CantidadPositiva, "cantidad > 0");
                tabla.HasCheckConstraint(LineaDesdeUno, "origen_linea > 0");
                tabla.HasCheckConstraint(
                    LiberadaConCausaYFecha,
                    "(estado = 'Liberada') = (causa IS NOT NULL) AND " +
                    "(estado = 'Liberada') = (liberada_el IS NOT NULL)");
                tabla.HasCheckConstraint(
                    MotivoSoloAMano, "(motivo IS NOT NULL) = COALESCE(causa = 'AMano', FALSE)");
                tabla.HasCheckConstraint(
                    CaducidadEnSuFecha,
                    "causa IS DISTINCT FROM 'Caducidad' OR COALESCE(liberada_el = caduca_el, FALSE)");
            });

        reserva.HasKey(fila => fila.Id);

        // SE AUDITA Y LLEVA TESTIGO, como cualquier cosa que se modifica (ADR-0059 §10), aunque
        // toda escritura sobre una reserva tome antes la valoración de su clave. El testigo es el
        // seguro del día que alguien la escriba sin ese cerrojo.
        reserva.SeAudita();
        reserva.LlevaTestigoDeConcurrencia();

        ConfiguracionDeEntidadBase.Mapear(reserva);

        reserva.Property(fila => fila.EmpresaId).IsRequired().SeAudita();

        // EL ORIGEN, como texto lo que es y sin clave ajena lo que apunta: la línea del pedido vive
        // en Ventas, y ninguna clave ajena cruza de esquema (§5, regla 4).
        reserva.Property(fila => fila.OrigenTipo)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .SeAudita();

        reserva.Property(fila => fila.OrigenId).IsRequired().SeAudita();
        reserva.Property(fila => fila.OrigenLinea).IsRequired().SeAudita();

        // SIN CLAVE AJENA al artículo, al almacén ni a la unidad, por lo mismo: viven en Catálogo
        // y en Organización. La unidad base es la que el artículo tenía al reservar (ADR-0059 §12).
        reserva.Property(fila => fila.ArticuloId).IsRequired().SeAudita();
        reserva.Property(fila => fila.AlmacenId).IsRequired().SeAudita();
        reserva.Property(fila => fila.UnidadBaseId).IsRequired().SeAudita();

        reserva.Property(fila => fila.Cantidad)
            .HasPrecision(18, MovimientoStock.DecimalesDeCantidad)
            .IsRequired()
            .SeAudita();

        // `timestamptz` y no `date`: la caducidad es un INSTANTE (ADR-0059 §1.3), y la liberación
        // también. No son fechas de negocio de la R14, porque una reserva no toca el libro.
        reserva.Property(fila => fila.CaducaEl).SeAudita();

        // EL ESTADO GUARDADO, por su nombre: no es público, porque nadie lee «Activa» a secas. Lo
        // lee `EstadoEn`, que aplica el reloj (ADR-0059 §4). Como texto, igual que los demás.
        reserva.Property<EstadoDeReserva>(Estado)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .SeAudita();

        reserva.Property(fila => fila.Causa)
            .HasConversion<string>()
            .HasMaxLength(20)
            .SeAudita();

        reserva.Property(fila => fila.Motivo)
            .HasMaxLength(Reserva.LargoDelMotivo)
            .SeAudita();

        reserva.Property(fila => fila.LiberadaEl).SeAudita();

        // LOS CONSUMOS SE CARGAN CON LA RESERVA, que es su agregado: lo pendiente es la cantidad
        // menos su suma, y sin ellos la reserva diría que no se ha consumido nada.
        reserva.HasMany(fila => fila.Consumos)
            .WithOne()
            .HasForeignKey(consumo => consumo.ReservaId)
            .OnDelete(DeleteBehavior.Cascade);

        reserva.Navigation(fila => fila.Consumos).AutoInclude();

        // UNA POR ORIGEN, EN TODOS LOS ESTADOS, y por eso sin filtro (ADR-0059 §1.6). Es lo que
        // sostiene la idempotencia: la segunda petición del mismo origen encuentra la primera. Si
        // las dos van a claves distintas, no comparten cerrojo, y la segunda revienta aquí al
        // confirmar la primera, como excepción y no como resultado (ADR-0059 §5).
        reserva.HasIndex(fila => new { fila.EmpresaId, fila.OrigenTipo, fila.OrigenId, fila.OrigenLinea })
            .IsUnique()
            .HasDatabaseName(UnaPorOrigen);

        // LO RESERVADO DE UNA CLAVE se suma de sus reservas activas (ADR-0059 §3), y la suma la lee
        // quien tiene la valoración de esa clave bloqueada. Parcial, porque las consumidas y las
        // liberadas son casi todas y no cuentan nunca.
        reserva.HasIndex(fila => new { fila.EmpresaId, fila.ArticuloId, fila.AlmacenId })
            .HasFilter("estado = 'Activa'")
            .HasDatabaseName(ActivasPorClave);
    }
}
