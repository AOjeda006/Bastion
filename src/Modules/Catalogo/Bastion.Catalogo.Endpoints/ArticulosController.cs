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
    IModificarArticulo modificar,
    IListarProveedoresDelArticulo listarProveedores,
    IObtenerProveedorDelArticulo obtenerProveedor,
    IAgregarProveedorAlArticulo agregarProveedor,
    IModificarProveedorDelArticulo modificarProveedor,
    IQuitarProveedorDelArticulo quitarProveedor) : ControladorDeCatalogo
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

    /// <summary>Devuelve quiénes suministran un artículo.</summary>
    /// <remarks>
    /// <para>
    /// <b>Sin paginar, y a propósito.</b> Lo que sale de aquí va filtrado por el bloqueo del
    /// tercero al que apunta cada fila (art. 32 de la LOPDGDD, R16), y ese filtro no cabe en el
    /// <c>WHERE</c>: el bloqueo está en otro esquema. Paginar y filtrar después daría páginas de
    /// tamaño variable y un total que miente —y restando de un total que miente se cuenta cuántos
    /// hay bloqueados—. Lo que acota el tamaño es el modelo: son los proveedores de UN artículo.
    /// </para>
    /// <para>
    /// <b>Y de cada proveedor sale su identificador y nada más de él.</b> La ficha es de Terceros y
    /// se pide allí, que es quien sabe además si puede enseñarla.
    /// </para>
    /// </remarks>
    /// <param name="articuloId">Identificador del artículo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{articuloId:guid}/proveedores")]
    [ExigePermiso(PermisosDeCatalogo.ArticuloVer)]
    [ProducesResponseType(typeof(IReadOnlyList<ArticuloProveedorDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListarProveedores(
        Guid articuloId,
        CancellationToken cancelacion) =>
        Responder(
            await listarProveedores.EjecutarAsync(articuloId, cancelacion).ConfigureAwait(false));

    /// <summary>Devuelve un suministro concreto.</summary>
    /// <param name="id">Identificador del suministro.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("proveedores/{id:guid}")]
    [ExigePermiso(PermisosDeCatalogo.ArticuloVer)]
    [ProducesResponseType(typeof(ArticuloProveedorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerProveedor(Guid id, CancellationToken cancelacion) =>
        ResponderConVersion(
            await obtenerProveedor.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>Declara que un tercero suministra este artículo.</summary>
    /// <remarks>
    /// <b>El <c>400</c> de <c>articulo-proveedor-tercero-no-valido</c> es UNO para cuatro casos</b>
    /// —el tercero no existe, es de otra empresa, está bloqueado, o no es proveedor— y no lleva
    /// parámetros. Distinguirlos convertiría este formulario en el censo de las bajas del artículo
    /// 32: cualquiera con este permiso podría recorrer identificadores y separar los que no existen
    /// de los que existen y están reservados. El <c>409</c> —<c>articulo-proveedor-duplicado</c>—
    /// solo sale para un tercero que se puede tratar, porque el estado se pregunta antes: un
    /// bloqueado que ya suministraba el artículo contesta el mismo <c>400</c>, y así el alta no
    /// cuenta lo que el listado calla.
    /// </remarks>
    /// <param name="articuloId">Identificador del artículo.</param>
    /// <param name="peticion">El tercero y su referencia.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("{articuloId:guid}/proveedores")]
    [AdmiteIdempotencia]
    [ExigePermiso(PermisosDeCatalogo.ArticuloProveedorAgregar)]
    [ProducesResponseType(typeof(ArticuloProveedorDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AgregarProveedor(
        Guid articuloId,
        [FromBody] AgregarProveedorDto peticion,
        CancellationToken cancelacion) =>
        ResponderCreado(
            await agregarProveedor.EjecutarAsync(articuloId, peticion, cancelacion)
                .ConfigureAwait(false),
            nameof(ObtenerProveedor),
            suministro => suministro.Id);

    /// <summary>Cambia la referencia con la que el proveedor llama a este artículo.</summary>
    /// <remarks>
    /// Ni el artículo ni el tercero están entre lo que se puede cambiar, y no lo impide el permiso:
    /// no están en el cuerpo ni en el agregado. Cambiar cualquiera de los dos sería otro suministro.
    /// </remarks>
    /// <param name="id">Identificador del suministro.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="peticion">La referencia nueva, o vacía para dejar de tener una.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPut("proveedores/{id:guid}")]
    [ExigePermiso(PermisosDeCatalogo.ArticuloProveedorModificar)]
    [ProducesResponseType(typeof(ArticuloProveedorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> ModificarProveedor(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] ModificarProveedorDto peticion,
        CancellationToken cancelacion) =>
        ResponderExigiendoVersionAsync(
            ifMatch,
            version => modificarProveedor.EjecutarAsync(id, version, peticion, cancelacion));

    /// <summary>Quita un proveedor de este artículo.</summary>
    /// <remarks>
    /// <b>El único <c>DELETE</c> del módulo, y borra de verdad.</b> Lo que desaparece no es la
    /// ficha de nadie: es un hecho entre dos que ha dejado de ser verdad. El rastro de que existió,
    /// y de quién lo quitó, está en la traza (ADR-0012).
    /// </remarks>
    /// <param name="id">Identificador del suministro.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpDelete("proveedores/{id:guid}")]
    [ExigePermiso(PermisosDeCatalogo.ArticuloProveedorQuitar)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> QuitarProveedor(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancelacion) =>
        ResponderSinContenidoExigiendoVersionAsync(
            ifMatch,
            version => quitarProveedor.EjecutarAsync(id, version, cancelacion));
}
