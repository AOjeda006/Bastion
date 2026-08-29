using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.Organizacion.Domain.Ejercicios;
using Bastion.Organizacion.Domain.Empresas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeEjercicio : IEntityTypeConfiguration<Ejercicio>
{
    public void Configure(EntityTypeBuilder<Ejercicio> ejercicio)
    {
        ArgumentNullException.ThrowIfNull(ejercicio);

        ejercicio.ToTable("ejercicios");

        // Maestro, y de los que deciden en que autoliquidacion entra una operacion (R14): abrir,
        // cerrar o mover las fechas de un ejercicio es un cambio con consecuencias fiscales.
        ejercicio.SeAudita();
        ejercicio.HasKey(fila => fila.Id);

        // R8: la columna de empresa está desde la primera tabla. El filtro global que la aplica
        // en toda consulta es del ítem 0.6; añadir la columna después obligaría a tocar todas
        // las tablas y todas las consultas, y eso es lo que se está evitando hoy.
        ejercicio.Property(fila => fila.EmpresaId).IsRequired().SeAudita();

        // `date`, no `timestamptz`. Un ejercicio contable no tiene zona horaria: empieza el 1 de
        // enero en Madrid y en Canarias. `DateOnly` mapea a `date` sin que haya que decirlo, y
        // hay un test de integración que lo comprueba en `information_schema`.
        ejercicio.Property(fila => fila.FechaDeInicio).IsRequired().SeAudita();
        ejercicio.Property(fila => fila.FechaDeFin).IsRequired().SeAudita();

        // No lleva `Property()` propio por convencion, pero clasificarse tiene que clasificarse:
        // el barrido no distingue entre «mapeada a mano» y «mapeada por convencion».
        ejercicio.Property(fila => fila.Anio).SeAudita();

        ejercicio.Property(fila => fila.Estado)
            .HasConversion<string>()
            .IsRequired()
            .SeAudita();

        // Un año, un ejercicio, por empresa.
        ejercicio.HasIndex(fila => new { fila.EmpresaId, fila.Anio }).IsUnique();

        // Clave foránea DENTRO del esquema del módulo. Entre esquemas no hay ninguna (§3).
        // `Restrict` y no `Cascade`: una empresa no se borra, se bloquea, y arrastrar sus
        // ejercicios en cascada sería el borrado que R16 prohíbe, por la puerta de atrás.
        ejercicio.HasOne<Empresa>()
            .WithMany()
            .HasForeignKey(fila => fila.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
