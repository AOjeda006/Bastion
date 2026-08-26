namespace Bastion.Identidad.Domain.Usuarios;

/// <summary>
/// Pertenencia de un usuario a una empresa, con los roles que tiene ahí.
/// </summary>
/// <remarks>
/// <para>
/// <b>Los roles son POR EMPRESA</b> (§11). La misma persona puede ser quien contabiliza en una
/// sociedad y solo consultar en otra; un rol global obligaría a crear un usuario por empresa,
/// que es como se acaba compartiendo contraseñas.
/// </para>
/// <para>
/// <b><see cref="EmpresaId"/> es un <c>Guid</c> sin clave ajena, y es deliberado.</b> La empresa
/// vive en el esquema <c>organizacion</c> y esta tabla en <c>identidad</c>: PostgreSQL dejaría
/// poner la clave ajena entre esquemas sin rechistar, y con ella los dos módulos quedarían atados
/// por la base de datos justo donde el §4 dice que solo pueden hablarse por <c>Contracts</c>.
/// Que la empresa exista lo comprueba el caso de uso preguntándoselo a Organización.
/// </para>
/// <para>
/// Es una entidad hija del agregado <see cref="Usuario"/>: se llega a ella a través del usuario y
/// no tiene repositorio propio. Así la invariante «una sola pertenencia por empresa» se comprueba
/// donde están todas las pertenencias, que es el único sitio donde se puede comprobar.
/// </para>
/// </remarks>
public sealed class Membresia
{
    private readonly List<RolDeMembresia> _roles = [];

    private Membresia()
    {
    }

    internal Membresia(Guid usuarioId, Guid empresaId)
    {
        Id = Guid.CreateVersion7();
        UsuarioId = usuarioId;
        EmpresaId = empresaId;
    }

    /// <summary>Identificador de la pertenencia.</summary>
    public Guid Id { get; private set; }

    /// <summary>Usuario al que pertenece.</summary>
    public Guid UsuarioId { get; private set; }

    /// <summary>Empresa a la que pertenece. Sin clave ajena: cruza esquemas.</summary>
    public Guid EmpresaId { get; private set; }

    /// <summary>Roles que el usuario tiene en esa empresa.</summary>
    public IReadOnlyCollection<RolDeMembresia> Roles => _roles;

    /// <summary>Concede un rol. Repetirlo no hace nada.</summary>
    /// <param name="rolId">Rol que se concede.</param>
    /// <returns>Si el rol no lo tenía ya.</returns>
    public bool AsignarRol(Guid rolId)
    {
        if (_roles.Exists(rol => rol.RolId == rolId))
        {
            return false;
        }

        _roles.Add(new RolDeMembresia(Id, rolId));
        return true;
    }

    /// <summary>Retira un rol. Retirar uno que no tenía no hace nada.</summary>
    /// <param name="rolId">Rol que se retira.</param>
    /// <returns>Si el rol lo tenía.</returns>
    public bool RetirarRol(Guid rolId) => _roles.RemoveAll(rol => rol.RolId == rolId) > 0;

    /// <summary>Si tiene concedido ese rol.</summary>
    /// <param name="rolId">Rol que se busca.</param>
    public bool Tiene(Guid rolId) => _roles.Exists(rol => rol.RolId == rolId);
}
