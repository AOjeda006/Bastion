using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Errores;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.BuildingBlocks.Infrastructure.Listados;

/// <summary>
/// Cómo se atiende un listado en el borde: se componen las piezas que ha enlazado la acción, se
/// valida el orden pedido y se responde.
/// </summary>
/// <remarks>
/// <para>
/// En un solo sitio y no una copia por controlador. Doce copias de tres líneas se escriben en
/// media hora y la número once se olvida de validar el orden: el listado seguiría respondiendo
/// <c>200</c>, ordenado por el campo de omisión, a un cliente que pidió otro — un fallo que
/// ninguna prueba de ese controlador busca porque el cuerpo es correcto.
/// </para>
/// <para>
/// <b>Las piezas llegan por separado desde el ítem 3.2</b>: la página
/// (<see cref="PaginaPedida"/>), el orden y los criterios propios del recurso
/// (<see cref="ConsultaDeListado"/>), y el texto (<see cref="FiltroDeTexto"/>), que es nulo en
/// un listado que no filtra por texto. Nulo no es «sin texto»: es «este listado no tiene», y un
/// <c>?q=</c> con algo escrito que llegue ahí se rechaza con un <c>400</c> en vez de contestar
/// la página entera como si hubiera filtrado (ADR-0072).
/// </para>
/// </remarks>
public static class RespuestasDeListado
{
    /// <summary>
    /// Atiende un listado: valida el <c>?sort=</c> contra lo que el propio listado admite y
    /// responde con la página, o con un <c>400</c> que dice qué campos valen.
    /// </summary>
    /// <typeparam name="TDto">Lo que se publica de cada elemento.</typeparam>
    /// <param name="controlador">El controlador que atiende, para componer la respuesta.</param>
    /// <param name="pagina">La página pedida, tal como ha llegado en la URL.</param>
    /// <param name="consulta">El orden, tal como ha llegado en la URL.</param>
    /// <param name="texto">El texto pedido, o nulo si el listado no filtra por texto.</param>
    /// <param name="listado">El caso de uso, que es quien dice por qué campos deja ordenar.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public static Task<IActionResult> ResponderAsync<TDto>(
        ControllerBase controlador,
        PaginaPedida pagina,
        ConsultaDeListado consulta,
        FiltroDeTexto? texto,
        IListado<TDto> listado,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(listado);

        return ResponderAsync(
            controlador, pagina, consulta, texto, listado, listado.EjecutarAsync, cancelacion);
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
    /// <param name="pagina">La página pedida, tal como ha llegado en la URL.</param>
    /// <param name="consulta">El orden y los criterios propios, tal como han llegado en la URL.</param>
    /// <param name="texto">El texto pedido, o nulo si el listado no filtra por texto.</param>
    /// <param name="ordenables">Quien dice por qué campos deja ordenar este listado.</param>
    /// <param name="ejecutar">La llamada al caso de uso, ya cerrada sobre sus criterios propios.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public static async Task<IActionResult> ResponderAsync<TDto>(
        ControllerBase controlador,
        PaginaPedida pagina,
        ConsultaDeListado consulta,
        FiltroDeTexto? texto,
        IOrdenaPor ordenables,
        Func<Paginacion, CancellationToken, Task<PaginaDe<TDto>>> ejecutar,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(ejecutar);

        Resultado<Paginacion> pedido = Componer(controlador, pagina, consulta, texto, ordenables);

        if (!pedido.EsCorrecto)
        {
            return pedido.Error!.AResultadoDeAccion();
        }

