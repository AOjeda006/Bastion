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
        rol.HasKey(fila => fila.Id);

        rol.Property(fila => fila.Codigo)
            .HasMaxLength(Rol.LongitudDelCodigo)
            .IsRequired();

        // El código es contrato con la semilla: `administrador` tiene que ser uno y solo uno,
        // porque es por donde la semilla vuelve a encontrarlo.
        rol.HasIndex(fila => fila.Codigo).IsUnique();

        rol.Property(fila => fila.Nombre).IsRequired();
        rol.Property(fila => fila.EsDelSistema).IsRequired();

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
