using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Bastion.BuildingBlocks.Infrastructure.Errores;

/// <summary>
/// Convierte cualquier excepción que llegue viva al borde en un <c>ProblemDetails</c>.
/// </summary>
/// <remarks>
/// <para>
/// Un mensaje de error tiene DOS destinatarios que no comparten texto: el de fuera necesita
/// saber qué hacer, el de dentro qué ha pasado. Aquí eso es literal: el detalle de la excepción
/// va al registro, con su traza; la respuesta lleva un texto FIJO por clase de fallo. Fundir los
/// dos es cómo el texto de una excepción acaba publicando rutas, consultas y nombres de tabla.
/// </para>
/// <para>
/// Por eso la excepción NO se pasa al <see cref="ProblemDetailsContext"/>: si no está a mano,
/// no puede colarse en la respuesta ni hoy ni cuando alguien añada una personalización.
/// </para>
/// </remarks>
internal sealed partial class ManejadorDeExcepcionesNoControladas(
    IProblemDetailsService problemas,
    ILogger<ManejadorDeExcepcionesNoControladas> registro) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        // `BadHttpRequestException` es cómo ASP.NET Core dice "esto lo ha mandado mal el
        // cliente" (cuerpo que no parsea, cabecera imposible, tamaño excedido). Responder 500
        // a eso sería culpar al servidor de un fallo del que no tiene parte.
        bool esCulpaDeLaPeticion = exception is BadHttpRequestException;
        int estado = esCulpaDeLaPeticion
            ? ((BadHttpRequestException)exception).StatusCode
            : StatusCodes.Status500InternalServerError;

        string metodo = httpContext.Request.Method;
        string ruta = httpContext.Request.Path.Value ?? string.Empty;

        if (esCulpaDeLaPeticion)
        {
            RegistrarPeticionMalFormada(registro, metodo, ruta, exception);
        }
        else
        {
            RegistrarExcepcionNoControlada(registro, metodo, ruta, exception);
        }

        httpContext.Response.StatusCode = estado;

        ProblemDetails problema = esCulpaDeLaPeticion
            ? new ProblemDetails
            {
                Status = estado,
                Type = PoliticaDeErrores.TipoDe("peticion-mal-formada"),
                Title = "Petición mal formada",
                Detail = "No se ha podido interpretar la petición. Revise el formato del cuerpo y "
                    + "de los parámetros.",
            }
            : new ProblemDetails
            {
                Status = estado,
                Type = PoliticaDeErrores.TipoDe("error-interno"),
                Title = "Error interno del servidor",
                Detail = "La operación no se ha podido completar por un fallo del servidor. Vuelva a "
                    + "intentarlo; si persiste, facilite el identificador de traza al soporte.",
            };

        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problema,
        });
    }

    // Generados en tiempo de compilación (CA1848): sin caja de los argumentos y sin formatear
    // nada cuando el nivel está apagado.
    [LoggerMessage(Level = LogLevel.Warning, Message = "Petición mal formada en {Metodo} {Ruta}.")]
    private static partial void RegistrarPeticionMalFormada(
        ILogger registro, string metodo, string ruta, Exception excepcion);

    [LoggerMessage(Level = LogLevel.Error, Message = "Excepción no controlada al atender {Metodo} {Ruta}.")]
    private static partial void RegistrarExcepcionNoControlada(
        ILogger registro, string metodo, string ruta, Exception excepcion);
}
