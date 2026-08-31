using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.Organizacion.Application.Almacenes;
using Bastion.Organizacion.Contracts;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Endpoints.Comun;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Organizacion.Endpoints;

/// <summary>Almacenes, bajo <c>/api/v1/organizacion/almacenes</c>.</summary>
public sealed class AlmacenesController(
    ICrearAlmacen crear,
    IObtenerAlmacen obtener,
    IListarAlmacenes listar,
    IModificarAlmacen modificar,
    IBloquearAlmacen bloquear,
    IDesbloquearAlmacen desbloquear) : ControladorDeOrganizacion
{
    /// <summary>Devuelve una página de almacenes.</summary>
    /// <param name="consulta">Paginación pedida (<c>page</c> y <c>size</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ExigePermiso(PermisosDeOrganizacion.AlmacenVer)]
    [ProducesResponseType(typeof(PaginaDe<AlmacenDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] ConsultaPaginada consulta,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return Ok(await listar.EjecutarAsync(consulta.APaginacion(), cancelacion).ConfigureAwait(false));
    }

    /// <summary>Devuelve un almacén.</summary>
    /// <param name="id">Identificador del almacén.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{id:guid}")]
    [ExigePermiso(PermisosDeOrganizacion.AlmacenVer)]
    [ProducesResponseType(typeof(AlmacenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancelacion) =>
        ResponderConVersion(await obtener.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>Da de alta un almacén.</summary>
    /// <param name="peticion">Datos del almacén.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost]
    [AdmiteIdempotencia]
    [ExigePermiso(PermisosDeOrganizacion.AlmacenCrear)]
    [ProducesResponseType(typeof(AlmacenDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearAlmacenDto peticion,
        CancellationToken cancelacion) =>
        ResponderCreado(
            await crear.EjecutarAsync(peticion, cancelacion).ConfigureAwait(false),
            nameof(Obtener),
            almacen => almacen.Id);

    /// <summary>Cambia los datos de un almacén.</summary>
    /// <param name="id">Identificador del almacén.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="peticion">Los datos nuevos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPut("{id:guid}")]
    [ExigePermiso(PermisosDeOrganizacion.AlmacenModificar)]
    [ProducesResponseType(typeof(AlmacenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Modificar(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] ModificarAlmacenDto peticion,
        CancellationToken cancelacion) =>
        ResponderExigiendoVersionAsync(
            ifMatch,
            version => modificar.EjecutarAsync(id, version, peticion, cancelacion));

    /// <summary>
    /// Bloquea un almacén. No lo borra.
    /// </summary>
    /// <remarks>
    /// Cada movimiento de existencias apunta a su almacén para siempre: borrar la fila rompería el
    /// histórico de valoración, que no se puede reconstruir después.
    /// </remarks>
    /// <param name="id">Identificador del almacén.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpDelete("{id:guid}")]
    [ExigePermiso(PermisosDeOrganizacion.AlmacenBloquear)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Bloquear(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancelacion) =>
        ResponderSinContenidoExigiendoVersionAsync(
            ifMatch,
            version => bloquear.EjecutarAsync(id, version, cancelacion));

    /// <summary>Devuelve un almacén bloqueado a la operativa.</summary>
    /// <remarks>
    /// Sin puerta HTTP en el 0.4 por lo mismo que el desbloqueo de empresa, y abierta hoy por lo
    /// mismo: detrás de su permiso, que no es el de bloquear.
    /// </remarks>
    /// <param name="id">Identificador del almacén.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("{id:guid}/desbloqueo")]
    [ExigePermiso(PermisosDeOrganizacion.AlmacenDesbloquear)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Desbloquear(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancelacion) =>
        ResponderSinContenidoExigiendoVersionAsync(
            ifMatch,
            version => desbloquear.EjecutarAsync(id, version, cancelacion));
}
