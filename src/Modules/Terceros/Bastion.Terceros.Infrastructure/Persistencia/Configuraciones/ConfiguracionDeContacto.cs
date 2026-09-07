using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Terceros.Domain.Terceros;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Terceros.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeContacto : IEntityTypeConfiguration<Contacto>
{
    public void Configure(EntityTypeBuilder<Contacto> contacto)
    {
        ArgumentNullException.ThrowIfNull(contacto);

        contacto.ToTable("contactos");

        // Se audita entero: es la tabla con nombre, correo y teléfono de personas identificadas.
        // Quién los escribió y quién los borró tiene que constar.
        contacto.SeAudita();
        contacto.HasKey(fila => fila.Id);

        // SIN testigo de concurrencia propio, y no por olvido: un contacto no es un recurso que se
        // edite por su cuenta, es parte del agregado. El testigo que gobierna la edición es el del
        // tercero, que es lo que la ruta exige en el cuerpo y lo que hace que dos ediciones
        // simultáneas de la MISMA ficha —una que cambia la razón social y otra que cuelga un
        // contacto— se detecten. Un testigo por hijo dejaría pasar la segunda.
        contacto.Property(fila => fila.TerceroId).IsRequired().SeAudita();

        contacto.Property(fila => fila.Nombre)
            .HasMaxLength(Contacto.LongitudMaximaDeNombre)
            .IsRequired()
            .SeAudita();

        contacto.Property(fila => fila.Cargo)
            .HasMaxLength(Contacto.LongitudMaximaDeCargo)
            .SeAudita();

        // Igual que el correo del usuario: una columna con la conversión del objeto de valor.
        // Anulable, porque un contacto puede tener solo teléfono.
        contacto.Property(fila => fila.Correo)
            .HasConversion(correo => correo!.Valor, valor => Correo.De(valor))
            .HasMaxLength(Correo.Longitud)
            .SeAudita();

        contacto.Property(fila => fila.Telefono)
            .HasMaxLength(Contacto.LongitudMaximaDeTelefono)
            .SeAudita();

        ConfiguracionDeEntidadBase.Mapear(contacto);

        // El índice es por la ficha, que es como se lee siempre: todos los contactos de este
        // tercero. Nadie busca un contacto suelto por su identificador.
        contacto.HasIndex(fila => fila.TerceroId);
    }
}
