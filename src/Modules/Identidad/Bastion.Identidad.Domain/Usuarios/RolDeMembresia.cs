namespace Bastion.Identidad.Domain.Usuarios;

/// <summary>
/// Un rol concedido a un usuario en una empresa concreta.
/// </summary>
/// <remarks>
/// Es una entidad hija de <see cref="Membresia"/> y no una lista de identificadores sueltos
/// porque tiene que ser una fila: la tabla que la guarda lleva clave primaria compuesta
/// <c>(membresia_id, rol_id)</c>, y esa clave es lo que impide conceder dos veces el mismo rol.
/// Una comprobación en memoria haría lo mismo hasta el día que dos peticiones simultáneas la
/// pasen las dos.
/// </remarks>
public sealed class RolDeMembresia
{
    private RolDeMembresia()
    {
    }

    internal RolDeMembresia(Guid membresiaId, Guid rolId) =>
        (MembresiaId, RolId) = (membresiaId, rolId);

    /// <summary>Pertenencia a la que se concede el rol.</summary>
    public Guid MembresiaId { get; private set; }

    /// <summary>Rol concedido.</summary>
    public Guid RolId { get; private set; }
}
