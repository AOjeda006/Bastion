using System.Globalization;
using System.Runtime.CompilerServices;
using Bastion.BuildingBlocks.Application.Exportacion;
using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Fechas;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Errores;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Bastion.BuildingBlocks.Infrastructure.Listados;

/// <summary>
/// Cómo se exporta un listado en el borde: todas sus páginas, de una misma foto, en CSV y en flujo
/// (ADR-0073).
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo mismo que el listado, menos la página.</b> La exportación enlaza el orden con sus
/// criterios y el texto, y los compone como el listado (ADR-0072): un <c>?sort=</c> que no se
/// admite o un <c>?q=</c> a un listado que no lo mira son el mismo <c>400</c>. La página la pone
/// ella, de <see cref="Paginacion.TamanioMaximo"/> en <see cref="Paginacion.TamanioMaximo"/>, que
/// es lo más que un cliente podría pedir: el caso de uso no ve nada que no pudiera ver ya.
/// </para>
/// <para>
/// <b>Lo que el listado rechaza se rechaza antes del primer byte.</b> La primera página se pide
/// antes de escribir nada, así que un <c>404</c> del recurso del que cuelga el listado, o un
/// <c>403</c>, salen como en el listado, con su problema. A partir de ahí la respuesta ya ha
/// empezado, y una página que falla a mitad lanza: ASP.NET Core corta la conexión sin cerrar el
/// fichero, y el navegador, que lo junta antes de guardarlo, ve un error y no un fichero corto.
/// </para>
/// <para>
/// <b>Todas las páginas salen de la misma foto</b> (<see cref="IFotoDeLectura"/>), así que el total
/// de la primera es el de todas, y es el que dice cuántas pedir.
/// </para>
/// </remarks>
public static class ExportacionDeListado
{
    /// <summary>El tipo del contenido, con su codificación.</summary>
    public const string TipoDeContenido = "text/csv; charset=utf-8";

    /// <summary>Exporta un listado cuyo caso de uso es un <see cref="IListado{TDto}"/>.</summary>
    /// <typeparam name="TDto">Lo que se publica de cada elemento.</typeparam>
    /// <param name="controlador">El controlador que atiende, que da la respuesta en la que se escribe.</param>
    /// <param name="consulta">El orden y los criterios, tal como han llegado en la URL.</param>
    /// <param name="texto">El texto pedido, o nulo si el listado no filtra por texto.</param>
    /// <param name="listado">El caso de uso.</param>
    /// <param name="foto">La foto del módulo del listado.</param>
    /// <param name="hoja">Las columnas del fichero.</param>
    /// <param name="reloj">El reloj, que da el día del nombre del fichero.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public static Task<IActionResult> ExportarAsync<TDto>(
        ControllerBase controlador,
        ConsultaDeListado consulta,
        FiltroDeTexto? texto,
        IListado<TDto> listado,
        IFotoDeLectura foto,
        HojaDeExportacion<TDto> hoja,
        TimeProvider reloj,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(listado);

