using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Organizacion.Domain.Divisas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeDivisa : IEntityTypeConfiguration<Divisa>
{
    /// <summary>Longitud de un código ISO 4217: tres letras, ni una más.</summary>
    public const int LongitudDelCodigo = 3;

    public void Configure(EntityTypeBuilder<Divisa> divisa)
    {
        ArgumentNullException.ThrowIfNull(divisa);

        divisa.ToTable("divisas");

        divisa.SeAudita();
        divisa.HasKey(fila => fila.Id);

        divisa.LlevaTestigoDeConcurrencia();

        ConfiguracionDeEntidadBase.Mapear(divisa);

        // `char(3)` no: `varchar(3)`. Un `char` rellena con espacios a la derecha y luego los
        // arrastra a cada comparación.
        divisa.Property(fila => fila.Codigo)
            .HasMaxLength(LongitudDelCodigo)
            .IsRequired()
            .SeAudita();

        divisa.Property(fila => fila.Nombre)
            .HasMaxLength(Divisa.LongitudMaximaDeNombre)
            .IsRequired()
            .SeAudita();

        // `Decimales` NO es una columna, y ese es el punto entero de la entidad: los decimales de
        // redondeo fiscal viven en el catálogo de los bloques comunes, con su caso dorado. Aquí
        // se ignora explícitamente, porque por convención EF Core intentaría mapear la propiedad
        // y crearía justamente la segunda fuente de verdad que se está evitando.
        divisa.Ignore(fila => fila.Decimales);

        // Una divisa, una fila. Sin esto, dos «EUR» con nombres distintos y ninguna forma de
        // decidir cuál es la buena.
        divisa.HasIndex(fila => fila.Codigo).IsUnique();
    }
}
