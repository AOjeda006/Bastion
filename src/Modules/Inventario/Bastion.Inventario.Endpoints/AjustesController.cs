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
/// <b>Una sola acción, y es a propósito.</b> El módulo abre su borde HTTP en el ítem 2.4 con la
/// confirmación y con nada más: ni alta, ni listado, ni ficha. La superficie del ajuste va con sus
/// pantallas, y lo que este ítem necesita de la API es el sitio donde el número entra en un recibo
/// de idempotencia — que no existe sin una acción de MVC.
/// </para>
/// </remarks>
/// <param name="confirmar">El caso de uso que confirma y numera.</param>
public sealed class AjustesController(IConfirmarAjuste confirmar) : ControladorDeInventario
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
}
