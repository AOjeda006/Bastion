using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Organizacion.Application.Bloqueos;
using Bastion.Organizacion.Contracts;
using Bastion.Organizacion.Contracts.Bloqueos;
using Bastion.Organizacion.Endpoints.Comun;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Organizacion.Endpoints;

/// <summary>
/// Lo bloqueado, bajo <c>/api/v1/organizacion/bloqueados</c>: el acceso reservado del artículo 32.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es el único camino de la API que entrega filas bloqueadas</b>, y tiene su permiso propio
/// (ADR-0027). No es una comodidad para administradores: sin él, un bloqueo hecho por error deja el
/// dato inalcanzable —la fila es invisible para toda la interfaz— y el art. 32 obliga a reservar,
/// no a perder.
/// </para>
/// <para>
/// <b>Un listado y ninguna ficha, a propósito.</b> No hay <c>GET /bloqueados/{id}</c> y no lo va a
/// haber por descuido: una ficha devolvería el recurso con su testigo de versión y haría caducar de
/// golpe las cuatro exenciones de <c>If-Match</c> de los desbloqueos (ADR-0017). Lo que el
/// desbloqueo necesita —el identificador— sale de aquí.
/// </para>
/// <para>
/// <b>Y ninguna escritura.</b> Lo bloqueado se ve para poder levantarlo, y levantarlo es una acción
/// de su propio recurso, con su propio permiso: <c>DELETE</c> del bloqueo en Empresas, Almacenes o
/// Ubicaciones. Aquí no se edita nada — tratar un dato reservado es justo lo que el artículo
/// prohíbe.
/// </para>
/// </remarks>
public sealed class BloqueadosController(IListarLoBloqueado listar) : ControladorDeOrganizacion
{
    /// <summary>Devuelve una página de lo que está bloqueado en esta empresa.</summary>
    /// <remarks>
    /// Cada llamada queda anotada en el registro con el motivo del acceso y con <b>quién</b>
    /// pregunta: eso es lo que convierte esto en la vía «separada, nominativa y trazada» que pide
    /// el art. 32, y no en una consulta más con un permiso distinto.
    /// </remarks>
    /// <param name="consulta">Paginación, orden y filtro (<c>page</c>, <c>size</c>, <c>sort</c>, <c>q</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ExigePermiso(PermisosDeOrganizacion.BloqueadoVer)]
    [ProducesResponseType(typeof(PaginaDe<BloqueadoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] ConsultaPaginada consulta,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return await ResponderListadoAsync(consulta, listar, cancelacion).ConfigureAwait(false);
    }
}
