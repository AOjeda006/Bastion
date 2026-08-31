using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.Identidad.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Identidad.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeRol : IEntityTypeConfiguration<Rol>
{
    public void Configure(EntityTypeBuilder<Rol> rol)
    {
        ArgumentNullException.ThrowIfNull(rol);

        rol.ToTable("roles");

        // Un rol es un juego de poderes. Cambiarlo cambia lo que puede hacer todo el que lo tenga.
        rol.SeAudita();
        rol.HasKey(fila => fila.Id);

        rol.LlevaTestigoDeConcurrencia();

        rol.Property(fila => fila.Codigo)
            .HasMaxLength(Rol.LongitudDelCodigo)
            .IsRequired()
            .SeAudita();

        // El código es contrato con la semilla: `administrador` tiene que ser uno y solo uno,
        // porque es por donde la semilla vuelve a encontrarlo.
        rol.HasIndex(fila => fila.Codigo).IsUnique();

        rol.Property(fila => fila.Nombre).IsRequired().SeAudita();
        rol.Property(fila => fila.EsDelSistema).IsRequired().SeAudita();

        rol.Metadata
            .FindNavigation(nameof(Rol.Permisos))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        rol.HasMany(fila => fila.Permisos)
            .WithOne()
            .HasForeignKey(permiso => permiso.RolId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ConfiguracionDePermisoDeRol : IEntityTypeConfiguration<PermisoDeRol>
{
    public void Configure(EntityTypeBuilder<PermisoDeRol> permiso)
    {
        ArgumentNullException.ThrowIfNull(permiso);

        permiso.ToTable("permisos_de_rol");

        // Conceder o retirar un permiso a un rol es EL cambio que hay que poder reconstruir. No
        // tiene ninguna propiedad que clasificar: sus dos columnas son la clave, o sea, la fila de
        // la que se habla. El alta y la baja de la fila SON el cambio.
        permiso.SeAudita();

        // Clave compuesta: un rol no puede conceder dos veces el mismo permiso.
        permiso.HasKey(fila => new { fila.RolId, fila.Permiso });

        // El permiso se guarda como TEXTO —`modulo.recurso.accion`— y no como número. Un catálogo
        // numerado obligaría a mantener una tabla de equivalencias que ningún módulo podría
        // ampliar sin tocar Identidad; en texto, cada módulo declara los suyos en su `Contracts` y
        // aquí solo se apunta cuál se ha concedido.
        //
        // 120 es tres ranuras estables de sobra. El tope existe porque esta columna entra en la
        // clave primaria, y una clave primaria sin tope es un índice que puede crecer sin freno.
        permiso.Property(fila => fila.Permiso)
            .HasMaxLength(120)
            .IsRequired();
    }
}
