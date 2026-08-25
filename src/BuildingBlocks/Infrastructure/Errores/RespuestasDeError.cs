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

        return new RespuestaDeProblema(new ProblemDetails
        {
            Status = PoliticaDeErrores.CodigoDeEstadoDe(error.Tipo),
            Type = PoliticaDeErrores.TipoDe(error.Codigo),
            Title = PoliticaDeErrores.TituloDe(error.Tipo),

            // El mensaje del error de operación ya está escrito PARA FUERA: dice qué hacer. No
            // es el texto de ninguna excepción, y por eso puede publicarse tal cual.
            Detail = error.Mensaje,
        });
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
