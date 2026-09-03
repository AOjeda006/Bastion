using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Errores;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.BuildingBlocks.Infrastructure.Listados;

/// <summary>
/// Cómo se atiende un listado en el borde: se valida el orden pedido y se responde.
/// </summary>
/// <remarks>
/// En un solo sitio y no una copia por controlador. Doce copias de tres líneas se escriben en
/// media hora y la número once se olvida de validar el orden: el listado seguiría respondiendo
/// <c>200</c>, ordenado por el campo de omisión, a un cliente que pidió otro — un fallo que
/// ninguna prueba de ese controlador busca porque el cuerpo es correcto.
/// </remarks>
public static class RespuestasDeListado
{
    /// <summary>
    /// Atiende un listado: valida el <c>?sort=</c> contra lo que el propio listado admite y
    /// responde con la página, o con un <c>400</c> que dice qué campos valen.
    /// </summary>
    /// <typeparam name="TDto">Lo que se publica de cada elemento.</typeparam>
    /// <param name="controlador">El controlador que atiende, para componer la respuesta.</param>
    /// <param name="consulta">Los parámetros tal como han llegado en la URL.</param>
    /// <param name="listado">El caso de uso, que es quien dice por qué campos deja ordenar.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public static async Task<IActionResult> ResponderAsync<TDto>(
        ControllerBase controlador,
        ConsultaPaginada consulta,
        IListado<TDto> listado,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(controlador);
        ArgumentNullException.ThrowIfNull(consulta);
        ArgumentNullException.ThrowIfNull(listado);

        Resultado<Paginacion> pedido = consulta.APaginacion(listado.CamposOrdenables);

        if (!pedido.EsCorrecto)
        {
            return pedido.Error!.AResultadoDeAccion();
        }

        return controlador.Ok(
            await listado.EjecutarAsync(pedido.Valor, cancelacion).ConfigureAwait(false));
    }
}
