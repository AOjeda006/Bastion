using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Bastion.Api.IntegrationTests.Api;
using Bastion.BuildingBlocks.Domain.Fechas;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Exportacion;

/// <summary>
/// Lo que comparten los casos de las exportaciones: pedir el fichero y afirmar lo que lo envuelve,
/// que es lo mismo en todos los módulos (ADR-0073).
/// </summary>
internal static class Exportaciones
{
    private static readonly byte[] s_bom = [0xEF, 0xBB, 0xBF];

    /// <summary>
    /// Pide la exportación de un listado y devuelve el fichero tal como llega, después de afirmar
    /// el BOM, el tipo del contenido y el nombre del adjunto.
    /// </summary>
    /// <param name="cliente">El cliente, ya en su empresa.</param>
    /// <param name="listado">La ruta del listado, la de su <c>GET</c>.</param>
    /// <param name="recurso">El nombre de la hoja, que es el del fichero.</param>
    /// <param name="consulta">El orden y el texto, con su <c>?</c>; vacía, ninguno.</param>
    internal static async Task<byte[]> FicheroAsync(
        HttpClient cliente,
        string listado,
        string recurso,
        string consulta = "")
    {
        DateOnly antes = DiaDeNegocio.Hoy(TimeProvider.System);
        using HttpResponseMessage respuesta = await cliente.GetAsync($"{listado}/exportacion{consulta}");
        DateOnly despues = DiaDeNegocio.Hoy(TimeProvider.System);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(respuesta));

        MediaTypeHeaderValue? tipo = respuesta.Content.Headers.ContentType;
        tipo.ShouldNotBeNull();
        tipo.MediaType.ShouldBe("text/csv");
        tipo.CharSet.ShouldBe("utf-8");

        // El nombre lleva el día de negocio, y una exportación que cruza la medianoche puede llevar
        // cualquiera de los dos.
        ContentDispositionHeaderValue? adjunto = respuesta.Content.Headers.ContentDisposition;
        adjunto.ShouldNotBeNull("el fichero sale sin Content-Disposition, y el navegador no lo guarda");
        adjunto.DispositionType.ShouldBe("attachment");
        adjunto.FileNameStar.ShouldBeOneOf(Nombre(recurso, antes), Nombre(recurso, despues));
        adjunto.FileName.ShouldNotBeNull("sin filename, un navegador viejo no sabe cómo llamarlo");
        adjunto.FileName.Trim('"').ShouldBe(adjunto.FileNameStar);

        byte[] fichero = await respuesta.Content.ReadAsByteArrayAsync();

        fichero[..3].ShouldBe(s_bom, "el fichero empieza por el BOM de UTF-8");

        return fichero;
    }

    /// <summary>Lo mismo, y devuelve el texto del fichero sin el BOM.</summary>
    /// <param name="cliente">El cliente, ya en su empresa.</param>
    /// <param name="listado">La ruta del listado, la de su <c>GET</c>.</param>
    /// <param name="recurso">El nombre de la hoja, que es el del fichero.</param>
    /// <param name="consulta">El orden y el texto, con su <c>?</c>; vacía, ninguno.</param>
    internal static async Task<string> ExportarAsync(
        HttpClient cliente,
        string listado,
        string recurso,
        string consulta = "") =>
        Texto(await FicheroAsync(cliente, listado, recurso, consulta));

    /// <summary>El texto de un fichero que ya se ha visto empezar por el BOM, sin él.</summary>
    /// <param name="fichero">Lo que devolvió <see cref="FicheroAsync"/>.</param>
    internal static string Texto(byte[] fichero) => Encoding.UTF8.GetString(fichero[3..]);

    /// <summary>
    /// El problema de una respuesta sin lo que es de cada petición, que es dónde se pidió y su
    /// traza: lo que queda es lo que dos respuestas al mismo rechazo tienen que tener igual.
    /// </summary>
    /// <param name="respuesta">Una respuesta con su <c>application/problem+json</c>.</param>
    internal static async Task<string> ProblemaAsync(HttpResponseMessage respuesta)
    {
        JsonObject problema = JsonNode.Parse(await respuesta.Content.ReadAsStringAsync())!.AsObject();

        problema.Remove("instance").ShouldBeTrue("el arnés: el problema dice dónde se pidió");
        problema.Remove("traceId").ShouldBeTrue("el arnés: el problema lleva su traza");

        return problema.ToJsonString();
    }

    private static string Nombre(string recurso, DateOnly dia) =>
        string.Create(CultureInfo.InvariantCulture, $"{recurso}-{dia:yyyy-MM-dd}.csv");
}
