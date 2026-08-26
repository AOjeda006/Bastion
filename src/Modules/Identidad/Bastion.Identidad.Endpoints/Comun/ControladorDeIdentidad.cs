using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Errores;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Identidad.Endpoints.Comun;

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
/// de <c>[ApiController]</c> es uno. Con él puesto, un error de enlace de modelo salía como
/// <c>application/json</c> en vez de <c>application/problem+json</c>, así que un cliente que
/// ramificara por el tipo de contenido —lo que manda la RFC 9457— no reconocía como problema justo
/// el error más frecuente. Lo que sí documenta, y no cambia nada en ejecución, es
/// <c>[ProducesResponseType]</c> en cada acción.
/// </para>
/// </remarks>
[ApiController]
[Route(RutaBase)]
public abstract class ControladorDeIdentidad : ControllerBase
{
    /// <summary>Ruta base del módulo, con la versión desde el primer día (§9).</summary>
    public const string RutaBase = "api/v1/identidad/[controller]";

    /// <summary>Convierte el desenlace de un caso de uso que devuelve valor en respuesta.</summary>
    /// <typeparam name="T">Lo que devuelve el caso de uso.</typeparam>
    /// <param name="resultado">Desenlace del caso de uso.</param>
    protected IActionResult Responder<T>(Resultado<T> resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        return resultado.EsCorrecto ? Ok(resultado.Valor) : resultado.Error!.AResultadoDeAccion();
    }

    /// <summary>Convierte el desenlace de un caso de uso sin valor en respuesta.</summary>
    /// <remarks>
    /// El caso correcto es <c>204</c>: la operación salió bien y no hay nada que contar. Devolver
    /// un <c>200</c> con el cuerpo vacío obliga al cliente a intentar leer un JSON que no está.
    /// </remarks>
    /// <param name="resultado">Desenlace del caso de uso.</param>
    protected IActionResult ResponderSinContenido(Resultado resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        return resultado.EsCorrecto ? NoContent() : resultado.Error!.AResultadoDeAccion();
    }

    /// <summary>
    /// Convierte el desenlace de una creación en respuesta: <c>201</c> con <c>Location</c>.
    /// </summary>
    /// <remarks>
    /// El <c>Location</c> no es adorno: es lo que le dice al cliente dónde ha quedado lo que acaba
    /// de crear, sin que tenga que componer la URL él a partir del identificador y romperse el día
    /// que la ruta cambie (§9).
    /// </remarks>
    /// <typeparam name="T">Lo que devuelve el caso de uso.</typeparam>
    /// <param name="resultado">Desenlace del caso de uso.</param>
    /// <param name="accionDeConsulta">Nombre de la acción que devuelve el recurso creado.</param>
    /// <param name="id">Identificador del recurso creado.</param>
    protected IActionResult ResponderCreado<T>(Resultado<T> resultado, string accionDeConsulta, Func<T, Guid> id)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(id);

        return resultado.EsCorrecto
            ? CreatedAtAction(accionDeConsulta, new { id = id(resultado.Valor) }, resultado.Valor)
            : resultado.Error!.AResultadoDeAccion();
    }
}
