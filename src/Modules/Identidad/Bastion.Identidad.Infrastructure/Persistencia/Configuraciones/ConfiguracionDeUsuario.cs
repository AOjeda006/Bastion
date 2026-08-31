using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Bloqueos;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
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

        // Maestro de personas, y ademas el sujeto del §11: quien entro, quien fue bloqueado y
        // quien cambio el correo de quien.
        usuario.SeAudita();
        usuario.HasKey(fila => fila.Id);

        usuario.LlevaTestigoDeConcurrencia();

        // 254 es el tope real de una dirección de correo (RFC 5321). Aquí el máximo no es una
        // comodidad: es lo que hace que el índice único quepa y que nadie meta un texto largo por
        // este campo.
        usuario.Property(fila => fila.Correo)
            .HasConversion(correo => correo.Valor, valor => Correo.De(valor))
            .HasMaxLength(Correo.Longitud)
            .IsRequired()
            .SeAudita();

        // Una cuenta por correo. Es con lo que se inicia sesión: dos filas con el mismo correo
        // harían que el login dependiera de cuál devuelva primero la consulta.
        usuario.HasIndex(fila => fila.Correo).IsUnique();

        usuario.Property(fila => fila.Nombre).IsRequired().SeAudita();

        // El resumen, nunca la contraseña. Longitud sin tope: el formato del `PasswordHasher`
        // crece cuando sube la versión del algoritmo, y un `varchar` corto convertiría esa subida
        // en filas truncadas, o sea, en cuentas que dejan de poder entrar.
        //
        // Y SECRETA. Una tabla que registra el valor viejo y el nuevo de cada propiedad, y que por
        // diseño no se puede limpiar, seria —sin que nadie lo decidiera— el historial completo de
        // resumenes de contraseña de todo el mundo. No es que «no interese auditarla»: es que no
        // puede acabar ahi por ningun camino, y hay un test que lo comprueba por el VALOR.
        usuario.Property(fila => fila.HashDeContrasena)
            .IsRequired()
            .EsSecreta("resumen de credencial: el historial de resumenes es un boton de ataque");

        ConfiguracionDeEntidadBase.Mapear(usuario);

        // El bloqueo de R16, con su fecha y su motivo. Es el de la baja logica, y no tiene nada
        // que ver con `RechazadoHasta`, que esta cuatro lineas mas abajo: uno lo decide una
        // persona y no caduca, el otro lo decide un contador de intentos y se levanta solo.
        usuario.ComplexProperty(fila => fila.Bloqueo, ConfiguracionDeBloqueo.Mapear);

        // Los dos de abajo son INSTANTES, no fechas de negocio: `timestamptz`. `RechazadoHasta`
        // se compara contra el reloj para decidir si se admite un intento — una fecha sin zona
        // haria que esa comparacion dependiera de donde este el servidor.

        // Los tres de abajo son ACCESOS, no cambios de maestro, y el §11 los pide igual. La
        // consecuencia se dice entera: cada entrada correcta y cada intento fallido escriben una
        // fila de traza, asi que esta tabla crece con el uso y no solo con la administracion. Es
        // lo que se quiere —«quien entro» es la mitad de una auditoria— y es lo que hay que tener
        // presente cuando en el 0.9, o mas tarde, se hable de retencion.
        usuario.Property(fila => fila.UltimoAccesoEn).SeAudita();
        usuario.Property(fila => fila.RechazadoHasta).SeAudita();
        usuario.Property(fila => fila.IntentosFallidos).IsRequired().SeAudita();

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
