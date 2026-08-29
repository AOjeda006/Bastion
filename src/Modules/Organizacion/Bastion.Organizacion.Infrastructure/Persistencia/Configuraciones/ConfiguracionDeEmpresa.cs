using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.Organizacion.Domain.Empresas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeEmpresa : IEntityTypeConfiguration<Empresa>
{
    public void Configure(EntityTypeBuilder<Empresa> empresa)
    {
        ArgumentNullException.ThrowIfNull(empresa);

        empresa.ToTable("empresas");

        // Maestro de la instalacion entera: quien la dio de alta, quien le cambio el NIF y quien
        // la bloqueo son las tres preguntas que una inspeccion hace primero.
        empresa.SeAudita();
        empresa.HasKey(fila => fila.Id);

        // `varchar(9)` y no `text`: nueve no es una estimación de comodidad, es la longitud del
        // identificador fiscal. Es el caso de libro en el que el tope pertenece al esquema.
        empresa.Property(fila => fila.Nif)
            .HasConversion(nif => nif.Valor, valor => Nif.De(valor))
            .HasMaxLength(Nif.Longitud)
            .IsRequired()
            .SeAudita();

        // Una empresa por NIF: dos fichas con el mismo NIF son dos libros registro para el
        // mismo obligado tributario, que es justo lo que R15 no permite.
        empresa.HasIndex(fila => fila.Nif).IsUnique();

        // `text`: la razón social no tiene tope legal. Un `varchar(200)` inventado solo sirve
        // para rechazar el día que aparezca una más larga, y en PostgreSQL no gana nada.
        empresa.Property(fila => fila.RazonSocial).IsRequired().SeAudita();

        empresa.Property(fila => fila.DivisaBase)
            .HasMaxLength(3)
            .IsRequired()
            .SeAudita();

        // Enumerados como TEXTO. Guardados por su valor entero dejan de significar nada en
        // cuanto alguien reordena el enumerado, y los datos duran más que el código.
        empresa.Property(fila => fila.RegimenDeIva)
            .HasConversion<string>()
            .IsRequired()
            .SeAudita();

        empresa.Property(fila => fila.Estado)
            .HasConversion<string>()
            .IsRequired()
            .SeAudita();

        // Instante, no fecha de negocio: de aquí arranca el plazo de prescripción del art. 32
        // de la LOPDGDD, así que se guarda con zona horaria.
        empresa.Property(fila => fila.BloqueadaEn).SeAudita();

        empresa.OwnsOne(fila => fila.DomicilioFiscal, ConfiguracionDeDireccion.Mapear);
        empresa.Navigation(fila => fila.DomicilioFiscal).IsRequired();
    }
}
