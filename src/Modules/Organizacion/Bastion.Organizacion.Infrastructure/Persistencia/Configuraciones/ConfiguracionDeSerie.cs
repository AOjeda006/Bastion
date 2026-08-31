using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Organizacion.Domain.Ejercicios;
using Bastion.Organizacion.Domain.Empresas;
using Bastion.Organizacion.Domain.Series;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeSerie : IEntityTypeConfiguration<Serie>
{
    public void Configure(EntityTypeBuilder<Serie> serie)
    {
        ArgumentNullException.ThrowIfNull(serie);

        serie.ToTable("series");

        // Maestro, y el mas delicado de los tres: la numeracion de una serie es legal (R5).
        serie.SeAudita();
        serie.HasKey(fila => fila.Id);

        serie.LlevaTestigoDeConcurrencia();

        ConfiguracionDeEntidadBase.Mapear(serie);

        serie.Property(fila => fila.EmpresaId).IsRequired().SeAudita();
        serie.Property(fila => fila.EjercicioId).IsRequired().SeAudita();

        serie.Property(fila => fila.TipoDeDocumento)
            .HasConversion<string>()
            .IsRequired()
            .SeAudita();

        serie.Property(fila => fila.Estado)
            .HasConversion<string>()
            .IsRequired()
            .SeAudita();

        serie.Property(fila => fila.Codigo)
            .HasMaxLength(Serie.LongitudMaximaDeCodigo)
            .IsRequired()
            .SeAudita();

        // El formato es una plantilla, no un identificador: no tiene tope legal, así que `text`.
        serie.Property(fila => fila.Formato).IsRequired().SeAudita();

        // EL CONTADOR ES UNA COLUMNA, y esto es una decisión de esquema sin segunda oportunidad.
        // Una secuencia de PostgreSQL sería lo cómodo, pero `nextval` NO se revierte al deshacer
        // la transacción: una confirmación que falla dejaría un hueco permanente, y R5 dice
        // «correlativa y sin huecos». El número lo asigna Facturación (fase 5) bloqueando esta
        // fila dentro de la transacción de confirmación.
        // Se audita, y la consecuencia hay que mirarla de frente: en la fase 5 esta columna se
        // mueve una vez por documento emitido, asi que la traza dejara de crecer con los cambios
        // de maestro y pasara a crecer con el volumen de facturacion. Aun asi va dentro, porque
        // un contador de serie retrocedido es exactamente lo que R5 prohibe y lo primero que una
        // inspeccion mira. Cuando llegue la fase 5 se revisa AQUI, con el numero delante.
        serie.Property(fila => fila.Contador).IsRequired().SeAudita();

        serie.HasIndex(fila => new { fila.EmpresaId, fila.EjercicioId, fila.Codigo }).IsUnique();

        serie.HasOne<Empresa>()
            .WithMany()
            .HasForeignKey(fila => fila.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        serie.HasOne<Ejercicio>()
            .WithMany()
            .HasForeignKey(fila => fila.EjercicioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
