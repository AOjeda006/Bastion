using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;

namespace Bastion.BuildingBlocks.Infrastructure.Errores;

/// <summary>
/// Traduce el choque de concurrencia de EF Core en el <c>412</c> que manda el protocolo, con la
/// versión actual del recurso dentro.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aquí y no en cada controlador.</b> Un <c>catch (DbUpdateConcurrencyException)</c> por acción
/// serían quince sitios que hay que acordarse de escribir, y el que falte devolverá un <c>500</c>:
/// el cliente leerá «fallo del servidor» donde lo que ha pasado es que otro guardó antes, y lo
/// reintentará tal cual, machacando lo que el otro escribió. Es exactamente la actualización
/// perdida, pero con un mensaje que despista.
/// </para>
/// <para>
/// <b>Va registrado ANTES que el manejador general</b>, que atrapa cualquier excepción y responde
/// <c>500</c>: los manejadores se prueban en orden de inscripción y el primero que dice que sí
/// cierra la respuesta.
/// </para>
/// <para>
/// <b>El estado actual del conflicto se sirve como versión, no como volcado de columnas.</b> La
/// convención pide devolver el estado actual para que el cliente pueda enseñar la diferencia; lo
/// que sale de aquí es la versión de ahora, en la extensión <c>versionActual</c>, y con ella el
/// cliente vuelve a leer el recurso por su representación de siempre. Volcar aquí los valores de
/// la fila publicaría columnas que no pasan por ningún DTO, entre ellas las clasificadas como
/// secretas, y en el sitio con menos contexto para decidirlo. Está razonado en el ADR-0014.
/// </para>
/// <para>
/// <b>Y va en el cuerpo y no en una cabecera <c>ETag</c>, que era el primer diseño.</b> No es una
/// preferencia: el middleware de excepciones de ASP.NET Core registra un <c>OnStarting</c> que
/// BORRA el <c>ETag</c> de toda respuesta de error —junto con poner <c>Cache-Control: no-cache,
/// no-store</c> y <c>Pragma: no-cache</c>—, así que la cabecera que se pusiera aquí no llegaría al
/// cliente. Comprobado poniendo a la vez el <c>ETag</c> y una cabecera cualquiera: la segunda
/// llegó y el <c>ETag</c> no.
/// </para>
/// <para>
/// Y el borrado tiene razón, que es lo que impide buscarle la vuelta con otro nombre: el
/// <c>ETag</c> de una respuesta es el de <b>la representación que va en esa respuesta</b> (RFC
/// 9110, §8.8.3), y la que va en un <c>412</c> es un documento de problema, no el recurso. Un
/// <c>ETag</c> aquí estaría etiquetando el error, y un intermediario que lo guardara serviría el
/// error como si fuera el almacén.
/// </para>
/// </remarks>
internal sealed partial class ManejadorDeVersionObsoleta(
    IProblemDetailsService problemas,
    ILogger<ManejadorDeVersionObsoleta> registro) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not DbUpdateConcurrencyException choque)
        {
            return false;
        }

        VersionDeRecurso? actual = await ActualAsync(choque, cancellationToken).ConfigureAwait(false);

        RegistrarChoque(
            registro,
            httpContext.Request.Method,
            httpContext.Request.Path.Value ?? string.Empty,
            exception);

        ErrorDeOperacion error = actual is null
            ? ErroresDeConcurrencia.ObsoletaYSinRecurso()
            : ErroresDeConcurrencia.Obsoleta(actual.Value);

        ProblemDetails problema = error.AProblema();

        if (actual is not null)
        {
            // La versión de ahora, en el cuerpo y SOLO en el cuerpo. Ver la nota de arriba: en un
            // 412 la cabecera ETag ni se puede poner ni debería ponerse.
            problema.Extensions["versionActual"] = actual.Value.Etiqueta;
        }

        httpContext.Response.StatusCode = StatusCodes.Status412PreconditionFailed;

        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problema,
        }).ConfigureAwait(false);
    }

    // La versión que la fila tiene AHORA, preguntándosela a la base. Puede no haber ninguna: si
    // entre la lectura del cliente y su escritura alguien borró el recurso, no hay fila que
    // versionar y el mensaje lo dice en vez de inventarse un número.
    private static async Task<VersionDeRecurso?> ActualAsync(
        DbUpdateConcurrencyException choque,
        CancellationToken cancelacion)
    {
        foreach (EntityEntry entrada in choque.Entries)
        {
            if (entrada.Metadata.FindProperty(TestigoDeConcurrencia.Nombre) is null)
            {
                continue;
            }

            PropertyValues? valores = await entrada.GetDatabaseValuesAsync(cancelacion)
                .ConfigureAwait(false);

            if (valores?[TestigoDeConcurrencia.Nombre] is uint version)
            {
                return new VersionDeRecurso(version);
            }
        }

        return null;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Choque de concurrencia en {Metodo} {Ruta}: la versión del cliente ya no era la actual.")]
    private static partial void RegistrarChoque(
        ILogger registro, string metodo, string ruta, Exception excepcion);
}
