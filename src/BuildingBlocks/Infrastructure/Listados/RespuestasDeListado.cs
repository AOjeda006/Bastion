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
    public static Task<IActionResult> ResponderAsync<TDto>(
        ControllerBase controlador,
        ConsultaPaginada consulta,
        IListado<TDto> listado,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(listado);

        return ResponderAsync(controlador, consulta, listado, listado.EjecutarAsync, cancelacion);
    }

    /// <summary>
    /// Lo mismo, para un listado que además recibe un criterio propio suyo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Existe desde el ítem 1.8, y por un listado que no cabe en <see cref="IListado{TDto}"/>:
    /// el de artículos, que se acota por categoría.</b> Las dos alternativas eran peores. Meter la
    /// categoría en <see cref="Paginacion"/> —que es del bloque común— se la publicaría a los doce
    /// listados del sistema como un parámetro que ninguno mira, que es la misma mentira de
    /// contrato que <c>ConsultaDeMaestro</c> existe para no cometer con <c>?retiradas=</c>. Y
    /// copiar estas cuatro líneas en el controlador dejaría fuera del sitio único justo la
    /// validación del <c>?sort=</c>, que es para lo que este tipo existe.
    /// </para>
    /// <para>
    /// Lo que se comparte, entonces, no es la firma del caso de uso: es
    /// <see cref="IOrdenaPor"/> —quién dice por qué campos se puede ordenar— y la decisión de qué
    /// se hace cuando el orden pedido no está entre ellos.
    /// </para>
    /// </remarks>
    /// <typeparam name="TDto">Lo que se publica de cada elemento.</typeparam>
    /// <param name="controlador">El controlador que atiende, para componer la respuesta.</param>
    /// <param name="consulta">Los parámetros tal como han llegado en la URL.</param>
    /// <param name="ordenables">Quien dice por qué campos deja ordenar este listado.</param>
    /// <param name="ejecutar">La llamada al caso de uso, ya cerrada sobre sus criterios propios.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public static async Task<IActionResult> ResponderAsync<TDto>(
        ControllerBase controlador,
        ConsultaPaginada consulta,
        IOrdenaPor ordenables,
        Func<Paginacion, CancellationToken, Task<PaginaDe<TDto>>> ejecutar,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(controlador);
        ArgumentNullException.ThrowIfNull(consulta);
        ArgumentNullException.ThrowIfNull(ordenables);
        ArgumentNullException.ThrowIfNull(ejecutar);

        Resultado<Paginacion> pedido = consulta.APaginacion(ordenables.CamposOrdenables);

        if (!pedido.EsCorrecto)
        {
            return pedido.Error!.AResultadoDeAccion();
        }

        return controlador.Ok(
            await ejecutar(pedido.Valor, cancelacion).ConfigureAwait(false));
    }
}
