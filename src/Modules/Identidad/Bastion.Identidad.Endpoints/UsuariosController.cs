using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.Identidad.Application.Usuarios;
using Bastion.Identidad.Contracts;
using Bastion.Identidad.Contracts.Comun;
using Bastion.Identidad.Contracts.Usuarios;
using Bastion.Identidad.Endpoints.Comun;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Identidad.Endpoints;

/// <summary>
/// Usuarios, bajo <c>/api/v1/identidad/usuarios</c>.
/// </summary>
/// <remarks>
/// <b>Cada acción declara SU permiso, y son distintos verbo a verbo.</b> Ver no es crear, crear no
/// es modificar y bloquear no es desbloquear, aunque las cuatro últimas las escriba el mismo tipo
/// y toquen la misma tabla. Autorizar «gestionar usuarios» de una vez sería conceder, con la llave
/// de consultar la plantilla, la de cambiarle la contraseña al administrador.
/// </remarks>
public sealed class UsuariosController(
    ICrearUsuario crear,
    IObtenerUsuario obtener,
    IListarUsuarios listar,
    IModificarUsuario modificar,
    IBloquearUsuario bloquear,
    IDesbloquearUsuario desbloquear,
    ICambiarContrasenaPropia cambiarPropia,
    IRestablecerContrasena restablecer,
    IListarPertenencias pertenencias,
    IConcederPertenencia conceder,
    IRetirarPertenencia retirar,
    IAsignarRol asignarRol,
    IRetirarRol retirarRol) : ControladorDeIdentidad
{
    /// <summary>Devuelve una página de usuarios de la empresa activa.</summary>
    /// <param name="consulta">Paginación pedida (<c>page</c> y <c>size</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ExigePermiso(PermisosDeIdentidad.UsuarioVer)]
    [ProducesResponseType(typeof(PaginaDe<UsuarioDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] ConsultaPaginada consulta,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return Ok(await listar.EjecutarAsync(consulta.APaginacion(), cancelacion).ConfigureAwait(false));
    }

    /// <summary>Devuelve un usuario.</summary>
    /// <param name="id">Identificador del usuario.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{id:guid}")]
    [ExigePermiso(PermisosDeIdentidad.UsuarioVer)]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancelacion) =>
        Responder(await obtener.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>Da de alta un usuario en la empresa activa.</summary>
    /// <param name="peticion">Correo, nombre y contraseña inicial.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost]
    [ExigePermiso(PermisosDeIdentidad.UsuarioCrear)]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearUsuarioDto peticion,
        CancellationToken cancelacion) =>
        ResponderCreado(
            await crear.EjecutarAsync(peticion, cancelacion).ConfigureAwait(false),
            nameof(Obtener),
            usuario => usuario.Id);

    /// <summary>Cambia el nombre de un usuario.</summary>
    /// <param name="id">Identificador del usuario.</param>
    /// <param name="peticion">Los datos nuevos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPut("{id:guid}")]
    [ExigePermiso(PermisosDeIdentidad.UsuarioModificar)]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Modificar(
        Guid id,
        [FromBody] ModificarUsuarioDto peticion,
        CancellationToken cancelacion) =>
        Responder(await modificar.EjecutarAsync(id, peticion, cancelacion).ConfigureAwait(false));

    /// <summary>Bloquea un usuario (R16). No lo borra.</summary>
    /// <param name="id">Identificador del usuario.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpDelete("{id:guid}")]
    [ExigePermiso(PermisosDeIdentidad.UsuarioBloquear)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Bloquear(Guid id, CancellationToken cancelacion) =>
        ResponderSinContenido(await bloquear.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>Devuelve a un usuario bloqueado a la actividad.</summary>
    /// <param name="id">Identificador del usuario.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("{id:guid}/desbloqueo")]
    [ExigePermiso(PermisosDeIdentidad.UsuarioDesbloquear)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desbloquear(Guid id, CancellationToken cancelacion) =>
        ResponderSinContenido(await desbloquear.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>Cambia la contraseña PROPIA, presentando la actual.</summary>
    /// <remarks>
    /// La única acción autenticada del sistema que no exige permiso, y no es una excepción
    /// caprichosa: la autorización es saber la contraseña de ahora, que se presenta en el cuerpo.
    /// Sobre quién se opera no se pregunta —sale del <i>claim</i>—, así que aquí no se puede tocar
    /// la cuenta de otro ni escribiéndolo aposta.
    /// </remarks>
    /// <param name="peticion">La contraseña de ahora y la nueva.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPut("actual/contrasena")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CambiarContrasenaPropia(
        [FromBody] CambiarContrasenaDto peticion,
        CancellationToken cancelacion) =>
        ResponderSinContenido(await cambiarPropia.EjecutarAsync(peticion, cancelacion).ConfigureAwait(false));

    /// <summary>Le cambia la contraseña a otro usuario.</summary>
    /// <param name="id">Identificador del usuario.</param>
    /// <param name="peticion">La contraseña nueva.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPut("{id:guid}/contrasena")]
    [ExigePermiso(PermisosDeIdentidad.UsuarioCambiarContrasena)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Restablecer(
        Guid id,
        [FromBody] RestablecerContrasenaDto peticion,
        CancellationToken cancelacion) =>
        ResponderSinContenido(await restablecer.EjecutarAsync(id, peticion, cancelacion).ConfigureAwait(false));

    /// <summary>Devuelve las pertenencias de un usuario.</summary>
    /// <param name="id">Identificador del usuario.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{id:guid}/pertenencias")]
    [ExigePermiso(PermisosDeIdentidad.PertenenciaVer)]
    [ProducesResponseType(typeof(IReadOnlyList<MembresiaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Pertenencias(Guid id, CancellationToken cancelacion) =>
        Responder(await pertenencias.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>Da de alta a un usuario en una empresa.</summary>
    /// <param name="id">Identificador del usuario.</param>
    /// <param name="peticion">Empresa a la que se le da de alta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("{id:guid}/pertenencias")]
    [ExigePermiso(PermisosDeIdentidad.PertenenciaConceder)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Conceder(
        Guid id,
        [FromBody] ConcederPertenenciaDto peticion,
        CancellationToken cancelacion) =>
        ResponderSinContenido(await conceder.EjecutarAsync(id, peticion, cancelacion).ConfigureAwait(false));

    /// <summary>Da de baja a un usuario de una empresa.</summary>
    /// <param name="id">Identificador del usuario.</param>
    /// <param name="empresaId">Empresa de la que se le da de baja.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpDelete("{id:guid}/pertenencias/{empresaId:guid}")]
    [ExigePermiso(PermisosDeIdentidad.PertenenciaRetirar)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Retirar(Guid id, Guid empresaId, CancellationToken cancelacion) =>
        ResponderSinContenido(await retirar.EjecutarAsync(id, empresaId, cancelacion).ConfigureAwait(false));

    /// <summary>Asigna un rol a un usuario en una empresa.</summary>
    /// <param name="id">Identificador del usuario.</param>
    /// <param name="peticion">Empresa y rol.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("{id:guid}/roles")]
    [ExigePermiso(PermisosDeIdentidad.PertenenciaAsignarRol)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AsignarRol(
        Guid id,
        [FromBody] AsignarRolDto peticion,
        CancellationToken cancelacion) =>
        ResponderSinContenido(await asignarRol.EjecutarAsync(id, peticion, cancelacion).ConfigureAwait(false));

    /// <summary>Le retira un rol a un usuario en una empresa.</summary>
    /// <remarks>
    /// Permiso propio, distinto del de asignar: hacer y deshacer no son la misma facultad. Quien
    /// reparte roles no tiene por qué poder quitarle el suyo al administrador.
    /// </remarks>
    /// <param name="id">Identificador del usuario.</param>
    /// <param name="empresaId">Empresa en la que se le retira.</param>
    /// <param name="rolId">Rol que se le retira.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpDelete("{id:guid}/roles/{empresaId:guid}/{rolId:guid}")]
    [ExigePermiso(PermisosDeIdentidad.PertenenciaRetirarRol)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetirarRol(
        Guid id,
        Guid empresaId,
        Guid rolId,
        CancellationToken cancelacion) =>
        ResponderSinContenido(await retirarRol
            .EjecutarAsync(id, new AsignarRolDto { EmpresaId = empresaId, RolId = rolId }, cancelacion)
            .ConfigureAwait(false));
}
