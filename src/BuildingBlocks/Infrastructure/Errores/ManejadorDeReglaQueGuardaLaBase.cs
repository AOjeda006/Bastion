using System.Diagnostics;
using Bastion.BuildingBlocks.Domain.Resultados;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Bastion.BuildingBlocks.Infrastructure.Errores;

/// <summary>
/// Traduce la violación de una restricción <c>CHECK</c> <b>declarada</b> en
/// <see cref="RestriccionesQueGuardanUnaRegla"/> al error que su módulo declaró para ella.
/// </summary>
/// <remarks>
/// <para>
/// <b>Existe porque hay reglas que solo el motor puede guardar.</b> Que el stock no baje de cero
/// no se comprueba leyendo antes: entre la lectura y la escritura cabe otra salida, y las dos
/// pasarían la comprobación juntas. El <c>CHECK</c> se evalúa con la fila ya bloqueada y el valor
/// ya sumado, y es la única guarda que dos peticiones simultáneas no cruzan a la vez. Lo que se
/// arregla aquí es la respuesta: sin esto, la regla más ordinaria del almacén saldría como un
/// <c>500</c>.
/// </para>
/// <para>
/// <b>En el borde y no en el repositorio</b> (ADR-0004, ADR-0046 §4). La infraestructura lanza, y
/// el <c>Resultado</c> solo cruza de Aplicación al borde. Atrapar la excepción en el repositorio
/// sería traducir en la infraestructura, y además con la transacción ya abortada: desde dentro no
/// se puede hacer nada más con ella.
/// </para>
/// <para>
/// <b>Por NOMBRE de restricción y nunca por código de error a secas.</b> Un <c>23514</c> cualquiera
/// sigue siendo un <c>500</c>: casi toda restricción guarda un invariante que el dominio ya
/// comprueba, y si salta es un defecto que merece su traza. Lo que distingue una de otra es una
/// decisión escrita, no una propiedad de la excepción.
/// </para>
/// <para>
/// <b>Es un manejador distinto del de la carrera perdida</b>, y no una rama suya, porque aquel
/// traduce siempre al mismo <c>412</c> sin código de negocio y aquí cada restricción lleva su propio
/// error. Va registrado <b>entre</b> ese y el general, por lo mismo que aquel: el general atrapa
/// cualquier cosa, y registrado después no se ejecutaría nunca.
/// </para>
/// </remarks>
/// <param name="restricciones">Las restricciones declaradas, con su error. Llegan por
/// <c>IOptions</c> para que el orden de los registros no decida nada: un módulo declara la suya con
/// <c>Configure</c>, se llame antes o después de la política.</param>
/// <param name="problemas">Quien escribe el <c>ProblemDetails</c> en la respuesta.</param>
/// <param name="registro">Registro estructurado.</param>
internal sealed partial class ManejadorDeReglaQueGuardaLaBase(
    IOptions<RestriccionesQueGuardanUnaRegla> restricciones,
    IProblemDetailsService problemas,
    ILogger<ManejadorDeReglaQueGuardaLaBase> registro) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(restricciones);

        if (RestriccionDe(exception) is not { } restriccion
            || !restricciones.Value.Declaradas.TryGetValue(restriccion, out RestriccionDeclarada? declarada))
        {
            // Un 23514 de una restricción NO declarada se deja pasar al manejador general: casi
            // siempre es un defecto y merece su 500 con su traza.
            return false;
        }

        string metodo = httpContext.Request.Method;
        string ruta = httpContext.Request.Path.Value ?? string.Empty;
        string traza = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;

        RegistrarReglaGuardadaPorLaBase(
            registro, metodo, ruta, restriccion, declarada.Error.Codigo, traza, exception);

        ProblemDetails problema = declarada.Error.AProblema();

        httpContext.Response.StatusCode = PoliticaDeErrores.CodigoDeEstadoDe(declarada.Error.Tipo);

        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problema,
        }).ConfigureAwait(false);
    }

    // El nombre de la restricción violada, o nada si lo que llegó no es un `23514`. Se recorre la
    // cadena porque la excepción del motor llega sola cuando la lanza una sentencia cruda, y
    // envuelta en la de EF Core cuando la lanza un `SaveChanges`.
    private static string? RestriccionDe(Exception fallo)
    {
        for (Exception? actual = fallo; actual is not null; actual = actual.InnerException)
        {
            if (actual is PostgresException postgres
                && string.Equals(postgres.SqlState, ViolacionDeRestriccion, StringComparison.Ordinal))
            {
                return postgres.ConstraintName;
            }
        }

        return null;
    }

    // `23514 check_violation`, tal cual lo publica PostgreSQL.
    private const string ViolacionDeRestriccion = "23514";

    [LoggerMessage(
        EventId = 8503,
        Level = LogLevel.Information,
        Message = "Regla guardada por la base en {Metodo} {Ruta}: la restricción {Restriccion} " +
                  "rechazó la escritura, así que se contesta {Codigo} y no 500. Traza: {Traza}.")]
    private static partial void RegistrarReglaGuardadaPorLaBase(
        ILogger registro,
        string metodo,
        string ruta,
        string restriccion,
        string codigo,
        string traza,
        Exception excepcion);
}
