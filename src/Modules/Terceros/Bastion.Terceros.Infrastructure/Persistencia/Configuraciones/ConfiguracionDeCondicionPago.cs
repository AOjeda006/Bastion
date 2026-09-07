using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Terceros.Domain.Terceros;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Terceros.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeCondicionPago : IEntityTypeConfiguration<CondicionPago>
{
    public void Configure(EntityTypeBuilder<CondicionPago> condicion)
    {
        ArgumentNullException.ThrowIfNull(condicion);

        // EL TOPE DE SESENTA DÍAS, TAMBIÉN EN EL MOTOR, y no por desconfiar del dominio: el
        // dominio protege lo que pasa por sus fábricas, y a esta tabla se llega también desde un
        // `UPDATE` a mano y desde una restauración de copia. La Ley 3/2004 dice que el plazo NO ES
        // AMPLIABLE POR ACUERDO, así que no hay ningún camino por el que un 90 sea correcto, y una
        // restricción que el motor sostenga es la única que sigue puesta cuando no hay proceso.
        condicion.ToTable(
            "condiciones_pago",
            tabla => tabla.HasCheckConstraint(
                "ck_condiciones_pago_plazo_legal",
                $"dias_de_plazo BETWEEN 0 AND {CondicionPago.DiasMaximosDePlazo}"));

        condicion.SeAudita();
        condicion.HasKey(fila => fila.Id);

        condicion.Property(fila => fila.TerceroId).IsRequired().SeAudita();

        // Como TEXTO, igual que los demás enumerados del proyecto.
        condicion.Property(fila => fila.Rol)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .SeAudita();

        condicion.Property(fila => fila.DiasDePlazo).IsRequired().SeAudita();
        condicion.Property(fila => fila.DiaDePagoFijo).SeAudita();

        // `numeric(5,2)`: un porcentaje entre 0 y 100 con dos decimales. Nunca coma flotante y
        // nunca el `money` del motor, que lleva pegada una configuración regional.
        condicion.Property(fila => fila.DescuentoPorProntoPago)
            .HasPrecision(5, 2)
            .SeAudita();

        ConfiguracionDeEntidadBase.Mapear(condicion);

        // Una por rol: el agregado actualiza en vez de dar de alta, y esto lo sostiene cuando
        // llegan dos peticiones a la vez.
        condicion.HasIndex(fila => new { fila.TerceroId, fila.Rol }).IsUnique();
    }
}
