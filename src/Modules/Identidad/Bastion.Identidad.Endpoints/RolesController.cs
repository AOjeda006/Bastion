using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.Identidad.Application.Roles;
using Bastion.Identidad.Contracts;
using Bastion.Identidad.Contracts.Comun;
using Bastion.Identidad.Contracts.Roles;
using Bastion.Identidad.Endpoints.Comun;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Identidad.Endpoints;

/// <summary>
/// Roles, bajo <c>/api/v1/identidad/roles</c>.
/// </summary>
/// <remarks>
/// Un rol es una agrupación de permisos, y nada más: la autorización no pregunta nunca por el rol,
/// pregunta por el permiso. Por eso aquí no hay ni un <c>[Authorize(Roles = "...")]</c>. Si lo
/// hubiera, el día que alguien creara un rol nuevo con los mismos permisos —cosa que esta misma
/// pantalla permite— ese rol no abriría la puerta, y el fallo se leería como un problema de datos
/// cuando sería de diseño.
/// </remarks>
public sealed class RolesController(
    ICrearRol crear,
    IObtenerRol obtener,
    IListarRoles listar,
    IModificarRol modificar,
    IListarPermisosDisponibles permisos) : ControladorDeIdentidad
{
    /// <summary>Devuelve una página de roles.</summary>
    /// <param name="consulta">Paginación pedida (<c>page</c> y <c>size</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ExigePermiso(PermisosDeIdentidad.RolVer)]
    [ProducesResponseType(typeof(PaginaDe<RolDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] ConsultaPaginada consulta,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return Ok(await listar.EjecutarAsync(consulta.APaginacion(), cancelacion).ConfigureAwait(false));
    }

    /// <summary>Devuelve el catálogo de permisos que se pueden conceder.</summary>
    /// <remarks>
    /// Exige <c>rol.ver</c> y no es una consulta pública de conveniencia: enumerar los permisos que
    /// existe es enumerar lo que el sistema sabe hacer, y eso es un mapa que a un anónimo no se le
    /// da. La ruta va antes que <c>{id:guid}</c> por claridad; no compiten, porque
    /// <c>permisos</c> no casa con la restricción <c>:guid</c>.
    /// </remarks>
    [HttpGet("permisos")]
    [ExigePermiso(PermisosDeIdentidad.RolVer)]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public IActionResult Permisos() => Ok(permisos.Ejecutar());

    /// <summary>Devuelve un rol.</summary>
    /// <param name="id">Identificador del rol.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{id:guid}")]
    [ExigePermiso(PermisosDeIdentidad.RolVer)]
    [ProducesResponseType(typeof(RolDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancelacion) =>
        Responder(await obtener.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>Crea un rol con su lista de permisos.</summary>
    /// <param name="peticion">Código, nombre y permisos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost]
    [ExigePermiso(PermisosDeIdentidad.RolCrear)]
    [ProducesResponseType(typeof(RolDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearRolDto peticion,
        CancellationToken cancelacion) =>
        ResponderCreado(
            await crear.EjecutarAsync(peticion, cancelacion).ConfigureAwait(false),
            nameof(Obtener),
            rol => rol.Id);

    /// <summary>Cambia el nombre y los permisos de un rol.</summary>
    /// <remarks>
    /// Permiso propio, distinto del de crear: crear un rol vacío no le da a nadie nada, mientras
    /// que modificar uno le cambia los permisos a todos los que ya lo tienen asignado.
    /// </remarks>
    /// <param name="id">Identificador del rol.</param>
    /// <param name="peticion">Nombre y la lista ENTERA de permisos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPut("{id:guid}")]
    [ExigePermiso(PermisosDeIdentidad.RolModificar)]
    [ProducesResponseType(typeof(RolDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Modificar(
        Guid id,
        [FromBody] ModificarRolDto peticion,
        CancellationToken cancelacion) =>
        Responder(await modificar.EjecutarAsync(id, peticion, cancelacion).ConfigureAwait(false));
}
