using Bastion.BuildingBlocks.Application.Exportacion;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Organizacion.Application;
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
    /// <summary>
    /// Las columnas de la exportación: lo que el listado publica de cada almacén, sin sus
    /// identificadores, y la dirección en sus seis campos.
    /// </summary>
    /// <remarks>
    /// Sin <c>id</c> ni <c>empresa_id</c>: la empresa es la de la sesión, la misma en todas las
    /// filas, y el identificador no lo enseña la pantalla. El tipo va con el valor del DTO, sin
    /// traducir (*La puerta del 3.2*).
    /// </remarks>
    internal static readonly HojaDeExportacion<AlmacenDto> HojaDeAlmacenes = new(
        "almacenes",
        [
            new("codigo", almacen => CampoCsv.Texto(almacen.Codigo)),
            new("nombre", almacen => CampoCsv.Texto(almacen.Nombre)),
            new("tipo", almacen => CampoCsv.Valor(almacen.Tipo)),
            .. ColumnasDeDireccion.De<AlmacenDto>("direccion", almacen => almacen.Direccion),
        ]);

    /// <summary>Devuelve una página de almacenes.</summary>
    /// <param name="pagina">La página (<c>page</c>, <c>size</c>).</param>
    /// <param name="consulta">El orden (<c>sort</c>).</param>
    /// <param name="texto">El filtro de texto (<c>q</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ExigePermiso(PermisosDeOrganizacion.AlmacenVer)]
    [ProducesResponseType(typeof(PaginaDe<AlmacenDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] PaginaPedida pagina,
        [FromQuery] ConsultaDeListado consulta,
        [FromQuery] FiltroDeTexto texto,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return await ResponderListadoAsync(pagina, consulta, texto, listar, cancelacion)
            .ConfigureAwait(false);
    }

    /// <summary>Descarga el listado de almacenes entero, con su orden y su texto, en CSV (ADR-0073).</summary>
    /// <param name="consulta">El orden (<c>sort</c>).</param>
    /// <param name="texto">El filtro de texto (<c>q</c>).</param>
    /// <param name="foto">La foto del módulo, de la que salen todas las páginas.</param>
    /// <param name="reloj">El reloj, que da el día del nombre del fichero.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("exportacion")]
    [ExigePermiso(PermisosDeOrganizacion.AlmacenVer)]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK, "text/csv")]
    public async Task<IActionResult> Exportar(
        [FromQuery] ConsultaDeListado consulta,
        [FromQuery] FiltroDeTexto texto,
        [FromServices] IFotoDeOrganizacion foto,
        [FromServices] TimeProvider reloj,
        CancellationToken cancelacion) =>
        await ExportarListadoAsync(consulta, texto, listar, foto, HojaDeAlmacenes, reloj, cancelacion)
            .ConfigureAwait(false);

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
    /// <remarks>
    /// <b>Es la única escritura del sistema sin <c>If-Match</c>.</b> No es un descuido ni una
    /// excepción de comodidad: desde el 0.10 un recurso bloqueado no se lee por ningún camino
    /// ordinario, y el <c>ETag</c> se obtiene leyendo. Exigir aquí una versión sería exigir una
    /// llave que no se puede conseguir, y dejaría la puerta cerrada para siempre. Lo que el
    /// <c>If-Match</c> evita —pisar el cambio de otro— aquí no puede pasar: mientras el recurso
    /// está bloqueado ninguna otra escritura llega hasta él, y desbloquear dos veces deja el mismo
    /// resultado que desbloquear una (ADR-0017).
    /// </remarks>
    /// <param name="id">Identificador del almacén.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("{id:guid}/desbloqueo")]
    [ExigePermiso(PermisosDeOrganizacion.AlmacenDesbloquear)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desbloquear(Guid id, CancellationToken cancelacion) =>
        ResponderSinContenido(await desbloquear.EjecutarAsync(id, cancelacion).ConfigureAwait(false));
}
