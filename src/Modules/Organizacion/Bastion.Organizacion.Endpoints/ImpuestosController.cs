using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Organizacion.Application.Impuestos;
using Bastion.Organizacion.Contracts;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Impuestos;
using Bastion.Organizacion.Endpoints.Comun;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Organizacion.Endpoints;

/// <summary>Tramos de tipo impositivo, bajo <c>/api/v1/organizacion/impuestos</c>.</summary>
public sealed class ImpuestosController(
    ICrearImpuesto crear,
    IObtenerImpuesto obtener,
    IListarImpuestos listar,
    IModificarImpuesto modificar,
    ICerrarImpuesto cerrar) : ControladorDeOrganizacion
{
    /// <summary>Devuelve una página de tramos.</summary>
    /// <param name="consulta">Paginación, orden y filtro (<c>page</c>, <c>size</c>, <c>sort</c>, <c>q</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ExigePermiso(PermisosDeOrganizacion.ImpuestoVer)]
    [ProducesResponseType(typeof(PaginaDe<ImpuestoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] ConsultaPaginada consulta,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return await ResponderListadoAsync(consulta, listar, cancelacion).ConfigureAwait(false);
    }

    /// <summary>Devuelve un tramo.</summary>
    /// <param name="id">Identificador del tramo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{id:guid}")]
    [ExigePermiso(PermisosDeOrganizacion.ImpuestoVer)]
    [ProducesResponseType(typeof(ImpuestoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancelacion) =>
        ResponderConVersion(await obtener.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>
    /// Abre un tramo de un tipo impositivo.
    /// </summary>
    /// <remarks>
    /// Es un <c>POST</c> a la colección y no un <c>PUT</c> sobre el impuesto: subir el IVA general
    /// del 18 % al 21 % no cambia una fila, añade otra. Las facturas de agosto de 2012 siguen
    /// llevando el 18 % para siempre.
    /// </remarks>
    /// <param name="peticion">Datos del tramo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost]
    [AdmiteIdempotencia]
    [ExigePermiso(PermisosDeOrganizacion.ImpuestoCrear)]
    [ProducesResponseType(typeof(ImpuestoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearImpuestoDto peticion,
        CancellationToken cancelacion) =>
        ResponderCreado(
            await crear.EjecutarAsync(peticion, cancelacion).ConfigureAwait(false),
            nameof(Obtener),
            impuesto => impuesto.Id);

    /// <summary>Cambia el nombre y las cuentas contables de un tramo.</summary>
    /// <param name="id">Identificador del tramo.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="peticion">Los datos nuevos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPut("{id:guid}")]
    [ExigePermiso(PermisosDeOrganizacion.ImpuestoModificar)]
    [ProducesResponseType(typeof(ImpuestoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Modificar(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] ModificarImpuestoDto peticion,
        CancellationToken cancelacion) =>
        ResponderExigiendoVersionAsync(
            ifMatch,
            version => modificar.EjecutarAsync(id, version, peticion, cancelacion));

    /// <summary>
    /// Pone fecha de fin a un tramo vigente.
    /// </summary>
    /// <remarks>
    /// Sub-recurso y no un campo del <c>PUT</c>: cerrar un tramo deja al código sin tipo a partir
    /// del día siguiente, y eso va detrás de su propio permiso.
    /// </remarks>
    /// <param name="id">Identificador del tramo.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="peticion">El último día de vigencia.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("{id:guid}/cierre")]
    [ExigePermiso(PermisosDeOrganizacion.ImpuestoCerrar)]
    [ProducesResponseType(typeof(ImpuestoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Cerrar(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] CerrarImpuestoDto peticion,
        CancellationToken cancelacion) =>
        ResponderExigiendoVersionAsync(
            ifMatch,
            version => cerrar.EjecutarAsync(id, version, peticion, cancelacion));
}
