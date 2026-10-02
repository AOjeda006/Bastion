using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.Catalogo.Application.Catalogo;
using Bastion.Catalogo.Contracts;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Endpoints.Comun;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Catalogo.Endpoints;

/// <summary>
/// Los códigos de barras de los artículos, bajo <c>/api/v1/catalogo/articulos</c> (ADR-0051).
/// </summary>
/// <remarks>
/// <para>
/// <b>Controlador propio bajo la ruta de otro recurso</b>, como <c>LoQueCuelgaDelTerceroController</c>
/// en Terceros: el código de barras cuelga de <c>/articulos</c>, pero sus cinco acciones no son del
/// artículo, y en <c>ArticulosController</c> lo habrían llevado a catorce dependencias. A diferencia
/// de aquel, cada fila tiene su versión y su <c>GET</c>, así que la baja cita la suya y no la del
/// artículo (ADR-0051 §6).
/// </para>
/// <para>
/// <b>El recurso se llama <c>gtins</c> y no <c>codigos-barras</c></b> (ADR-0052 §1): es el GTIN que
/// lleva el código, y así lo teclea quien lo busca. Las rutas las contestó el usuario en la puerta
/// del 2.10.
/// </para>
/// </remarks>
[Route(Prefijo + "/articulos")]
public sealed class CodigosBarrasController(
    IListarCodigosBarrasDelArticulo listar,
    IObtenerCodigoBarras obtener,
    IBuscarCodigoBarrasPorGtin buscar,
    IAgregarCodigoBarrasAlArticulo agregar,
    IQuitarCodigoBarras quitar) : ControladorDeCatalogo
{
    /// <summary>Devuelve los códigos de barras de un artículo, de la base a las agrupaciones.</summary>
    /// <remarks>Sin paginar: son los de UN artículo, que son unos pocos.</remarks>
    /// <param name="articuloId">Identificador del artículo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{articuloId:guid}/gtins")]
    [ExigePermiso(PermisosDeCatalogo.ArticuloVer)]
    [ProducesResponseType(typeof(IReadOnlyList<CodigoBarrasDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Listar(Guid articuloId, CancellationToken cancelacion) =>
        Responder(await listar.EjecutarAsync(articuloId, cancelacion).ConfigureAwait(false));

    /// <summary>Devuelve un código de barras, con la ETag que pide su baja.</summary>
    /// <param name="id">Identificador del código de barras.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("gtins/{id:guid}")]
    [ExigePermiso(PermisosDeCatalogo.ArticuloVer)]
    [ProducesResponseType(typeof(CodigoBarrasDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancelacion) =>
        ResponderConVersion(await obtener.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>Busca qué artículo lleva un GTIN: una lista de uno, o vacía.</summary>
    /// <remarks>
    /// <b>Lo que entra se normaliza</b>, así que el GTIN-12 y su forma de 13 encuentran lo mismo. Un
    /// texto que no es un GTIN —o ninguno— recibe el <c>400</c> de su motivo, con su <c>type</c>
    /// <c>gtin-…</c>, y no una lista vacía (ADR-0051 §7).
    /// </remarks>
    /// <param name="gtin">El GTIN, de 8, 12, 13 o 14 cifras.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("gtins")]
    [ExigePermiso(PermisosDeCatalogo.ArticuloVer)]
    [ProducesResponseType(typeof(IReadOnlyList<CodigoBarrasDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Buscar([FromQuery] string? gtin, CancellationToken cancelacion) =>
        Responder(await buscar.EjecutarAsync(gtin, cancelacion).ConfigureAwait(false));

    /// <summary>Da de alta un código de barras en un artículo.</summary>
    /// <remarks>
    /// El <c>400</c> dice por su <c>type</c> qué falla: el motivo del GTIN (<c>gtin-…</c>), el nivel
    /// o las unidades. El <c>409</c> <c>codigo-barras-duplicado</c> es el de un GTIN que ya lleva un
    /// artículo de la empresa, lo diga la comprobación previa o el índice único (ADR-0051 §5).
    /// </remarks>
    /// <param name="articuloId">Identificador del artículo.</param>
    /// <param name="peticion">El GTIN, su nivel y sus unidades.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("{articuloId:guid}/gtins")]
    [AdmiteIdempotencia]
    [ExigePermiso(PermisosDeCatalogo.CodigoBarrasAgregar)]
    [ProducesResponseType(typeof(CodigoBarrasDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Agregar(
        Guid articuloId,
        [FromBody] AgregarCodigoBarrasDto peticion,
        CancellationToken cancelacion) =>
        ResponderCreado(
            await agregar.EjecutarAsync(articuloId, peticion, cancelacion).ConfigureAwait(false),
            nameof(Obtener),
            codigo => codigo.Id);

    /// <summary>Quita un código de barras de su artículo.</summary>
    /// <remarks>
    /// Borra de verdad, y la empresa puede volver a dar de alta ese GTIN enseguida (ADR-0051 §8). La
    /// fila no cambia nunca, pero la baja exige su <c>If-Match</c>: nadie borra lo que no ha visto.
    /// </remarks>
    /// <param name="id">Identificador del código de barras.</param>
    /// <param name="ifMatch">Versión que se borra, tal como la devolvió el ETag.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpDelete("gtins/{id:guid}")]
    [ExigePermiso(PermisosDeCatalogo.CodigoBarrasQuitar)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Quitar(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancelacion) =>
        ResponderSinContenidoExigiendoVersionAsync(
            ifMatch,
            version => quitar.EjecutarAsync(id, version, cancelacion));
}
