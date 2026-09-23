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
/// <b>Dos acciones, y ninguna de las dos es superficie de más.</b> El módulo abrió su borde HTTP
/// en el ítem 2.4 con la confirmación y con nada más —ni alta, ni listado, ni ficha—, y el 2.5
/// añade la anulación por el mismo motivo por el que existía la primera: el documento que anula
/// es un ajuste confirmado de pleno derecho, así que necesita número, el número necesita
/// transacción, y el único dueño de transacción del sistema es el filtro de idempotencia
/// (ADR-0014), que no corre sin una acción de MVC. Un caso de uso de anulación sin puerta sería
/// un camino que ningún llamante de producción puede recorrer.
/// </para>
/// <para>
/// <b>La superficie de lectura sigue sin estar</b>, y eso no cambia aquí: el alta, el listado y la
/// ficha van con sus pantallas.
/// </para>
/// </remarks>
/// <param name="confirmar">El caso de uso que confirma y numera.</param>
/// <param name="anular">El caso de uso que opone el contra-documento.</param>
public sealed class AjustesController(IConfirmarAjuste confirmar, IAnularAjuste anular)
    : ControladorDeInventario
{
    /// <summary>Confirma un ajuste en borrador: le da su número y mueve el libro.</summary>
    /// <remarks>
    /// <para>
    /// <b>Es un POST sobre un sub-recurso y no un PUT sobre el ajuste</b>, porque lo que se pide no
    /// es dejar el documento como dice el cuerpo: es que ocurra algo —se toma un correlativo, se
    /// escriben los movimientos— y eso no tiene cuerpo que mandar. La confirmación es un hecho que
    /// se crea, no un campo que se fija.
    /// </para>
    /// <para>
    /// <b>La <c>Idempotency-Key</c> es OBLIGATORIA aquí</b>, que es la única acción de toda la API
    /// que la exige. Sin ella el filtro se aparta sin abrir transacción, y la atomicidad entre el
    /// número y el documento se va con ella: el <c>UPDATE</c> del contador se confirmaría por su
    /// cuenta y un fallo posterior dejaría el número gastado, o sea un hueco en la serie, que es
    /// exactamente lo que la R5 prohíbe. Sin cabecera son <c>428</c>.
    /// </para>
    /// <para>
    /// <b>Y no exige <c>If-Match</c></b>, ni le haría falta: lo que protege de confirmar dos veces
    /// no es una versión, es la máquina de estados —el segundo intento se encuentra un ajuste que
    /// ya no está en borrador y sale <c>409</c>—, y lo que protege del reintento del mismo cliente
    /// es la clave de arriba, que le devuelve la respuesta de la primera vez con su número dentro.
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
    /// <b>La <c>Idempotency-Key</c> es OBLIGATORIA</b>, y con esta son <b>dos</b> las acciones de
    /// toda la API que la exigen. El criterio no se amplía para que quepa: es el mismo de la
    /// confirmación —sin la cabecera el filtro se aparta sin abrir transacción, y el inverso no
    /// podría tomar su número sin dejar un hueco en la serie, que es lo que la R5 prohíbe—. Sin
    /// cabecera son <c>428</c>, y el reintento con la misma clave devuelve el par de la primera
    /// vez en vez de anular dos veces.
    /// </para>
    /// <para>
    /// <b>Y no exige <c>If-Match</c></b>, por lo mismo que la confirmación: de anular dos veces
    /// seguidas protege la máquina de estados —el segundo intento se encuentra un ajuste que ya
    /// no está confirmado y sale <c>409</c>—, y de anular dos veces <b>a la vez</b> protege el
    /// testigo de concurrencia de la fila (R11), que devuelve <c>412</c> con la versión de ahora
    /// dentro y deja sin efecto la transacción entera de quien pierde: ni inverso, ni número
    /// gastado.
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
