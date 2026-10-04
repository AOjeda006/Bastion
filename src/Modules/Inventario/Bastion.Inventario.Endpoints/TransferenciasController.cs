using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
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
/// <b>Tres acciones, las que cambian el estado</b>, y la superficie es la del ajuste (ADR-0053 §9):
/// cada una mueve el libro, y la que numera necesita la transacción que solo abre el filtro de
/// idempotencia (ADR-0014). El alta, el listado y la ficha llegan con sus pantallas.
/// </para>
/// <para>
/// <b>Las tres exigen la <c>Idempotency-Key</c></b>, y ninguna <c>If-Match</c>: de repetir protege
/// la máquina de estados, y de la carrera entre dos personas, el testigo de la fila del documento,
/// que sale por <c>412</c>. Sin cabecera son <c>428</c>.
/// </para>
/// <para>
/// <b>Cada una con su permiso</b>: quien envía y quien recibe suelen ser personas distintas, en
/// almacenes distintos.
/// </para>
/// </remarks>
/// <param name="enviar">El caso de uso que numera y saca del origen.</param>
/// <param name="recibir">El caso de uso que mete en el destino.</param>
/// <param name="anular">El caso de uso que opone el inverso.</param>
public sealed class TransferenciasController(
    IEnviarTransferencia enviar,
    IRecibirTransferencia recibir,
    IAnularTransferencia anular) : ControladorDeInventario
{
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
