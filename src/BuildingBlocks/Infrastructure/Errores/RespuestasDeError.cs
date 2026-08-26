using Bastion.BuildingBlocks.Domain.Resultados;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Bastion.BuildingBlocks.Infrastructure.Errores;

/// <summary>
/// Traduce un <see cref="ErrorDeOperacion"/> —el desenlace fallido que devuelve la capa de
/// aplicación— en la respuesta HTTP que le corresponde.
/// </summary>
/// <remarks>
/// Esta es la costura de la que habla el ADR-0004: el <see cref="Resultado"/> llega hasta aquí
/// y aquí se acaba. Más allá de este punto hay HTTP; más acá, no.
/// </remarks>
public static class RespuestasDeError
{
    /// <summary>Respuesta <c>ProblemDetails</c> que corresponde a un error de operación.</summary>
    public static IResult ARespuesta(this ErrorDeOperacion error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var problema = new ProblemDetails
        {
            Status = PoliticaDeErrores.CodigoDeEstadoDe(error.Tipo),
            Type = PoliticaDeErrores.TipoDe(error.Codigo),
            Title = PoliticaDeErrores.TituloDe(error.Tipo),

            // El mensaje del error de operación ya está escrito PARA FUERA: dice qué hacer. No
            // es el texto de ninguna excepción, y por eso puede publicarse tal cual.
            Detail = error.Mensaje,
        };

        // Errores por campo (§9) en la extensión `errors` del MISMO ProblemDetails, con la forma
        // exacta que ya usa el 400 automático de [ApiController]: un cliente lee los dos igual, sin
        // saber si el fallo lo detectó el enlace del modelo o el caso de uso. La clave se omite
        // cuando no hay campos, en lugar de mandar un objeto vacío que el cliente tendría que
        // distinguir de «no lo sé».
        if (error.Campos.Count > 0)
        {
            problema.Extensions["errors"] = error.Campos;
        }

        return new RespuestaDeProblema(problema);
    }

    /// <summary>
    /// La misma respuesta, en la forma que devuelve un controlador de MVC.
    /// </summary>
    /// <remarks>
    /// Un adaptador de diez líneas, y no una segunda política: la respuesta la sigue construyendo
    /// <see cref="ARespuesta"/>, así que un endpoint mínimo y un controlador devuelven exactamente
    /// el mismo cuerpo. Dos caminos que construyen el ProblemDetails por su cuenta es como se
    /// acaba teniendo respuestas de error de segunda clase, sin identificador de traza (§9).
    /// </remarks>
    public static IActionResult AResultadoDeAccion(this ErrorDeOperacion error) =>
        new ResultadoDeAccion(error.ARespuesta());

    // MVC espera un IActionResult y aquí hay un IResult. Lo único que hace falta es dejar que el
    // segundo se ejecute donde el primero iba a hacerlo.
    private sealed class ResultadoDeAccion(IResult respuesta) : IActionResult
    {
        public Task ExecuteResultAsync(ActionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return respuesta.ExecuteAsync(context.HttpContext);
        }
    }

    // Se escribe a través de IProblemDetailsService, y no con Results.Problem, para que pase por
    // la misma personalización central que el resto: si no, estas respuestas serían las únicas
    // sin identificador de traza.
    private sealed class RespuestaDeProblema(ProblemDetails problema) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            httpContext.Response.StatusCode = problema.Status ?? StatusCodes.Status500InternalServerError;

            IProblemDetailsService servicio = httpContext.RequestServices
                .GetRequiredService<IProblemDetailsService>();

            await servicio.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problema,
            });
        }
    }
}
