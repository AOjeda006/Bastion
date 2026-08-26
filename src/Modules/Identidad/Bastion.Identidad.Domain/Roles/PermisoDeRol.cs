namespace Bastion.Identidad.Domain.Roles;

/// <summary>Un permiso concedido por un rol.</summary>
/// <remarks>
/// Entidad hija de <see cref="Rol"/>, y no una lista de cadenas, porque tiene que ser una fila:
/// la tabla lleva clave primaria compuesta <c>(rol_id, permiso)</c>, y esa clave es lo que impide
/// conceder dos veces el mismo permiso.
/// </remarks>
public sealed class PermisoDeRol
{
    private PermisoDeRol() => Permiso = null!;

    internal PermisoDeRol(Guid rolId, string permiso) => (RolId, Permiso) = (rolId, permiso);

    /// <summary>Rol que concede el permiso.</summary>
    public Guid RolId { get; private set; }

    /// <summary>El permiso, con la forma <c>modulo.recurso.accion</c>.</summary>
    public string Permiso { get; private set; }
}
