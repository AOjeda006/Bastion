using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.Organizacion.Application.Unidades;
using Bastion.Organizacion.Contracts;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Unidades;
using Bastion.Organizacion.Endpoints.Comun;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Organizacion.Endpoints;

/// <summary>Unidades de medida, bajo <c>/api/v1/organizacion/unidades-de-medida</c>.</summary>
/// <remarks>Ruta escrita por lo mismo que en las cotizaciones.</remarks>
[Route(Prefijo + "/unidades-de-medida")]
public sealed class UnidadesDeMedidaController(
    ICrearUnidadMedida crear,
    IObtenerUnidadMedida obtener,
    IListarUnidadesDeMedida listar,
    IModificarUnidadMedida modificar) : ControladorDeOrganizacion
{
    /// <summary>Devuelve una página de unidades de medida.</summary>
    /// <param name="consulta">Paginación pedida (<c>page</c> y <c>size</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ExigePermiso(PermisosDeOrganizacion.UnidadMedidaVer)]
    [ProducesResponseType(typeof(PaginaDe<UnidadMedidaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] ConsultaPaginada consulta,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return Ok(await listar.EjecutarAsync(consulta.APaginacion(), cancelacion).ConfigureAwait(false));
    }

    /// <summary>Devuelve una unidad de medida.</summary>
    /// <param name="id">Identificador de la unidad.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{id:guid}")]
    [ExigePermiso(PermisosDeOrganizacion.UnidadMedidaVer)]
    [ProducesResponseType(typeof(UnidadMedidaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancelacion) =>
        ResponderConVersion(await obtener.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>Da de alta una unidad de medida.</summary>
    /// <param name="peticion">Datos de la unidad.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost]
    [AdmiteIdempotencia]
    [ExigePermiso(PermisosDeOrganizacion.UnidadMedidaCrear)]
    [ProducesResponseType(typeof(UnidadMedidaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearUnidadMedidaDto peticion,
        CancellationToken cancelacion) =>
        ResponderCreado(
            await crear.EjecutarAsync(peticion, cancelacion).ConfigureAwait(false),
            nameof(Obtener),
            unidad => unidad.Id);

    /// <summary>
    /// Cambia el nombre de una unidad de medida.
    /// </summary>
    /// <remarks>
    /// Los decimales no viajan en el cuerpo: bajarlos dejaría inválidas las existencias ya
    /// registradas con más precisión, sin tocarlas ni avisar.
    /// </remarks>
    /// <param name="id">Identificador de la unidad.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="peticion">Los datos nuevos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPut("{id:guid}")]
    [ExigePermiso(PermisosDeOrganizacion.UnidadMedidaModificar)]
    [ProducesResponseType(typeof(UnidadMedidaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Modificar(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] ModificarUnidadMedidaDto peticion,
        CancellationToken cancelacion) =>
        ResponderExigiendoVersionAsync(
            ifMatch,
            version => modificar.EjecutarAsync(id, version, peticion, cancelacion));
}
