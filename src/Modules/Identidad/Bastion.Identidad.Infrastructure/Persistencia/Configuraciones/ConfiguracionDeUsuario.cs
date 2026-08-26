using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.Identidad.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Identidad.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeUsuario : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        usuario.ToTable("usuarios");
        usuario.HasKey(fila => fila.Id);

        // 254 es el tope real de una dirección de correo (RFC 5321). Aquí el máximo no es una
        // comodidad: es lo que hace que el índice único quepa y que nadie meta un texto largo por
        // este campo.
        usuario.Property(fila => fila.Correo)
            .HasConversion(correo => correo.Valor, valor => Correo.De(valor))
            .HasMaxLength(Correo.Longitud)
            .IsRequired();

        // Una cuenta por correo. Es con lo que se inicia sesión: dos filas con el mismo correo
        // harían que el login dependiera de cuál devuelva primero la consulta.
        usuario.HasIndex(fila => fila.Correo).IsUnique();

        usuario.Property(fila => fila.Nombre).IsRequired();

        // El resumen, nunca la contraseña. Longitud sin tope: el formato del `PasswordHasher`
        // crece cuando sube la versión del algoritmo, y un `varchar` corto convertiría esa subida
        // en filas truncadas, o sea, en cuentas que dejan de poder entrar.
        usuario.Property(fila => fila.HashDeContrasena).IsRequired();

        usuario.Property(fila => fila.Estado)
            .HasConversion<string>()
            .IsRequired();

        // Los cuatro son INSTANTES, no fechas de negocio: `timestamptz`. De `BloqueadoEn` arranca
        // el plazo del art. 32 de la LOPDGDD, y `RechazadoHasta` se compara contra el reloj para
        // decidir si se admite un intento — una fecha sin zona haría que esa comparación
        // dependiera de dónde esté el servidor.
        usuario.Property(fila => fila.BloqueadoEn);
        usuario.Property(fila => fila.CreadoEn).IsRequired();
        usuario.Property(fila => fila.UltimoAccesoEn);
        usuario.Property(fila => fila.RechazadoHasta);
        usuario.Property(fila => fila.IntentosFallidos).IsRequired();

        // La colección se mapea por su CAMPO, no por la propiedad: la propiedad es de solo
        // lectura a propósito, para que nadie añada pertenencias esquivando `Conceder`.
        usuario.Metadata
            .FindNavigation(nameof(Usuario.Membresias))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        usuario.HasMany(fila => fila.Membresias)
            .WithOne()
            .HasForeignKey(membresia => membresia.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