        return ExportarAsync(
            controlador, consulta, texto, listado, listado.EjecutarAsync, foto, hoja, reloj, cancelacion);
    }

    /// <summary>Exporta un listado cuyo caso de uso devuelve la página a secas.</summary>
    /// <typeparam name="TDto">Lo que se publica de cada elemento.</typeparam>
    /// <param name="controlador">El controlador que atiende, que da la respuesta en la que se escribe.</param>
    /// <param name="consulta">El orden y los criterios, tal como han llegado en la URL.</param>
    /// <param name="texto">El texto pedido, o nulo si el listado no filtra por texto.</param>
    /// <param name="ordenables">Quien dice por qué campos deja ordenar.</param>
    /// <param name="ejecutar">El caso de uso, que se llama una vez por página interna.</param>
    /// <param name="foto">La foto del módulo del listado.</param>
    /// <param name="hoja">Las columnas del fichero.</param>
    /// <param name="reloj">El reloj, que da el día del nombre del fichero.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public static Task<IActionResult> ExportarAsync<TDto>(
        ControllerBase controlador,
        ConsultaDeListado consulta,
        FiltroDeTexto? texto,
        IOrdenaPor ordenables,
        Func<Paginacion, CancellationToken, Task<PaginaDe<TDto>>> ejecutar,
        IFotoDeLectura foto,
        HojaDeExportacion<TDto> hoja,
        TimeProvider reloj,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(ejecutar);

        return ExportarResultadoAsync(
            controlador,
            consulta,
            texto,
            ordenables,
            async (paginacion, cancelada) =>
                Resultado.Correcto(await ejecutar(paginacion, cancelada).ConfigureAwait(false)),
            foto,
            hoja,
            reloj,
            cancelacion);
    }

    /// <summary>
    /// Exporta un listado cuyo caso de uso puede fallar, como el que cuelga de otro recurso: su
    /// fallo en la primera página sale como en el listado, antes de escribir nada.
    /// </summary>
    /// <typeparam name="TDto">Lo que se publica de cada elemento.</typeparam>
    /// <param name="controlador">El controlador que atiende, que da la respuesta en la que se escribe.</param>
    /// <param name="consulta">El orden y los criterios, tal como han llegado en la URL.</param>
    /// <param name="texto">El texto pedido, o nulo si el listado no filtra por texto.</param>
    /// <param name="ordenables">Quien dice por qué campos deja ordenar.</param>
    /// <param name="ejecutar">El caso de uso, que se llama una vez por página interna.</param>
    /// <param name="foto">La foto del módulo del listado.</param>
    /// <param name="hoja">Las columnas del fichero.</param>
    /// <param name="reloj">El reloj, que da el día del nombre del fichero.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public static async Task<IActionResult> ExportarResultadoAsync<TDto>(
        ControllerBase controlador,
        ConsultaDeListado consulta,
        FiltroDeTexto? texto,
        IOrdenaPor ordenables,
        Func<Paginacion, CancellationToken, Task<Resultado<PaginaDe<TDto>>>> ejecutar,
        IFotoDeLectura foto,
        HojaDeExportacion<TDto> hoja,
        TimeProvider reloj,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(controlador);
        ArgumentNullException.ThrowIfNull(ejecutar);
        ArgumentNullException.ThrowIfNull(foto);
        ArgumentNullException.ThrowIfNull(hoja);
        ArgumentNullException.ThrowIfNull(reloj);

        Resultado<Paginacion> criterios = RespuestasDeListado.Criterios(controlador, consulta, texto, ordenables);

        if (!criterios.EsCorrecto)
        {
            return criterios.Error!.AResultadoDeAccion();
        }

        Paginacion pedida = criterios.Valor with { Pagina = 1, Tamanio = Paginacion.TamanioMaximo };

        IAsyncDisposable abierta = await foto.AbrirAsync(cancelacion).ConfigureAwait(false);

        await using (abierta.ConfigureAwait(false))
        {
            Resultado<PaginaDe<TDto>> primera = await ejecutar(pedida, cancelacion).ConfigureAwait(false);

            if (!primera.EsCorrecto)
            {
                return primera.Error!.AResultadoDeAccion();
            }

            HttpResponse respuesta = controlador.Response;
            respuesta.ContentType = TipoDeContenido;
            respuesta.Headers.ContentDisposition = Adjunto(hoja.Recurso, DiaDeNegocio.Hoy(reloj));

            await EscritorCsv.EscribirAsync(
                    respuesta.Body, hoja, Paginas(hoja, pedida, primera.Valor, ejecutar, cancelacion), cancelacion)
                .ConfigureAwait(false);
        }

        return new EmptyResult();
    }

    /// <summary>El nombre del fichero: el recurso y el día de negocio, <c>&lt;recurso&gt;-AAAA-MM-DD.csv</c>.</summary>
    /// <param name="recurso">El nombre del recurso en la ruta.</param>
    /// <param name="dia">El día de negocio en que se exporta.</param>
    public static string NombreDelFichero(string recurso, DateOnly dia) =>
        string.Create(CultureInfo.InvariantCulture, $"{recurso}-{dia:yyyy-MM-dd}.csv");

    private static string Adjunto(string recurso, DateOnly dia)
    {
        ContentDispositionHeaderValue adjunto = new("attachment");
        adjunto.SetHttpFileName(NombreDelFichero(recurso, dia));

        return adjunto.ToString();
    }

    private static async IAsyncEnumerable<IReadOnlyList<TDto>> Paginas<TDto>(
        HojaDeExportacion<TDto> hoja,
        Paginacion pedida,
        PaginaDe<TDto> primera,
        Func<Paginacion, CancellationToken, Task<Resultado<PaginaDe<TDto>>>> ejecutar,
        [EnumeratorCancellation] CancellationToken cancelacion)
    {
        yield return primera.Elementos;

        for (int numero = 2; (long)(numero - 1) * pedida.Tamanio < primera.Total; numero++)
        {
            Resultado<PaginaDe<TDto>> pagina =
                await ejecutar(pedida with { Pagina = numero }, cancelacion).ConfigureAwait(false);

            // La respuesta ya ha empezado, así que no hay problema que contestar: se lanza, y la
            // conexión se corta con el fichero sin cerrar. Dentro de la foto, una página que ha
            // salido bien no tiene por qué fallar en la siguiente; si pasa, es un fallo del código.
            if (!pagina.EsCorrecto)
            {
                throw new InvalidOperationException(
                    $"La página {numero} de la exportación de {hoja.Recurso} ha fallado con " +
                    $"{pagina.Error!.Codigo} cuando el fichero ya había empezado a salir.");
            }

            yield return pagina.Valor.Elementos;
        }
    }
}
