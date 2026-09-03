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

/// <summary>
/// Conversiones entre unidades, bajo <c>/api/v1/organizacion/conversiones-de-unidades</c>.
/// </summary>
/// <remarks>
/// <para>Ruta escrita por lo mismo que en las cotizaciones.</para>
/// <para>
/// Colección propia y no un sub-recurso de la unidad de origen: una conversión relaciona DOS
/// unidades y ninguna de las dos la contiene. Colgarla de una de ellas daría a entender que la
/// otra es un detalle suyo, y la de vuelta —que hay que dar de alta aparte— quedaría en otro sitio.
/// </para>
/// </remarks>
[Route(Prefijo + "/conversiones-de-unidades")]
public sealed class ConversionesDeUnidadesController(
    ICrearConversionUm crear,
    IObtenerConversionUm obtener,
    IListarConversionesUm listar,
    IModificarConversionUm modificar) : ControladorDeOrganizacion
{
    /// <summary>Devuelve una página de conversiones.</summary>
    /// <param name="consulta">Paginación, orden y filtro (<c>page</c>, <c>size</c>, <c>sort</c>, <c>q</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ExigePermiso(PermisosDeOrganizacion.ConversionUmVer)]
    [ProducesResponseType(typeof(PaginaDe<ConversionUmDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] ConsultaPaginada consulta,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return await ResponderListadoAsync(consulta, listar, cancelacion).ConfigureAwait(false);
    }

    /// <summary>Devuelve una conversión.</summary>
    /// <param name="id">Identificador de la conversión.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{id:guid}")]
    [ExigePermiso(PermisosDeOrganizacion.ConversionUmVer)]
    [ProducesResponseType(typeof(ConversionUmDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancelacion) =>
        ResponderConVersion(await obtener.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>Da de alta una conversión entre dos unidades.</summary>
    /// <param name="peticion">Datos de la conversión.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost]
    [AdmiteIdempotencia]
    [ExigePermiso(PermisosDeOrganizacion.ConversionUmCrear)]
    [ProducesResponseType(typeof(ConversionUmDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearConversionUmDto peticion,
        CancellationToken cancelacion) =>
        ResponderCreado(
            await crear.EjecutarAsync(peticion, cancelacion).ConfigureAwait(false),
            nameof(Obtener),
            conversion => conversion.Id);

    /// <summary>Corrige el factor de una conversión.</summary>
    /// <param name="id">Identificador de la conversión.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="peticion">El factor nuevo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPut("{id:guid}")]
    [ExigePermiso(PermisosDeOrganizacion.ConversionUmModificar)]
    [ProducesResponseType(typeof(ConversionUmDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Modificar(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] ModificarConversionUmDto peticion,
        CancellationToken cancelacion) =>
        ResponderExigiendoVersionAsync(
            ifMatch,
            version => modificar.EjecutarAsync(id, version, peticion, cancelacion));
}
