using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Inventario.Application.Transferencias;
using Bastion.Inventario.Contracts;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Endpoints.Comun;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Inventario.Endpoints;

/// <summary>Transferencias entre almacenes, bajo <c>/api/v1/inventario/transferencias</c>.</summary>
/// <remarks>
/// <para>
/// <b>Las tres acciones que cambian el estado llegaron antes que la pantalla</b>, en el 2.11
/// (ADR-0053 §9): cada una mueve el libro, y la que numera necesita la transacción que solo abre el
/// filtro de idempotencia (ADR-0014). <b>El alta, el listado, la ficha y el descarte llegan con la
/// pantalla</b>, en el 2.19 (ADR-0068 §1), como los del ajuste.
/// </para>
/// <para>
/// <b>Enviar, recibir y anular exigen la <c>Idempotency-Key</c></b>, y ninguna <c>If-Match</c>
/// (ADR-0068 §7): las líneas de un borrador no se modifican, así que lo que la ficha enseña solo
/// cambia por una transición, y cada acción mira el estado que pide. De la carrera entre dos personas
/// protege el testigo de la fila del documento, que sale por <c>412</c>. Sin cabecera son <c>428</c>.
/// </para>
/// <para>
/// <b>Cada una con su permiso</b>: quien envía y quien recibe suelen ser personas distintas, en
/// almacenes distintos, y ver, abrir y descartar no son ninguna de las dos cosas.
/// </para>
/// </remarks>
/// <param name="abrir">El alta del borrador, con sus líneas.</param>
/// <param name="obtener">La ficha, con su versión.</param>
/// <param name="listar">La lista de transferencias.</param>
/// <param name="descartar">El borrado lógico del borrador.</param>
/// <param name="enviar">El caso de uso que numera y saca del origen.</param>
/// <param name="recibir">El caso de uso que mete en el destino.</param>
/// <param name="anular">El caso de uso que opone el inverso.</param>
public sealed class TransferenciasController(
    IAbrirTransferencia abrir,
    IObtenerTransferencia obtener,
    IListarTransferencias listar,
    IDescartarTransferencia descartar,
    IEnviarTransferencia enviar,
    IRecibirTransferencia recibir,
    IAnularTransferencia anular) : ControladorDeInventario
{
    /// <summary>Devuelve una página de transferencias, sin sus líneas.</summary>
    /// <remarks>
    /// Va por la fecha de envío, de la última a la primera, y se ordena también por la fecha de
    /// recepción y por el número. Una descartada no sale (ADR-0068 §5).
    /// </remarks>
    /// <param name="pagina">La página (<c>page</c>, <c>size</c>).</param>
    /// <param name="consulta">
    /// El orden, el estado y los dos almacenes (<c>sort</c>, <c>estado</c>, <c>origen</c>,
    /// <c>destino</c>). Sin filtro de texto: un <c>q</c> con algo escrito es un <c>400</c>.
    /// </param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ExigePermiso(PermisosDeInventario.TransferenciaVer)]
    [ProducesResponseType(typeof(PaginaDe<TransferenciaResumenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Listar(
        [FromQuery] PaginaPedida pagina,
        [FromQuery] ConsultaDeTransferencias consulta,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return await ResponderListadoAsync(
            pagina,
            consulta,
            texto: null,
            listar,
            (paginacion, testigo) => listar.EjecutarAsync(
                paginacion, consulta.Estado, consulta.Origen, consulta.Destino, testigo),
            cancelacion).ConfigureAwait(false);
    }

    /// <summary>Devuelve la ficha de una transferencia, con todas sus líneas y su <c>ETag</c>.</summary>
    /// <remarks>
    /// Cada línea lleva sus dos ubicaciones, el factor y la cantidad en unidad base, que pone el
    /// servidor, y su valor cuando ya se ha enviado (ADR-0068 §8). La versión es la que pide el
    /// descarte.
    /// </remarks>
    /// <param name="id">Identificador de la transferencia.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{id:guid}")]
    [ExigePermiso(PermisosDeInventario.TransferenciaVer)]
    [ProducesResponseType(typeof(FichaDeTransferenciaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancelacion) =>
        ResponderConVersion(await obtener.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>Abre una transferencia en borrador, con sus líneas.</summary>
    /// <remarks>
    /// <para>
    /// <b>El factor de cada línea lo pone el servidor</b> (ADR-0068 §2): 1 si la unidad es la base
    /// del artículo, y si no, el de su conversión. Sin conversión, <c>422</c>
    /// <c>transferencia-sin-conversion</c>; con una conversión retirada, <c>409</c>
    /// <c>transferencia-conversion-retirada</c>; los dos, con la línea y las dos unidades en
    /// <c>actual</c>. Cada rechazo de una línea dice cuál es: en <c>errors</c>, con la ruta del
    /// campo, si es un <c>400</c>, y en <c>actual</c> si es un <c>409</c> o un <c>422</c>; el de una
    /// ubicación bloqueada dice además la punta (§6).
    /// </para>
    /// <para>
    /// <b>La <c>Idempotency-Key</c> es OBLIGATORIA</b>, por lo mismo que en el alta del ajuste: un
    /// borrador de transferencia no tiene código único ni índice que pare el reintento, y uno sin
    /// clave dejaría dos borradores iguales que se podrían enviar los dos. Sin cabecera son
    /// <c>428</c>.
    /// </para>
    /// <para>
    /// <b>La respuesta no lleva <c>ETag</c></b>: con la clave obligatoria, la repetición devolvería la
    /// versión de entonces (ADR-0057 §1). La versión se lee con la ficha. Como mucho doscientas
    /// líneas; una más es un <c>400</c>.
    /// </para>
    /// </remarks>
    /// <param name="peticion">Con qué serie, de qué almacén a cuál, qué día y qué se mueve.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost]
    [AdmiteIdempotencia(Obligatoria = true)]
    [ExigePermiso(PermisosDeInventario.TransferenciaAbrir)]
    [ProducesResponseType(typeof(TransferenciaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> Abrir(
        [FromBody] AbrirTransferenciaDto peticion,
        CancellationToken cancelacion) =>
        ResponderCreado(
            await abrir.EjecutarAsync(peticion, cancelacion).ConfigureAwait(false),
            nameof(Obtener),
            transferencia => transferencia.Id);

    /// <summary>
    /// Descarta una transferencia en borrador: deja de verse, y no para el cierre del ejercicio.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es un borrado lógico, y por eso un <c>DELETE</c></b> (ADR-0068 §5), como el del ajuste: el
    /// borrador no numeró, no movió nada y no bloqueó nada. La fila se queda con la marca, y la
    /// auditoría dice quién y cuándo.
    /// </para>
    /// <para>
    /// <b>Con <c>If-Match</c>, y la versión va antes que el estado</b> (ADR-0057 §3): con una versión
    /// vieja es <c>412</c>, aunque ya no esté en borrador; con la de ahora, una enviada es <c>409</c>
    /// <c>transferencia-no-esta-en-borrador</c>. Sin <c>Idempotency-Key</c>: repetirlo da
    /// <c>404</c>, que es lo que es.
    /// </para>
    /// </remarks>
    /// <param name="id">Identificador de la transferencia.</param>
    /// <param name="ifMatch">Versión de la transferencia, tal como la devolvió el ETag de la ficha.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpDelete("{id:guid}")]
    [ExigePermiso(PermisosDeInventario.TransferenciaDescartar)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Descartar(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancelacion) =>
        ResponderSinContenidoExigiendoVersionAsync(
            ifMatch,
            version => descartar.EjecutarAsync(id, version, cancelacion));

    /// <summary>Envía una transferencia en borrador: le da su número, y lo que sale queda en tránsito.</summary>
    /// <remarks>
    /// <para>
    /// <b>Un POST sobre un sub-recurso</b>, como la confirmación del ajuste: el envío es un hecho que
    /// se crea, no un campo que se fija.
    /// </para>
    /// <para>
    /// <b>La <c>Idempotency-Key</c> es obligatoria</b> por el motivo de la confirmación: toma un
    /// número, y el número y el documento quedan escritos en la misma transacción o no quedan (R5).
    /// </para>
    /// </remarks>
    /// <param name="id">Identificador de la transferencia que se envía.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("{id:guid}/envio")]
    [AdmiteIdempotencia(Obligatoria = true)]
    [ExigePermiso(PermisosDeInventario.TransferenciaEnviar)]
    [ProducesResponseType(typeof(TransferenciaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> Enviar(Guid id, CancellationToken cancelacion) =>
        Responder(await enviar.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>Recibe entera una transferencia enviada: lo que vuela entra en el destino.</summary>
    /// <remarks>
    /// <para>
    /// <b>El cuerpo solo trae la fecha</b>, porque la recepción es entera (ADR-0053 §4).
    /// </para>
    /// <para>
    /// <b>La <c>Idempotency-Key</c> es obligatoria aunque no numere</b>: la recepción bloquea la
    /// valoración del destino y escribe el documento, el tránsito, las existencias y el libro, y sin
    /// la transacción del filtro cada sentencia se confirmaría por su cuenta. El cerrojo se soltaría
    /// al acabar la suya, y un fallo a mitad dejaría la mercancía fuera del tránsito y sin entrar.
    /// </para>
    /// </remarks>
    /// <param name="id">Identificador de la transferencia que se recibe.</param>
    /// <param name="peticion">El día en que llega.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("{id:guid}/recepcion")]
    [AdmiteIdempotencia(Obligatoria = true)]
    [ExigePermiso(PermisosDeInventario.TransferenciaRecibir)]
    [ProducesResponseType(typeof(TransferenciaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> Recibir(
        Guid id,
        [FromBody] RecibirTransferenciaDto peticion,
        CancellationToken cancelacion) =>
        Responder(await recibir.EjecutarAsync(id, peticion, cancelacion).ConfigureAwait(false));

    /// <summary>
    /// Anula una transferencia enviada o recibida: crea el inverso que la compensa, lo numera y lo
    /// recibe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Un POST, no un DELETE</b>, por lo mismo que en el ajuste: no deja de estar nada, y lo que se
    /// crea es un documento nuevo.
    /// </para>
    /// <para>
    /// <b>La <c>Idempotency-Key</c> es obligatoria</b>, porque el inverso toma su número de la serie
    /// del original. El reintento con la misma clave devuelve el par de la primera vez.
    /// </para>
    /// </remarks>
    /// <param name="id">Identificador de la transferencia que se anula.</param>
    /// <param name="peticion">Por qué se anula.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("{id:guid}/anulacion")]
    [AdmiteIdempotencia(Obligatoria = true)]
    [ExigePermiso(PermisosDeInventario.TransferenciaAnular)]
    [ProducesResponseType(typeof(AnulacionDeTransferenciaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> Anular(
        Guid id,
        [FromBody] AnularTransferenciaDto peticion,
        CancellationToken cancelacion) =>
        Responder(await anular.EjecutarAsync(id, peticion, cancelacion).ConfigureAwait(false));
}
