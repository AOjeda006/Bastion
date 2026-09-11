using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.BuildingBlocks.Infrastructure.Errores;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Catalogo.Endpoints.Comun;

/// <summary>
/// Lo que comparten los controladores del módulo: la ruta base, y cómo se convierte un
/// <see cref="Resultado"/> en respuesta.
/// </summary>
/// <remarks>
/// <para>
/// La conversión está aquí y no repetida en cada acción porque es donde se decide que un error de
/// negocio salga con su código de estado y su ProblemDetails. Escrita veinte veces, la número
/// diecisiete devolvería un 400 donde tocaba un 409 y nadie lo vería hasta que un cliente
/// ramificara mal.
/// </para>
/// <para>
/// Deriva de <see cref="ControllerBase"/> y no de <c>Controller</c>: esto es una API, no un sitio
/// con vistas, y <c>Controller</c> arrastra todo el aparato de Razor.
/// </para>
/// <para>
/// <b>Sin <c>[Produces("application/json")]</c>, y no por olvido.</b> Ese atributo no documenta:
/// SUSTITUYE los tipos de contenido de cualquier <c>ObjectResult</c>, y el <c>400</c> automático
/// de <c>[ApiController]</c> es uno. Con él puesto, un error de enlace de modelo saldría como
/// <c>application/json</c> en vez de <c>application/problem+json</c>, así que un cliente que
/// ramificara por el tipo de contenido —lo que manda la RFC 9457— no reconocería como problema
/// justo el error más frecuente.
/// </para>
/// </remarks>
[ApiController]
[Route(RutaBase)]
public abstract class ControladorDeCatalogo : ControllerBase
{
    /// <summary>Prefijo del módulo, con la versión desde el primer día (§9).</summary>
    public const string Prefijo = "api/v1/catalogo";

    /// <summary>Ruta base del módulo: el prefijo más el nombre del controlador.</summary>
    public const string RutaBase = Prefijo + "/[controller]";

    /// <summary>
    /// Atiende un listado: valida el orden pedido contra lo que el listado admite y responde con
    /// la página, o con un <c>400</c> que dice qué campos valen.
    /// </summary>
    /// <typeparam name="TDto">Lo que se publica de cada elemento.</typeparam>
    /// <param name="consulta">Los parámetros tal como han llegado en la URL.</param>
    /// <param name="listado">El caso de uso, que es quien dice por qué campos deja ordenar.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    protected Task<IActionResult> ResponderListadoAsync<TDto>(
        ConsultaPaginada consulta,
        IListado<TDto> listado,
        CancellationToken cancelacion) =>
        RespuestasDeListado.ResponderAsync(this, consulta, listado, cancelacion);

    /// <summary>Lo mismo, para un listado que además recibe un criterio propio suyo.</summary>
    /// <typeparam name="TDto">Lo que se publica de cada elemento.</typeparam>
    /// <param name="consulta">Los parámetros tal como han llegado en la URL.</param>
    /// <param name="ordenables">Quien dice por qué campos deja ordenar este listado.</param>
    /// <param name="ejecutar">La llamada al caso de uso, ya cerrada sobre sus criterios.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    protected Task<IActionResult> ResponderListadoAsync<TDto>(
        ConsultaPaginada consulta,
        IOrdenaPor ordenables,
        Func<Paginacion, CancellationToken, Task<PaginaDe<TDto>>> ejecutar,
        CancellationToken cancelacion) =>
        RespuestasDeListado.ResponderAsync(this, consulta, ordenables, ejecutar, cancelacion);

    /// <summary>
    /// Lo mismo, para un listado que cuelga de otro recurso y por tanto puede no llegar a haber
    /// página: si el padre no existe, <c>404</c> y no una página vacía con un <c>200</c>.
    /// </summary>
    /// <typeparam name="TDto">Lo que se publica de cada elemento.</typeparam>
    /// <param name="consulta">Los parámetros tal como han llegado en la URL.</param>
    /// <param name="ordenables">Quien dice por qué campos deja ordenar este listado.</param>
    /// <param name="ejecutar">La llamada al caso de uso, ya cerrada sobre sus criterios.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    protected Task<IActionResult> ResponderListadoDeResultadoAsync<TDto>(
        ConsultaPaginada consulta,
        IOrdenaPor ordenables,
        Func<Paginacion, CancellationToken, Task<Resultado<PaginaDe<TDto>>>> ejecutar,
        CancellationToken cancelacion) =>
        RespuestasDeListado.ResponderResultadoAsync(
            this, consulta, ordenables, ejecutar, cancelacion);

    /// <summary>Convierte el desenlace de un caso de uso que devuelve valor en respuesta.</summary>
    /// <typeparam name="T">Lo que devuelve el caso de uso.</typeparam>
    /// <param name="resultado">Desenlace del caso de uso.</param>
    protected IActionResult Responder<T>(Resultado<T> resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        return resultado.EsCorrecto ? Ok(resultado.Valor) : resultado.Error!.AResultadoDeAccion();
    }

    /// <summary>
    /// Convierte el desenlace de una creación en respuesta: <c>201</c> con <c>Location</c>.
    /// </summary>
    /// <typeparam name="T">Lo que devuelve el caso de uso.</typeparam>
    /// <param name="resultado">Desenlace del caso de uso.</param>
    /// <param name="accionDeConsulta">Nombre de la acción que devuelve el recurso creado.</param>
    /// <param name="id">Identificador del recurso creado.</param>
    protected IActionResult ResponderCreado<T>(
        Resultado<T> resultado,
        string accionDeConsulta,
        Func<T, Guid> id)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(id);

        return resultado.EsCorrecto
            ? CreatedAtAction(accionDeConsulta, new { id = id(resultado.Valor) }, resultado.Valor)
            : resultado.Error!.AResultadoDeAccion();
    }

    /// <summary>
    /// Publica un recurso leído con su <c>ETag</c>, que es lo que el cliente devolverá en
    /// <c>If-Match</c> cuando lo escriba.
    /// </summary>
    /// <typeparam name="T">Lo que devuelve el caso de uso.</typeparam>
    /// <param name="resultado">Desenlace de la lectura.</param>
    protected IActionResult ResponderConVersion<T>(Resultado<ConVersion<T>> resultado) =>
        RespuestasConVersion.ConEtiqueta(this, resultado);

    /// <summary>
    /// Ejecuta una escritura sobre la versión que exige la petición: sin <c>If-Match</c> responde
    /// <c>428</c>, con una cabecera ilegible <c>400</c>, y si la versión ya no es la actual el
    /// guardado falla y la política central responde <c>412</c>.
    /// </summary>
    /// <typeparam name="T">Lo que devuelve el caso de uso.</typeparam>
    /// <param name="ifMatch">Valor de la cabecera <c>If-Match</c>.</param>
    /// <param name="operacion">La escritura, que recibe la versión ya leída.</param>
    protected Task<IActionResult> ResponderExigiendoVersionAsync<T>(
        string? ifMatch,
        Func<VersionDeRecurso, Task<Resultado<T>>> operacion) =>
        RespuestasConVersion.ExigiendoVersionAsync(this, ifMatch, operacion);
}
