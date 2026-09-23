using System.Diagnostics;
using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Bastion.BuildingBlocks.Infrastructure.Errores;

/// <summary>
/// Traduce la violación de un índice único <b>declarado</b> en <see cref="IndicesQueDelatanUnaCarreraPerdida"/>
/// al mismo <c>412</c> que da el testigo de concurrencia.
/// </summary>
/// <remarks>
/// <para>
/// <b>Existe porque una garantía y un código de estado no son lo mismo, y se pueden tener los
/// dos.</b> Cuando una operación escribe una fila nueva y cambia la de al lado, cuál de las dos
/// llega antes a la base lo decide el ORM. Si llega primero el <c>INSERT</c>, quien pierde la
/// carrera sale por una violación de unicidad —un <see cref="DbUpdateException"/>, o sea un
/// <c>500</c>— en vez de por el testigo, que es un <c>412</c>. La tentación es quitar el índice
/// para «arreglar» la respuesta; eso cambia una garantía del motor por un código de estado, y deja
/// el dato malo pudiendo entrar por cualquier camino futuro que no toque la fila con testigo. Lo
/// que se arregla aquí es la respuesta, y el índice se queda.
/// </para>
/// <para>
/// <b>Por NOMBRE de índice y nunca por código de error a secas.</b> Un <c>23505</c> cualquiera
/// sigue siendo un <c>500</c>: dos altas con el mismo NIF son un defecto, no una carrera, y
/// contestarlas con <c>412</c> mandaría al cliente a releer y reintentar para siempre. Lo que
/// distingue a un índice del otro es una decisión escrita, no una propiedad de la excepción.
/// </para>
/// <para>
/// <b>Va registrado ENTRE los dos manejadores que ya había</b>: después del testigo —que es el
/// camino normal y el que lleva la versión dentro— y antes del general, que atrapa cualquier cosa
/// y responde <c>500</c>. Registrado el último no se ejecutaría nunca.
/// </para>
/// <para>
/// <b>Y sale sin <c>versionActual</c>, que es la única diferencia con el testigo y se dice aquí en
/// vez de disimularla.</b> Un <c>23505</c> aborta la transacción de PostgreSQL: desde dentro no se
/// puede leer nada más, y para cuando esto corre, el dueño de la transacción ya la ha deshecho y
/// las entidades que chocaron son filas que no llegaron a existir. La versión que el cliente
/// necesita es la de <i>otro</i> recurso —aquel contra el que se perdió la carrera—, y averiguarlo
/// desde aquí obligaría a este manejador a conocer la forma de cada agregado que se declare. El
/// código del error es el mismo (<c>version-obsoleta</c>), el estado es el mismo, y la acción que
/// el cliente tiene que tomar es la misma: releer. Se usa la variante que el contrato ya publica
/// para «no hay versión que dar», no una nueva.
/// </para>
/// </remarks>
/// <param name="indices">Los índices declarados como carrera perdida, con su motivo. Llegan
/// por <c>IOptions</c> y no por instancia para que el orden de los registros no decida nada: un
/// módulo declara el suyo con <c>Configure</c>, se llame antes o después de la política.</param>
/// <param name="problemas">Quien escribe el <c>ProblemDetails</c> en la respuesta.</param>
/// <param name="registro">Registro estructurado.</param>
internal sealed partial class ManejadorDeCarreraPerdidaEnLaBase(
    IOptions<IndicesQueDelatanUnaCarreraPerdida> indices,
    IProblemDetailsService problemas,
    ILogger<ManejadorDeCarreraPerdidaEnLaBase> registro) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(indices);

        IReadOnlyDictionary<string, string> declarados = indices.Value.Motivos;

        if (exception is not DbUpdateException fallo || IndiceDe(fallo) is not { } indice)
        {
            return false;
        }

        if (!declarados.TryGetValue(indice, out string? motivo))
        {
            // Un 23505 de un índice NO declarado se deja pasar al manejador general, que es lo
            // correcto: casi siempre es un defecto y merece su 500 con su traza.
            return false;
        }

        string metodo = httpContext.Request.Method;
        string ruta = httpContext.Request.Path.Value ?? string.Empty;
        string traza = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;

        RegistrarCarreraPerdida(registro, metodo, ruta, indice, motivo, traza, exception);

        ProblemDetails problema = ErroresDeConcurrencia.ObsoletaYSinRecurso().AProblema();

        httpContext.Response.StatusCode = StatusCodes.Status412PreconditionFailed;

        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problema,
        }).ConfigureAwait(false);
    }

    // El nombre del índice que se ha violado, o nada si lo que llegó no es una violación de
    // unicidad. Se recorre la cadena de excepciones porque EF Core envuelve la del motor, y puede
    // envolverla a más de un nivel según por dónde venga el `SaveChanges`.
    private static string? IndiceDe(Exception fallo)
    {
        for (Exception? actual = fallo; actual is not null; actual = actual.InnerException)
        {
            if (actual is PostgresException postgres
                && string.Equals(postgres.SqlState, ViolacionDeUnicidad, StringComparison.Ordinal))
            {
                return postgres.ConstraintName;
            }
        }

        return null;
    }

    // `23505 unique_violation`, tal cual lo publica PostgreSQL. Escrito y no calculado: el catálogo
    // de códigos del motor es contrato suyo y no cambia, y una constante con nombre se lee.
    private const string ViolacionDeUnicidad = "23505";

    [LoggerMessage(
        EventId = 8502,
        Level = LogLevel.Information,
        Message = "Carrera perdida en {Metodo} {Ruta}: el índice {Indice} la impidió en la base, " +
                  "así que se contesta 412 y no 500. Motivo declarado: {Motivo}. Traza: {Traza}.")]
    private static partial void RegistrarCarreraPerdida(
        ILogger registro,
        string metodo,
        string ruta,
        string indice,
        string motivo,
        string traza,
        Exception excepcion);
}
