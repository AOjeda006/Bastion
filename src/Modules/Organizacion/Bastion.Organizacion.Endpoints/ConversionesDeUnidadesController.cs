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
    IModificarConversionUm modificar,
    IRetirarConversionUm retirar,
    IReincorporarConversionUm reincorporar,
    IResolverConversionUm resolver) : ControladorDeOrganizacion
{
    /// <summary>Devuelve una página de conversiones.</summary>
    /// <param name="consulta">
    /// Paginación, orden, filtro y retiradas (<c>page</c>, <c>size</c>, <c>sort</c>, <c>q</c>,
    /// <c>retiradas</c>). Por omisión, las retiradas <b>no</b> salen (ADR-0023).
    /// </param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ExigePermiso(PermisosDeOrganizacion.ConversionUmVer)]
    [ProducesResponseType(typeof(PaginaDe<ConversionUmDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] ConsultaDeMaestro consulta,
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

    /// <summary>
    /// Resuelve la conversión declarada entre dos unidades, o falla con nombre (ADR-0023).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Un par no declarado es un 404 con nombre, y nunca un número.</b> Con <c>kg→g</c> y
    /// <c>g→mg</c> dados de alta, preguntar <c>kg→mg</c> responde
    /// <c>conversion-um-no-declarada</c>, no <c>1000000</c>: encadenar dos factores redondeados
    /// multiplica el error, y el sistema no inventa una conversión que nadie declaró. Tampoco
    /// devuelve cero ni nulo, que son las otras dos maneras de que el error salga a la superficie
    /// convertido en una cantidad.
    /// </para>
    /// <para>
    /// <c>resolucion</c> y no <c>{id:guid}</c>: el segmento no casa con la restricción de la ruta
    /// de al lado, así que las dos conviven sin ambigüedad.
    /// </para>
    /// <para>
    /// Permiso de lectura, el mismo que ver conversiones: resolver es leer la fila que ya está
    /// publicada en la colección, y una fila retirada también resuelve —eso es exactamente lo que
    /// «sigue resolviendo para lo que ya apunta a ella» significa—, así que la respuesta dice si lo
    /// está.
    /// </para>
    /// </remarks>
    /// <param name="origen">Unidad de la que se parte.</param>
    /// <param name="destino">Unidad a la que se llega.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("resolucion")]
    [ExigePermiso(PermisosDeOrganizacion.ConversionUmVer)]
    [ProducesResponseType(typeof(ResolucionDeConversionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resolver(
        [FromQuery] Guid origen,
        [FromQuery] Guid destino,
        CancellationToken cancelacion) =>
        Responder(await resolver.EjecutarAsync(origen, destino, cancelacion).ConfigureAwait(false));

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

    /// <summary>Retira una conversión: deja de ofrecerse para operaciones nuevas (ADR-0023).</summary>
    /// <remarks>
    /// Sub-recurso, como el cierre del ejercicio, y por lo mismo: retirar y reincorporar son poner
    /// y quitar la misma cosa. Lo que el ADR prohíbe para siempre es el <c>DELETE</c> del recurso
    /// entero, que no existe ni va a existir; esto borra la retirada, no la fila. El <c>GET</c> por
    /// identificador sigue devolviéndola después, al revés que una fila bloqueada.
    /// </remarks>
    /// <param name="id">Identificador de la conversión.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("{id:guid}/retirada")]
    [ExigePermiso(PermisosDeOrganizacion.ConversionUmRetirar)]
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

    /// <summary>Vuelve a ofrecer una conversión retirada.</summary>
    /// <remarks>
    /// Permiso propio y distinto del de retirar, como <c>cerrar</c>/<c>reabrir</c>: los cuatro
    /// maestros son de instalación (R8), así que retirar por error deja sin esa conversión a todas las
    /// empresas, y deshacerlo tiene que poder autorizarse aparte.
    /// </remarks>
    /// <param name="id">Identificador de la conversión.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpDelete("{id:guid}/retirada")]
    [ExigePermiso(PermisosDeOrganizacion.ConversionUmReincorporar)]
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
