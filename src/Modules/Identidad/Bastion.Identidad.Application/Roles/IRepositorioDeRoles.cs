using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Identidad.Domain.Roles;

namespace Bastion.Identidad.Application.Roles;

/// <summary>Acceso a los roles guardados.</summary>
public interface IRepositorioDeRoles : IOrdenaPor
{
    /// <summary>El rol con ese identificador, con sus permisos, o nulo.</summary>
    /// <param name="id">Identificador del rol.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Rol?> ObtenerAsync(Guid id, CancellationToken cancelacion);

    /// <summary>El rol con ese código, o nulo.</summary>
    /// <param name="codigo">Código estable, ya normalizado.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Rol?> ObtenerPorCodigoAsync(string codigo, CancellationToken cancelacion);

    /// <summary>Si ya hay un rol con ese código.</summary>
    /// <param name="codigo">Código estable, ya normalizado.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<bool> ExisteConCodigoAsync(string codigo, CancellationToken cancelacion);

    /// <summary>Si existe el rol, sin traérselo entero.</summary>
    /// <param name="id">Identificador del rol.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<bool> ExisteAsync(Guid id, CancellationToken cancelacion);

    /// <summary>Una página de roles, con el total.</summary>
    /// <param name="paginacion">Qué página se pide.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<PaginaDe<Rol>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion);

    /// <summary>
    /// Los permisos que conceden esos roles, sin repetidos.
    /// </summary>
    /// <remarks>
    /// Es la consulta que alimenta el token: un usuario tiene varios roles en una empresa y lo
    /// que va al <i>claim</i> es la unión de lo que conceden. Se pide entera y de una vez para no
    /// hacer una consulta por rol en el camino del login, que es el más caliente que hay.
    /// </remarks>
    /// <param name="rolIds">Roles concedidos en la empresa activa.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<IReadOnlyList<string>> PermisosDeAsync(
        IReadOnlyCollection<Guid> rolIds,
        CancellationToken cancelacion);

    /// <summary>Los roles con esos identificadores, con sus permisos.</summary>
    /// <remarks>
    /// Para pintar las pertenencias de un usuario hace falta el nombre de cada rol, y pedirlos de
    /// uno en uno dentro del bucle de pertenencias es el problema de las N+1 consultas escrito a
    /// mano. Con la lista entera son dos consultas, no crezca lo que crezca el número de roles.
    /// </remarks>
    /// <param name="rolIds">Identificadores que se quieren resolver.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<IReadOnlyList<Rol>> PorIdsAsync(IReadOnlyCollection<Guid> rolIds, CancellationToken cancelacion);

    /// <summary>Apunta un rol nuevo. No lo graba: eso lo hace la unidad de trabajo.</summary>
    /// <param name="rol">Rol que se crea.</param>
    void Agregar(Rol rol);
}
