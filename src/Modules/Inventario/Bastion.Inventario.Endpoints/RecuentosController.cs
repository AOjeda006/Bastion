using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.Inventario.Application.Recuentos;
using Bastion.Inventario.Contracts;
using Bastion.Inventario.Contracts.Recuentos;
using Bastion.Inventario.Endpoints.Comun;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Inventario.Endpoints;

/// <summary>Recuentos de inventario, bajo <c>/api/v1/inventario/recuentos</c>.</summary>
/// <remarks>
/// <para>
/// <b>La primera superficie de lectura del módulo</b>: el recuento se cuenta en una pantalla, así que
/// necesita lista, ficha y líneas, que ningún documento de Inventario tenía (ADR-0055).
/// </para>
/// <para>
/// <b>Un permiso por acción</b> (ADR-0055 §13): ver, abrir y, con sus acciones, contar, confirmar,
/// anular y descartar. Quien cuenta en el almacén no tiene por qué poder confirmar lo que mueve el
/// libro.
/// </para>
/// </remarks>
/// <param name="abrir">El alta, con sus líneas precargadas.</param>
/// <param name="obtener">La ficha, con su versión.</param>
/// <param name="listar">La lista de recuentos.</param>
/// <param name="listarLineas">La página de líneas de un recuento.</param>
/// <param name="obtenerLinea">Una línea, con su versión.</param>
public sealed class RecuentosController(
    IAbrirRecuento abrir,
    IObtenerRecuento obtener,
    IListarRecuentos listar,
    IListarLineasDeRecuento listarLineas,
    IObtenerLineaDeRecuento obtenerLinea) : ControladorDeInventario
{
    /// <summary>Devuelve una página de recuentos.</summary>
    /// <param name="consulta">
    /// Paginación, orden, filtro, estado y almacén (<c>page</c>, <c>size</c>, <c>sort</c>, <c>q</c>,
    /// <c>estado</c>, <c>almacen</c>). El filtro de texto mira el motivo.
    /// </param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ExigePermiso(PermisosDeInventario.RecuentoVer)]
    [ProducesResponseType(typeof(PaginaDe<RecuentoResumenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Listar(
        [FromQuery] ConsultaDeRecuentos consulta,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return await ResponderListadoAsync(
            consulta,
            listar,
            (paginacion, testigo) => listar.EjecutarAsync(paginacion, consulta.Estado, consulta.Almacen, testigo),
            cancelacion).ConfigureAwait(false);
    }

    /// <summary>Devuelve la ficha de un recuento, con su <c>ETag</c>.</summary>
    /// <remarks>
    /// Lleva siempre cuántas líneas quedan sin contar. Mientras está en curso lleva además las dos
    /// cuentas del teórico de ahora —con el teórico cambiado y con tránsito— y la huella del teórico,
    /// que es la que pide la confirmación (ADR-0055 §2).
    /// </remarks>
    /// <param name="id">Identificador del recuento.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{id:guid}")]
    [ExigePermiso(PermisosDeInventario.RecuentoVer)]
    [ProducesResponseType(typeof(RecuentoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancelacion) =>
        ResponderConVersion(await obtener.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>Abre el recuento de un almacén entero, con sus claves precargadas y sin contar.</summary>
    /// <remarks>
    /// <para>
    /// El <c>409</c> puede venir de tres sitios, y el <c>type</c> los separa: el almacén ya tiene uno
    /// en curso (<c>recuento-ya-hay-uno-en-curso</c>), el almacén está bloqueado
    /// (<c>recuento-almacen-bloqueado</c>) o una de las dos series está cerrada
    /// (<c>recuento-serie-cerrada</c>).
    /// </para>
    /// <para>
    /// <b>La <c>Idempotency-Key</c> se admite y no se exige</b>, como en las altas de los maestros: el
    /// alta no numera, así que no hay hueco que evitar, y el reintento sin clave lo para el índice
    /// de uno en curso por almacén.
    /// </para>
    /// </remarks>
    /// <param name="peticion">Qué almacén, con qué series y por qué.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost]
    [AdmiteIdempotencia]
    [ExigePermiso(PermisosDeInventario.RecuentoAbrir)]
    [ProducesResponseType(typeof(RecuentoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Abrir(
        [FromBody] AbrirRecuentoDto peticion,
        CancellationToken cancelacion) =>
        ResponderCreado(
            await abrir.EjecutarAsync(peticion, cancelacion).ConfigureAwait(false),
            nameof(Obtener),
            recuento => recuento.Id);

    /// <summary>Devuelve una página de las líneas de un recuento, con su teórico.</summary>
    /// <param name="id">Identificador del recuento.</param>
    /// <param name="consulta">
    /// Paginación, orden, filtro y acotado (<c>page</c>, <c>size</c>, <c>sort</c>, <c>q</c>,
    /// <c>solo</c>). Se ordena por <c>numero</c>, y el filtro de texto mira el lote y la serie.
    /// </param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{id:guid}/lineas")]
    [ExigePermiso(PermisosDeInventario.RecuentoVer)]
    [ProducesResponseType(typeof(PaginaDe<LineaDeRecuentoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListarLineas(
        Guid id,
        [FromQuery] ConsultaDeLineasDeRecuento consulta,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return await ResponderListadoDeResultadoAsync(
            consulta,
            listarLineas,
            (paginacion, testigo) => listarLineas.EjecutarAsync(paginacion, id, consulta.Solo, testigo),
            cancelacion).ConfigureAwait(false);
    }

    /// <summary>Devuelve una línea de un recuento, con su <c>ETag</c>.</summary>
    /// <remarks>
    /// La versión es la de la línea, que es la que pide contarla o quitarla (ADR-0055 §4).
    /// </remarks>
    /// <param name="id">Identificador del recuento.</param>
    /// <param name="lineaId">Identificador de la línea.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{id:guid}/lineas/{lineaId:guid}")]
    [ExigePermiso(PermisosDeInventario.RecuentoVer)]
    [ProducesResponseType(typeof(LineaDeRecuentoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerLinea(Guid id, Guid lineaId, CancellationToken cancelacion) =>
        ResponderConVersion(await obtenerLinea.EjecutarAsync(id, lineaId, cancelacion).ConfigureAwait(false));
}