        return controlador.Ok(
            await ejecutar(pedido.Valor, cancelacion).ConfigureAwait(false));
    }

    /// <summary>
    /// Lo mismo, para un listado que puede fallar antes de llegar a haber página.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Existe desde el ítem 1.9, y por los listados que cuelgan de otro recurso</b>: las líneas
    /// de una tarifa. Pedir las líneas de un identificador inventado no es una página vacía, es un
    /// <c>404</c>, y la diferencia importa porque una página vacía con un <c>200</c> es
    /// indistinguible de una tarifa recién abierta que todavía no tiene líneas. Quien lo comprueba
    /// es el caso de uso —es él quien sabe qué es el padre—, así que devuelve
    /// <see cref="Resultado{T}"/> y no una página a secas.
    /// </para>
    /// <para>
    /// Se separa por nombre y no por sobrecarga a propósito: dos sobrecargas que solo se distinguen
    /// por lo que devuelve la lambda se resuelven bien hasta el día en que alguien cambia el tipo de
    /// retorno del caso de uso y el compilador, en vez de avisar, elige la otra.
    /// </para>
    /// </remarks>
    /// <typeparam name="TDto">Lo que se publica de cada elemento.</typeparam>
    /// <param name="controlador">El controlador que atiende, para componer la respuesta.</param>
    /// <param name="pagina">La página pedida, tal como ha llegado en la URL.</param>
    /// <param name="consulta">El orden y los criterios propios, tal como han llegado en la URL.</param>
    /// <param name="texto">El texto pedido, o nulo si el listado no filtra por texto.</param>
    /// <param name="ordenables">Quien dice por qué campos deja ordenar este listado.</param>
    /// <param name="ejecutar">La llamada al caso de uso, ya cerrada sobre sus criterios propios.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public static async Task<IActionResult> ResponderResultadoAsync<TDto>(
        ControllerBase controlador,
        PaginaPedida pagina,
        ConsultaDeListado consulta,
        FiltroDeTexto? texto,
        IOrdenaPor ordenables,
        Func<Paginacion, CancellationToken, Task<Resultado<PaginaDe<TDto>>>> ejecutar,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(ejecutar);

        Resultado<Paginacion> pedido = Componer(controlador, pagina, consulta, texto, ordenables);

        if (!pedido.EsCorrecto)
        {
            return pedido.Error!.AResultadoDeAccion();
        }

        Resultado<PaginaDe<TDto>> respuesta =
            await ejecutar(pedido.Valor, cancelacion).ConfigureAwait(false);

        return respuesta.EsCorrecto
            ? controlador.Ok(respuesta.Valor)
            : respuesta.Error!.AResultadoDeAccion();
    }

    /// <summary>
    /// Lo que pide la URL de un listado, sin la página: el orden, los criterios propios y el
    /// texto; o el <c>400</c> si el orden no se admite o si llega un texto que el listado no mira.
    /// </summary>
    /// <param name="controlador">El controlador que atiende, por la cadena de consulta.</param>
    /// <param name="consulta">El orden y los criterios propios, tal como han llegado en la URL.</param>
    /// <param name="texto">El texto pedido, o nulo si el listado no filtra por texto.</param>
    /// <param name="ordenables">Quien dice por qué campos deja ordenar este listado.</param>
    internal static Resultado<Paginacion> Criterios(
        ControllerBase controlador,
        ConsultaDeListado consulta,
        FiltroDeTexto? texto,
        IOrdenaPor ordenables)
    {
        ArgumentNullException.ThrowIfNull(controlador);
        ArgumentNullException.ThrowIfNull(consulta);
        ArgumentNullException.ThrowIfNull(ordenables);

        if (texto is null && FiltroDeTexto.LlegaEn(controlador.Request.Query))
        {
            return Resultado.Fallo<Paginacion>(FiltroDeTexto.NoAdmitido());
        }

        Resultado<Paginacion> pedido = consulta.APaginacion(ordenables.CamposOrdenables);

        return pedido.EsCorrecto
            ? Resultado.Correcto(pedido.Valor with { Filtro = texto?.Texto })
            : pedido;
    }

    /// <summary>
    /// La paginación entera que pide la URL de un listado, o el <c>400</c> si el orden no se
    /// admite o si llega un texto que el listado no mira.
    /// </summary>
    /// <remarks>
    /// Pública por el listado cuya respuesta no es una página a secas, el del saldo, que lleva la
    /// fecha al lado. Los demás pasan por los métodos de arriba, que la llaman.
    /// </remarks>
    /// <param name="controlador">El controlador que atiende, por la cadena de consulta.</param>
    /// <param name="pagina">La página pedida, tal como ha llegado en la URL.</param>
    /// <param name="consulta">El orden y los criterios propios, tal como han llegado en la URL.</param>
    /// <param name="texto">El texto pedido, o nulo si el listado no filtra por texto.</param>
    /// <param name="ordenables">Quien dice por qué campos deja ordenar este listado.</param>
    public static Resultado<Paginacion> Componer(
        ControllerBase controlador,
        PaginaPedida pagina,
        ConsultaDeListado consulta,
        FiltroDeTexto? texto,
        IOrdenaPor ordenables)
    {
        ArgumentNullException.ThrowIfNull(pagina);

        Resultado<Paginacion> criterios = Criterios(controlador, consulta, texto, ordenables);

        return criterios.EsCorrecto
            ? Resultado.Correcto(criterios.Valor with
            {
                Pagina = pagina.Pagina,
                Tamanio = pagina.Tamanio,
            })
            : criterios;
    }
}
