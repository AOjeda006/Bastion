using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.Catalogo.Application.Catalogo;
using Bastion.Catalogo.Contracts;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Endpoints.Comun;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Catalogo.Endpoints;

/// <summary>Artículos, bajo <c>/api/v1/catalogo/articulos</c>.</summary>
public sealed class ArticulosController(
    ICrearArticulo crear,
    IObtenerArticulo obtener,
    IListarArticulos listar,
    IModificarArticulo modificar) : ControladorDeCatalogo
{
    /// <summary>Devuelve una página de artículos.</summary>
    /// <param name="consulta">
    /// Paginación, orden, filtro y categoría (<c>page</c>, <c>size</c>, <c>sort</c>, <c>q</c>,
    /// <c>categoria</c>). El filtro de texto mira el código y la descripción.
    /// </param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ExigePermiso(PermisosDeCatalogo.ArticuloVer)]
    [ProducesResponseType(typeof(PaginaDe<ArticuloDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Listar(
        [FromQuery] ConsultaDeArticulos consulta,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return await ResponderListadoAsync(
            consulta,
            listar,
            (paginacion, testigo) => listar.EjecutarAsync(paginacion, consulta.Categoria, testigo),
            cancelacion).ConfigureAwait(false);
    }

    /// <summary>Devuelve un artículo.</summary>
    /// <param name="id">Identificador del artículo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{id:guid}")]
    [ExigePermiso(PermisosDeCatalogo.ArticuloVer)]
    [ProducesResponseType(typeof(ArticuloDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancelacion) =>
        ResponderConVersion(await obtener.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>Da de alta un artículo.</summary>
    /// <remarks>
    /// El <c>409</c> puede venir de tres sitios distintos, y el <c>type</c> del ProblemDetails los
    /// separa: el código ya está usado (<c>articulo-duplicado</c>), la unidad está retirada
    /// (<c>articulo-unidad-retirada</c>) o el tramo de impuesto no rige hoy
    /// (<c>articulo-impuesto-no-vigente</c>). El frontal escribe el texto humano a partir del
    /// <c>type</c> (ADR-0030), no del mensaje.
    /// </remarks>
    /// <param name="peticion">Datos del artículo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost]
    [AdmiteIdempotencia]
    [ExigePermiso(PermisosDeCatalogo.ArticuloCrear)]
    [ProducesResponseType(typeof(ArticuloDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearArticuloDto peticion,
        CancellationToken cancelacion) =>
        ResponderCreado(
            await crear.EjecutarAsync(peticion, cancelacion).ConfigureAwait(false),
            nameof(Obtener),
            articulo => articulo.Id);

    /// <summary>Cambia la descripción, el tipo, el impuesto propuesto o la categoría.</summary>
    /// <remarks>
    /// Ni el código ni la unidad base están entre lo que se puede cambiar, y no es el permiso
    /// quien lo impide: no están en el cuerpo ni en <c>Articulo.Modificar</c>.
    /// </remarks>
    /// <param name="id">Identificador del artículo.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="peticion">Los datos nuevos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPut("{id:guid}")]
    [ExigePermiso(PermisosDeCatalogo.ArticuloModificar)]
    [ProducesResponseType(typeof(ArticuloDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Modificar(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] ModificarArticuloDto peticion,
        CancellationToken cancelacion) =>
        ResponderExigiendoVersionAsync(
            ifMatch,
            version => modificar.EjecutarAsync(id, version, peticion, cancelacion));
}
