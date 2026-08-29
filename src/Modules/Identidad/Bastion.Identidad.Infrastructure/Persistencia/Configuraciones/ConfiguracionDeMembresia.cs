using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.Identidad.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Identidad.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeMembresia : IEntityTypeConfiguration<Membresia>
{
    public void Configure(EntityTypeBuilder<Membresia> membresia)
    {
        ArgumentNullException.ThrowIfNull(membresia);

        membresia.ToTable("membresias");

        // Quien pertenece a que empresa. Es la frontera del inquilinato del 0.6 escrita en filas:
        // un alta aqui da acceso a los datos de una empresa entera.
        membresia.SeAudita();
        membresia.HasKey(fila => fila.Id);

        membresia.Property(fila => fila.UsuarioId).IsRequired().SeAudita();

        // `empresa_id` es un Guid A SECAS, sin `HasOne(...).HasForeignKey(...)`, y eso NO es un
        // descuido. La empresa vive en el esquema `organizacion`: PostgreSQL dejaría poner la
        // clave ajena —las claves ajenas entre esquemas son legales—, pero la frontera del §4 no.
        // Con la clave puesta, borrar una tabla de Organización arrastraría filas de Identidad y
        // los dos módulos dejarían de poder migrarse por separado.
        //
        // Lo que sustituye a la integridad referencial es una comprobación explícita contra
        // `IConsultaDeEmpresas` en el caso de uso que escribe esta fila. No es gratis; es el
        // precio de la frontera, y está pagado a la vista.
        membresia.Property(fila => fila.EmpresaId).IsRequired().SeAudita();

        // Una sola pertenencia por usuario y empresa. Dos filas darían dos juegos de roles para
        // el mismo par, y la sesión se llevaría el de la que saliera primero.
        membresia.HasIndex(fila => new { fila.UsuarioId, fila.EmpresaId }).IsUnique();

        membresia.Metadata
            .FindNavigation(nameof(Membresia.Roles))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        membresia.HasMany(fila => fila.Roles)
            .WithOne()
            .HasForeignKey(rol => rol.MembresiaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ConfiguracionDeRolDeMembresia : IEntityTypeConfiguration<RolDeMembresia>
{
    public void Configure(EntityTypeBuilder<RolDeMembresia> concedido)
    {
        ArgumentNullException.ThrowIfNull(concedido);

        concedido.ToTable("roles_de_membresia");

        // Que rol tiene alguien en una empresa: la otra mitad de «quien puede que». Como
        // `PermisoDeRol`, sus dos columnas son la clave y el alta o la baja SON el cambio.
        concedido.SeAudita();

        // Clave primaria COMPUESTA, que es lo que impide conceder dos veces el mismo rol. La
        // comprobación en memoria de `AsignarRol` cubre el caso normal; esta cubre el de dos
        // peticiones simultáneas, que es el que la comprobación en memoria no puede ver.
        concedido.HasKey(fila => new { fila.MembresiaId, fila.RolId });

        // Sin clave ajena hacia `roles`: la asignación tiene que sobrevivir a que el rol se
        // borre, porque lo que hay que poder hacer entonces es limpiarla, no quedarse sin poder
        // tocar la pertenencia. `RetirarRol` no comprueba que el rol exista, justamente por esto.
        concedido.Property(fila => fila.RolId).IsRequired();
    }
}
