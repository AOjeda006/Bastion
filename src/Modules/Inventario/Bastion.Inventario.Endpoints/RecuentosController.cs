using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Errores;
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
/// <b>Un permiso por acción</b> (ADR-0055 §13 y ADR-0056): ver, abrir, contar, añadir y quitar una
/// línea y, con sus acciones, confirmar, anular y descartar. Quien cuenta en el almacén no tiene por
/// qué poder confirmar lo que mueve el libro, ni decidir qué claves entran en el recuento.
/// </para>
/// <para>
/// <b>Las escrituras en una línea llevan la versión de la línea</b>, y no la de la cabecera
/// (ADR-0055 §4).
/// </para>
/// <para>
/// <b>Las acciones de la cabecera piden los dos mecanismos</b>, el <c>If-Match</c> y la
/// <c>Idempotency-Key</c>, y responden sin <c>ETag</c> (ADR-0057): una respuesta repetida desde la
/// caché llevaría una versión que ya no es la de la fila.
/// </para>
/// </remarks>
/// <param name="abrir">El alta, con sus líneas precargadas.</param>
/// <param name="obtener">La ficha, con su versión.</param>
/// <param name="listar">La lista de recuentos.</param>
/// <param name="listarLineas">La página de líneas de un recuento.</param>
/// <param name="obtenerLinea">Una línea, con su versión.</param>
/// <param name="contar">Anota lo contado en una línea.</param>
/// <param name="anadir">Añade una clave que la precarga no traía.</param>
/// <param name="quitar">Quita una línea que no se va a contar.</param>
/// <param name="confirmar">Confirma el recuento, con el ajuste de su diferencia.</param>
/// <param name="anular">Anula el recuento confirmado, con el ajuste de su diferencia.</param>
/// <param name="descartar">Descarta el recuento en curso.</param>
public sealed class RecuentosController(
    IAbrirRecuento abrir,
    IObtenerRecuento obtener,
    IListarRecuentos listar,
    IListarLineasDeRecuento listarLineas,
    IObtenerLineaDeRecuento obtenerLinea,
    IContarLineaDeRecuento contar,
    IAnadirLineaDeRecuento anadir,
    IQuitarLineaDeRecuento quitar,
    IConfirmarRecuento confirmar,
    IAnularRecuento anular,
    IDescartarRecuento descartar) : ControladorDeInventario
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

    /// <summary>Anota lo contado en una línea, con el <c>If-Match</c> de la línea.</summary>
    /// <remarks>
    /// <para>
    /// <b>La versión que se exige es la de la línea, y cambia además la de la cabecera</b> (ADR-0055
    /// §4): dos personas que cuentan líneas distintas a la vez se esperan un instante en la cabecera, y
    /// ninguna recibe un <c>412</c> por lo que hizo la otra.
    /// </para>
    /// <para>
    /// Un recuento ya confirmado, anulado o descartado da un <c>409</c>
    /// (<c>recuento-no-esta-en-curso</c>). La respuesta no lleva <c>ETag</c>: la versión nueva de la
    /// línea se lee con ella.
    /// </para>
    /// </remarks>
    /// <param name="id">Identificador del recuento.</param>
    /// <param name="lineaId">Identificador de la línea.</param>
    /// <param name="ifMatch">Versión de la línea sobre la que se cuenta, tal como la devolvió el ETag.</param>
    /// <param name="peticion">Lo contado, en la unidad base del artículo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPut("{id:guid}/lineas/{lineaId:guid}")]
    [ExigePermiso(PermisosDeInventario.RecuentoContar)]
    [ProducesResponseType(typeof(LineaDeRecuentoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Contar(
        Guid id,
        Guid lineaId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] ContarLineaDeRecuentoDto peticion,
        CancellationToken cancelacion) =>
        ResponderExigiendoVersionAsync(
            ifMatch,
            version => contar.EjecutarAsync(id, lineaId, version, peticion, cancelacion));

    /// <summary>Añade una clave que la precarga no traía, sin contar.</summary>
    /// <remarks>
    /// <para>
    /// La ubicación tiene que ser del almacén del recuento, el artículo tiene que almacenarse, y el
    /// lote o el número de serie tienen que casar con su marca (ADR-0055 §6). El <c>409</c> puede ser
    /// la clave repetida (<c>recuento-clave-repetida</c>), el mismo número de serie en otra ubicación
    /// (<c>recuento-serie-repetida</c>), la marca (<c>recuento-trazabilidad-no-casa</c>), un maestro
    /// bloqueado o un recuento que ya no está en curso.
    /// </para>
    /// <para>
    /// <b>La <c>Idempotency-Key</c> se admite y no se exige</b>: añadir no numera, y el reintento sin
    /// clave lo para la clave repetida.
    /// </para>
    /// </remarks>
    /// <param name="id">Identificador del recuento.</param>
    /// <param name="peticion">La clave, y su coste si se sabe.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("{id:guid}/lineas")]
    [AdmiteIdempotencia]
    [ExigePermiso(PermisosDeInventario.RecuentoAgregarLinea)]
    [ProducesResponseType(typeof(LineaDeRecuentoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AnadirLinea(
        Guid id,
        [FromBody] AnadirLineaDeRecuentoDto peticion,
        CancellationToken cancelacion)
    {
        Resultado<LineaDeRecuentoDto> resultado =
            await anadir.EjecutarAsync(id, peticion, cancelacion).ConfigureAwait(false);

        // LA RUTA DE LA LÍNEA LLEVA DOS IDENTIFICADORES, y el ayudante de las creaciones solo sabe
        // poner uno: por eso el `201` se compone aquí.
        return resultado.EsCorrecto
            ? CreatedAtAction(nameof(ObtenerLinea), new { id, lineaId = resultado.Valor.Id }, resultado.Valor)
            : resultado.Error!.AResultadoDeAccion();
    }

    /// <summary>Quita una línea que no se va a contar, con el <c>If-Match</c> de la línea.</summary>
    /// <remarks>
    /// <b>Su clave queda como está</b> (ADR-0055 §5): el recuento solo dice algo de las claves que
    /// lleva. Quitarla es la manera de confirmar sin contarla, porque una línea sin contar no es un
    /// cero.
    /// </remarks>
    /// <param name="id">Identificador del recuento.</param>
    /// <param name="lineaId">Identificador de la línea.</param>
    /// <param name="ifMatch">Versión de la línea que se quita, tal como la devolvió el ETag.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpDelete("{id:guid}/lineas/{lineaId:guid}")]
    [ExigePermiso(PermisosDeInventario.RecuentoQuitarLinea)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> QuitarLinea(
        Guid id,
        Guid lineaId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancelacion) =>
        ResponderSinContenidoExigiendoVersionAsync(
            ifMatch,
            version => quitar.EjecutarAsync(id, lineaId, version, cancelacion));

    /// <summary>
    /// Confirma el recuento con la fecha de hoy: lo numera y mueve su diferencia con un ajuste, en la
    /// misma transacción.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Lleva dos comprobaciones, y cada una dice una cosa</b> (ADR-0055 §2): el <c>If-Match</c> de la
    /// cabecera, si el documento cambió desde que se leyó, y la huella del cuerpo, si cambió el
    /// almacén. Lo primero es un <c>412</c>. Lo segundo es un <c>409</c>
    /// (<c>recuento-teorico-cambiado</c>) con la huella de ahora y las líneas cambiadas en
    /// <c>actual</c>.
    /// </para>
    /// <para>
    /// Las líneas sin contar dan un <c>422</c> (<c>recuento-con-lineas-sin-contar</c>), con las
    /// primeras en <c>actual</c>. El <c>409</c> puede ser además el tránsito
    /// (<c>recuento-sube-con-transito</c>), un recuento que ya no está en curso, el ejercicio de hoy,
    /// una serie cerrada o una fecha fuera del ejercicio de su serie, un maestro bloqueado, la marca de
    /// un artículo o una clave que no se valora.
    /// </para>
    /// <para>
    /// <b>La <c>Idempotency-Key</c> es obligatoria</b>, porque confirmar gasta dos correlativos. La
    /// respuesta no lleva <c>ETag</c> (ADR-0057): la versión nueva se lee con la ficha.
    /// </para>
    /// </remarks>
    /// <param name="id">Identificador del recuento.</param>
    /// <param name="ifMatch">Versión de la cabecera, tal como la devolvió el ETag de la ficha.</param>
    /// <param name="peticion">La huella del teórico que se vio en la ficha.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("{id:guid}/confirmacion")]
    [AdmiteIdempotencia(Obligatoria = true)]
    [ExigePermiso(PermisosDeInventario.RecuentoConfirmar)]
    [ProducesResponseType(typeof(RecuentoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Confirmar(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] ConfirmarRecuentoDto peticion,
        CancellationToken cancelacion) =>
        ResponderExigiendoVersionAsync(
            ifMatch,
            version => confirmar.EjecutarAsync(id, version, peticion, cancelacion));

    /// <summary>Anula el recuento confirmado y, si movió el libro, su ajuste, con un inverso de hoy.</summary>
    /// <remarks>
    /// <para>
    /// <b>Es el único camino que anula el ajuste de un recuento</b> (ADR-0055 §9): la anulación del
    /// ajuste lo rechaza con <c>409</c> (<c>ajuste-de-un-recuento-no-se-anula</c>). El inverso numera
    /// en la serie del ajuste, con la fecha de hoy.
    /// </para>
    /// <para>
    /// El <c>409</c> puede ser un recuento que no está confirmado (<c>recuento-no-esta-confirmado</c>),
    /// el ejercicio de hoy, una serie cerrada o una clave que no se valora. La <c>Idempotency-Key</c>
    /// es obligatoria, porque el inverso gasta un correlativo, y la respuesta va sin <c>ETag</c>
    /// (ADR-0057).
    /// </para>
    /// </remarks>
    /// <param name="id">Identificador del recuento.</param>
    /// <param name="ifMatch">Versión de la cabecera, tal como la devolvió el ETag de la ficha.</param>
    /// <param name="peticion">Por qué se anula.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("{id:guid}/anulacion")]
    [AdmiteIdempotencia(Obligatoria = true)]
    [ExigePermiso(PermisosDeInventario.RecuentoAnular)]
    [ProducesResponseType(typeof(RecuentoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Anular(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] AnularRecuentoDto peticion,
        CancellationToken cancelacion) =>
        ResponderExigiendoVersionAsync(
            ifMatch,
            version => anular.EjecutarAsync(id, version, peticion, cancelacion));

    /// <summary>Descarta el recuento en curso: lo contado se pierde, y el almacén queda libre.</summary>
    /// <remarks>
    /// <para>
    /// <b>No mueve nada ni numera</b> (ADR-0055 §1.6): el recuento se queda sin número y con su motivo,
    /// y el almacén admite otro recuento en curso.
    /// </para>
    /// <para>
    /// El <c>409</c> es un recuento que ya no está en curso (<c>recuento-no-esta-en-curso</c>). La
    /// <c>Idempotency-Key</c> se admite y no se exige, y la respuesta va sin <c>ETag</c> (ADR-0057).
    /// </para>
    /// </remarks>
    /// <param name="id">Identificador del recuento.</param>
    /// <param name="ifMatch">Versión de la cabecera, tal como la devolvió el ETag de la ficha.</param>
    /// <param name="peticion">Por qué se descarta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("{id:guid}/descarte")]
    [AdmiteIdempotencia]
    [ExigePermiso(PermisosDeInventario.RecuentoDescartar)]
    [ProducesResponseType(typeof(RecuentoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Descartar(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] DescartarRecuentoDto peticion,
        CancellationToken cancelacion) =>
        ResponderExigiendoVersionAsync(
            ifMatch,
            version => descartar.EjecutarAsync(id, version, peticion, cancelacion));
}
