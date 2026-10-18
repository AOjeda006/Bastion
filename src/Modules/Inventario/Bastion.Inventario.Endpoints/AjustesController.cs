using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.Inventario.Application.Ajustes;
using Bastion.Inventario.Contracts;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Endpoints.Comun;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Inventario.Endpoints;

/// <summary>Ajustes de inventario, bajo <c>/api/v1/inventario/ajustes</c>.</summary>
/// <remarks>
/// <para>
/// <b>La confirmación y la anulación llegaron antes que la pantalla.</b> El módulo abrió su borde
/// HTTP en el ítem 2.4 con la confirmación y con nada más, y el 2.5 añadió la anulación por el
/// mismo motivo: el documento que anula es un ajuste confirmado de pleno derecho, así que necesita
/// número, el número necesita transacción, y el único dueño de transacción del sistema es el filtro
/// de idempotencia (ADR-0014), que no corre sin una acción de MVC.
/// </para>
/// <para>
/// <b>El alta, el listado, la ficha y el descarte llegan con la pantalla</b>, en el 2.17
/// (ADR-0065 §6), cada uno con su permiso: ver, abrir y descartar no son confirmar.
/// </para>
/// </remarks>
/// <param name="abrir">El alta del borrador, con sus líneas.</param>
/// <param name="obtener">La ficha, con su versión.</param>
/// <param name="listar">La lista de ajustes.</param>
/// <param name="descartar">El borrado lógico del borrador.</param>
/// <param name="confirmar">El caso de uso que confirma y numera.</param>
/// <param name="anular">El caso de uso que opone el contra-documento.</param>
public sealed class AjustesController(
    IAbrirAjuste abrir,
    IObtenerAjuste obtener,
    IListarAjustes listar,
    IDescartarAjuste descartar,
    IConfirmarAjuste confirmar,
    IAnularAjuste anular) : ControladorDeInventario
{
    /// <summary>Devuelve una página de ajustes, sin sus líneas.</summary>
    /// <remarks>
    /// Va por la fecha de operación, de la última a la primera, y se ordena también por el número. Un
    /// borrador descartado no sale (ADR-0065 §5).
    /// </remarks>
    /// <param name="consulta">
    /// Paginación, orden, filtro, estado y almacén (<c>page</c>, <c>size</c>, <c>sort</c>, <c>q</c>,
    /// <c>estado</c>, <c>almacen</c>). El filtro de texto mira el motivo.
    /// </param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ExigePermiso(PermisosDeInventario.AjusteVer)]
    [ProducesResponseType(typeof(PaginaDe<AjusteResumenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Listar(
        [FromQuery] ConsultaDeAjustes consulta,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return await ResponderListadoAsync(
            consulta,
            listar,
            (paginacion, testigo) => listar.EjecutarAsync(paginacion, consulta.Estado, consulta.Almacen, testigo),
            cancelacion).ConfigureAwait(false);
    }

    /// <summary>Devuelve la ficha de un ajuste, con todas sus líneas y su <c>ETag</c>.</summary>
    /// <remarks>
    /// Cada línea lleva el factor y la cantidad en unidad base, que pone el servidor, y su valor
    /// cuando ya se ha confirmado (ADR-0065 §8). La versión es la que pide el descarte.
    /// </remarks>
    /// <param name="id">Identificador del ajuste.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{id:guid}")]
    [ExigePermiso(PermisosDeInventario.AjusteVer)]
    [ProducesResponseType(typeof(FichaDeAjusteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancelacion) =>
        ResponderConVersion(await obtener.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>Abre un ajuste en borrador, con sus líneas.</summary>
    /// <remarks>
    /// <para>
    /// <b>El factor de cada línea lo pone el servidor</b> (ADR-0065 §2): 1 si la unidad es la base
    /// del artículo, y si no, el de su conversión. Sin conversión, <c>422</c>
    /// <c>ajuste-sin-conversion</c>; con una conversión retirada, <c>409</c>
    /// <c>ajuste-conversion-retirada</c>; los dos, con la línea y las dos unidades en <c>actual</c>.
    /// Cada rechazo de una línea dice cuál es: en <c>errors</c>, con la ruta del campo, si es un
    /// <c>400</c>, y en <c>actual</c> si es un <c>409</c> o un <c>422</c> (§3).
    /// </para>
    /// <para>
    /// <b>La <c>Idempotency-Key</c> es OBLIGATORIA</b> (§6), y no porque el alta numere, que no
    /// numera: el alta del recuento la admite sin exigirla porque el índice de uno en curso por almacén
    /// para el reintento, y aquí no hay nada que lo pare. Un reintento sin clave dejaría dos borradores
    /// iguales, y los dos se podrían confirmar. Sin cabecera son <c>428</c>.
    /// </para>
    /// <para>
    /// <b>La respuesta no lleva <c>ETag</c></b>: con la clave obligatoria, la repetición devolvería la
    /// versión de entonces (ADR-0057 §1). La versión se lee con la ficha. Como mucho doscientas
    /// líneas; una más es un <c>400</c>.
    /// </para>
    /// </remarks>
    /// <param name="peticion">Qué almacén, con qué serie, qué día, por qué y qué se mueve.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost]
    [AdmiteIdempotencia(Obligatoria = true)]
    [ExigePermiso(PermisosDeInventario.AjusteAbrir)]
    [ProducesResponseType(typeof(AjusteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> Abrir(
        [FromBody] AbrirAjusteDto peticion,
        CancellationToken cancelacion) =>
        ResponderCreado(
            await abrir.EjecutarAsync(peticion, cancelacion).ConfigureAwait(false),
            nameof(Obtener),
            ajuste => ajuste.Id);

    /// <summary>Descarta un ajuste en borrador: deja de verse, y no para el cierre del ejercicio.</summary>
    /// <remarks>
    /// <para>
    /// <b>Es un borrado lógico, y por eso un <c>DELETE</c></b> (ADR-0065 §5): el borrador no numeró,
    /// no movió nada y no bloqueó nada, así que tirarlo no deja nada que contar. La fila se queda
    /// con la marca, y la auditoría dice quién y cuándo.
    /// </para>
    /// <para>
    /// <b>Con <c>If-Match</c>, y la versión va antes que el estado</b> (ADR-0057 §3): con una versión
    /// vieja es <c>412</c>, aunque el ajuste ya no esté en borrador; con la de ahora, un confirmado
    /// es <c>409</c> <c>ajuste-no-esta-en-borrador</c>. Sin <c>Idempotency-Key</c>: repetirlo da
    /// <c>404</c>, que es lo que es.
    /// </para>
    /// </remarks>
    /// <param name="id">Identificador del ajuste.</param>
    /// <param name="ifMatch">Versión del ajuste, tal como la devolvió el ETag de la ficha.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpDelete("{id:guid}")]
    [ExigePermiso(PermisosDeInventario.AjusteDescartar)]
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

    /// <summary>Confirma un ajuste en borrador: le da su número y mueve el libro.</summary>
    /// <remarks>
    /// <para>
    /// <b>Es un POST sobre un sub-recurso y no un PUT sobre el ajuste</b>, porque lo que se pide no
    /// es dejar el documento como dice el cuerpo: es que ocurra algo —se toma un correlativo, se
    /// escriben los movimientos— y eso no tiene cuerpo que mandar. La confirmación es un hecho que
    /// se crea, no un campo que se fija.
    /// </para>
    /// <para>
    /// <b>La <c>Idempotency-Key</c> es OBLIGATORIA aquí</b>, que fue la primera acción de toda la API
    /// que la exigió. Sin ella el filtro se aparta sin abrir transacción, y la atomicidad entre el
    /// número y el documento se va con ella: el <c>UPDATE</c> del contador se confirmaría por su
    /// cuenta y un fallo posterior dejaría el número gastado, o sea un hueco en la serie, que es
    /// exactamente lo que la R5 prohíbe. Sin cabecera son <c>428</c>.
    /// </para>
    /// <para>
    /// <b>Y no exige <c>If-Match</c></b> (ADR-0065 §7): lo que la ficha enseña de un borrador solo
    /// cambia por una transición de estado, porque las líneas no se modifican, y esa la mira la
    /// máquina de estados —quien confirma lo que otro acaba de confirmar recibe el <c>409</c>, y si ya
    /// lo habían descartado, el <c>404</c>—. Contra la carrera está el testigo de la fila, con su
    /// <c>412</c>, y del reintento del mismo cliente protege la clave de arriba, que le devuelve la
    /// respuesta de la primera vez con su número dentro. El día que una línea se pueda modificar,
    /// confirmar tendrá que haberla visto, y pedirá los dos mecanismos, como la del recuento.
    /// </para>
    /// </remarks>
    /// <param name="id">Identificador del ajuste que se confirma.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("{id:guid}/confirmacion")]
    [AdmiteIdempotencia(Obligatoria = true)]
    [ExigePermiso(PermisosDeInventario.AjusteConfirmar)]
    [ProducesResponseType(typeof(AjusteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> Confirmar(Guid id, CancellationToken cancelacion) =>
        Responder(await confirmar.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>
    /// Anula un ajuste confirmado: crea el inverso que lo compensa, lo numera y lo confirma.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Un POST sobre un sub-recurso, no un DELETE</b>, y la diferencia es la regla entera: un
    /// <c>DELETE</c> promete que el recurso deja de estar, y aquí no deja de estar nada. El
    /// original se queda, sus filas del libro se quedan —es de solo añadido (R3)— y lo que se
    /// crea es un documento nuevo. La anulación es un hecho que se añade, no una fila que se
    /// quita, y el verbo tiene que decirlo.
    /// </para>
    /// <para>
    /// <b>La <c>Idempotency-Key</c> es OBLIGATORIA</b>, y con esta fueron <b>dos</b> las acciones de
    /// toda la API que la exigían; la lista entera, con el motivo de cada una, la lleva
    /// <c>TodaEscrituraDiceComoSeProtegeTests</c>. El criterio no se amplía para que quepa: es el
    /// mismo de la confirmación —sin la cabecera el filtro se aparta sin abrir transacción, y el
    /// inverso no podría tomar su número sin dejar un hueco en la serie, que es lo que la R5
    /// prohíbe—. Sin cabecera son <c>428</c>, y el reintento con la misma clave devuelve el par de
    /// la primera vez en vez de anular dos veces.
    /// </para>
    /// <para>
    /// <b>Y no exige <c>If-Match</c></b>, por lo mismo que la confirmación (ADR-0065 §7): de anular
    /// dos veces seguidas protege la máquina de estados —el segundo intento se encuentra un ajuste que
    /// ya no está confirmado y sale <c>409</c>—, y de anular dos veces <b>a la vez</b> protegen dos
    /// cosas que dicen lo mismo: el índice único de <c>anula_a_id</c>, que es el que llega primero,
    /// y detrás el testigo de concurrencia de la fila (R11). Los dos salen por <c>412</c> —el
    /// segundo con la versión de ahora dentro— y dejan sin efecto la transacción entera de quien
    /// pierde: ni inverso, ni número gastado.
    /// </para>
    /// </remarks>
    /// <param name="id">Identificador del ajuste que se anula.</param>
    /// <param name="peticion">Por qué se anula.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("{id:guid}/anulacion")]
    [AdmiteIdempotencia(Obligatoria = true)]
    [ExigePermiso(PermisosDeInventario.AjusteAnular)]
    [ProducesResponseType(typeof(AnulacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> Anular(
        Guid id,
        [FromBody] AnularAjusteDto peticion,
        CancellationToken cancelacion) =>
        Responder(await anular.EjecutarAsync(id, peticion, cancelacion).ConfigureAwait(false));
}
