using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.Identidad.Application.Roles;
using Bastion.Identidad.Application.Sesiones;
using Bastion.Identidad.Application.Usuarios;
using Bastion.Identidad.Domain.Roles;
using Bastion.Identidad.Domain.Usuarios;

namespace Bastion.Identidad.Application.Arranque;

/// <summary>Los datos con los que nace la primera cuenta.</summary>
/// <param name="EmpresaId">Empresa a la que pertenece.</param>
/// <param name="Correo">Correo con el que iniciará sesión.</param>
/// <param name="Contrasena">Contraseña inicial, que sale del entorno y no del repositorio.</param>
public sealed record SemillaDeAdministrador(Guid EmpresaId, string Correo, string Contrasena);

/// <summary>
/// Crea la primera cuenta, y solo mientras no haya ninguna.
/// </summary>
/// <remarks>
/// <para>
/// Resuelve la circularidad del arranque: para crear un usuario hace falta el permiso
/// <c>identidad.usuario.crear</c>, el permiso viene de un rol, el rol se asigna en una pertenencia
/// y la pertenencia la concede alguien que ya está dentro. Sin una puerta de arranque, un sistema
/// que deniega por defecto nace cerrado para siempre.
/// </para>
/// <para>
/// <b>La condición es «no hay NINGÚN usuario», no «no existe este correo».</b> Con la segunda,
/// quien pudiera cambiar una variable de entorno se fabricaría un administrador nuevo en cada
/// reinicio de una instalación ya en marcha. Con la primera, la puerta se cierra sola en cuanto
/// existe la primera cuenta y no vuelve a abrirse.
/// </para>
/// </remarks>
public interface ISembrarAdministrador
{
    /// <summary>Ejecuta la semilla.</summary>
    /// <param name="semilla">Empresa, correo y contraseña inicial.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns><c>true</c> si ha creado la cuenta; <c>false</c> si ya había usuarios.</returns>
    Task<bool> EjecutarAsync(SemillaDeAdministrador semilla, CancellationToken cancelacion);
}

/// <inheritdoc cref="ISembrarAdministrador"/>
internal sealed class SembrarAdministrador(
    IRepositorioDeUsuarios usuarios,
    IRepositorioDeRoles roles,
    ICatalogoDePermisos catalogo,
    IHasherDeContrasenas hasher,
    IUnidadTrabajoDeIdentidad unidadTrabajo,
    TimeProvider reloj) : ISembrarAdministrador
{
    /// <summary>Código del rol que reúne todos los permisos.</summary>
    /// <remarks>
    /// Nace marcado como <b>del sistema</b>: es el único que se crea sin que nadie lo pida, y el
    /// que tiene la facultad de repartir cualquier otra. Marcarlo permite que el módulo lo trate
    /// distinto —no se borra— sin tener que reconocerlo por su nombre, que es editable.
    /// </remarks>
    internal const string CodigoDelRol = "administracion";

    public async Task<bool> EjecutarAsync(SemillaDeAdministrador semilla, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(semilla);

        if (!await usuarios.NoHayNingunoAsync(cancelacion).ConfigureAwait(false))
        {
            return false;
        }

        if (!Correo.Intentar(semilla.Correo, out Correo? correo))
        {
            // Revienta el arranque en lugar de seguir sin administrador. Un sistema en pie al que
            // nadie puede entrar es peor que uno que no arranca: el segundo se arregla mirando el
            // mensaje; el primero se descubre cuando alguien intenta trabajar.
            throw new InvalidOperationException(
                "El correo de la semilla de arranque no es un correo electrónico válido.");
        }

        Rol rol = await roles.ObtenerPorCodigoAsync(CodigoDelRol, cancelacion).ConfigureAwait(false)
            ?? CrearRolDeAdministracion();

        // Se fijan SIEMPRE, también si el rol ya existía: el catálogo crece con cada módulo que
        // se añade, y un rol de administración que se quedó con los permisos de la fase 0 dejaría
        // la fase 1 sin nadie que pudiera conceder los suyos.
        rol.FijarPermisos(catalogo.Todos);

        var usuario = Usuario.Crear(
            correo!,
            "Administración",
            hasher.Hashear(semilla.Contrasena),
            reloj.GetUtcNow());

        Membresia membresia = usuario.Conceder(semilla.EmpresaId);
        membresia.AsignarRol(rol.Id);

        usuarios.Agregar(usuario);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return true;
    }

    private Rol CrearRolDeAdministracion()
    {
        var rol = Rol.Crear(CodigoDelRol, "Administración", reloj.GetUtcNow(), esDelSistema: true);
        roles.Agregar(rol);

        return rol;
    }
}
