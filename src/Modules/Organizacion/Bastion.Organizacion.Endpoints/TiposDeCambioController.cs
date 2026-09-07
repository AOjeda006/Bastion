using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Organizacion.Application.Divisas;
using Bastion.Organizacion.Contracts;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Divisas;
using Bastion.Organizacion.Endpoints.Comun;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Organizacion.Endpoints;

/// <summary>
/// Cotizaciones, bajo <c>/api/v1/organizacion/tipos-de-cambio</c>.
/// </summary>
/// <remarks>
/// Ruta escrita en vez de heredada: el host publica las URL en minúsculas, así que
/// <c>[controller]</c> daría <c>/tiposdecambio</c> —tres palabras pegadas— en un contrato que
/// después no se puede cambiar sin romper a quien ya lo use.
/// </remarks>
[Route(Prefijo + "/tipos-de-cambio")]
public sealed class TiposDeCambioController(
    ICrearTipoCambio crear,
    IObtenerTipoCambio obtener,
    IListarTiposDeCambio listar,
    IModificarTipoCambio modificar,
    IRetirarTipoCambio retirar,
    IReincorporarTipoCambio reincorporar) : ControladorDeOrganizacion
{
    /// <summary>Devuelve una página de cotizaciones, de la más reciente a la más antigua.</summary>
    /// <param name="consulta">
    /// Paginación, orden, filtro y retiradas (<c>page</c>, <c>size</c>, <c>sort</c>, <c>q</c>,
    /// <c>retiradas</c>). Por omisión, las retiradas <b>no</b> salen (ADR-0023).
    /// </param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ExigePermiso(PermisosDeOrganizacion.TipoCambioVer)]
    [ProducesResponseType(typeof(PaginaDe<TipoCambioDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] ConsultaDeMaestro consulta,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return await ResponderListadoAsync(consulta, listar, cancelacion).ConfigureAwait(false);
    }

    /// <summary>Devuelve una cotización.</summary>
    /// <param name="id">Identificador de la cotización.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{id:guid}")]
    [ExigePermiso(PermisosDeOrganizacion.TipoCambioVer)]
    [ProducesResponseType(typeof(TipoCambioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancelacion) =>
        ResponderConVersion(await obtener.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>Registra la cotización de un día.</summary>
    /// <param name="peticion">Datos de la cotización.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost]
    [AdmiteIdempotencia]
    [ExigePermiso(PermisosDeOrganizacion.TipoCambioCrear)]
    [ProducesResponseType(typeof(TipoCambioDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearTipoCambioDto peticion,
        CancellationToken cancelacion) =>
        ResponderCreado(
            await crear.EjecutarAsync(peticion, cancelacion).ConfigureAwait(false),
            nameof(Obtener),
            cambio => cambio.Id);

    /// <summary>Rectifica la tasa de una cotización.</summary>
    /// <param name="id">Identificador de la cotización.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="peticion">La tasa nueva.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPut("{id:guid}")]
    [ExigePermiso(PermisosDeOrganizacion.TipoCambioModificar)]
    [ProducesResponseType(typeof(TipoCambioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Modificar(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] ModificarTipoCambioDto peticion,
        CancellationToken cancelacion) =>
        ResponderExigiendoVersionAsync(
            ifMatch,
            version => modificar.EjecutarAsync(id, version, peticion, cancelacion));

    /// <summary>Retira una cotización: deja de ofrecerse para operaciones nuevas (ADR-0023).</summary>
    /// <remarks>
    /// Sub-recurso, como el cierre del ejercicio, y por lo mismo: retirar y reincorporar son poner
    /// y quitar la misma cosa. Lo que el ADR prohíbe para siempre es el <c>DELETE</c> del recurso
    /// entero, que no existe ni va a existir; esto borra la retirada, no la fila. El <c>GET</c> por
    /// identificador sigue devolviéndola después, al revés que una fila bloqueada.
    /// </remarks>
    /// <param name="id">Identificador de la cotización.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("{id:guid}/retirada")]
    [ExigePermiso(PermisosDeOrganizacion.TipoCambioRetirar)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Retirar(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancelacion) =>
        ResponderSinContenidoExigiendoVersionAsync(
            ifMatch,
            version => retirar.EjecutarAsync(id, version, cancelacion));

    /// <summary>Vuelve a ofrecer una cotización retirada.</summary>
    /// <remarks>
    /// Permiso propio y distinto del de retirar, como <c>cerrar</c>/<c>reabrir</c>: los cuatro
    /// maestros son de instalación (R8), así que retirar por error deja sin esa cotización a todas las
    /// empresas, y deshacerlo tiene que poder autorizarse aparte.
    /// </remarks>
    /// <param name="id">Identificador de la cotización.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpDelete("{id:guid}/retirada")]
    [ExigePermiso(PermisosDeOrganizacion.TipoCambioReincorporar)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Reincorporar(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancelacion) =>
        ResponderSinContenidoExigiendoVersionAsync(
            ifMatch,
            version => reincorporar.EjecutarAsync(id, version, cancelacion));
}
