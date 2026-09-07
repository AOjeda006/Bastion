using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.BuildingBlocks.Infrastructure.Listados;
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
    IModificarUnidadMedida modificar,
    IRetirarUnidadMedida retirar,
    IReincorporarUnidadMedida reincorporar) : ControladorDeOrganizacion
{
    /// <summary>Devuelve una página de unidades de medida.</summary>
    /// <param name="consulta">
    /// Paginación, orden, filtro y retiradas (<c>page</c>, <c>size</c>, <c>sort</c>, <c>q</c>,
    /// <c>retiradas</c>). Por omisión, las retiradas <b>no</b> salen (ADR-0023).
    /// </param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ExigePermiso(PermisosDeOrganizacion.UnidadMedidaVer)]
    [ProducesResponseType(typeof(PaginaDe<UnidadMedidaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] ConsultaDeMaestro consulta,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return await ResponderListadoAsync(consulta, listar, cancelacion).ConfigureAwait(false);
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

    /// <summary>Retira una unidad de medida: deja de ofrecerse para operaciones nuevas (ADR-0023).</summary>
    /// <remarks>
    /// Sub-recurso, como el cierre del ejercicio, y por lo mismo: retirar y reincorporar son poner
    /// y quitar la misma cosa. Lo que el ADR prohíbe para siempre es el <c>DELETE</c> del recurso
    /// entero, que no existe ni va a existir; esto borra la retirada, no la fila. El <c>GET</c> por
    /// identificador sigue devolviéndola después, al revés que una fila bloqueada.
    /// </remarks>
    /// <param name="id">Identificador de la unidad.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("{id:guid}/retirada")]
    [ExigePermiso(PermisosDeOrganizacion.UnidadMedidaRetirar)]
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

    /// <summary>Vuelve a ofrecer una unidad de medida retirada.</summary>
    /// <remarks>
    /// Permiso propio y distinto del de retirar, como <c>cerrar</c>/<c>reabrir</c>: los cuatro
    /// maestros son de instalación (R8), así que retirar por error deja sin esa unidad a todas las
    /// empresas, y deshacerlo tiene que poder autorizarse aparte.
    /// </remarks>
    /// <param name="id">Identificador de la unidad.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpDelete("{id:guid}/retirada")]
    [ExigePermiso(PermisosDeOrganizacion.UnidadMedidaReincorporar)]
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
