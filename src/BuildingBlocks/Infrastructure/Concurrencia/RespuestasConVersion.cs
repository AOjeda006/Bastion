using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Errores;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.BuildingBlocks.Infrastructure.Concurrencia;

/// <summary>
/// Los dos lados de la concurrencia optimista en el borde: publicar la versión al leer y exigirla
/// al escribir.
/// </summary>
/// <remarks>
/// <para>
/// Está en los bloques comunes y no en cada módulo porque es <b>protocolo</b>, no negocio: el
/// <c>ETag</c> se pone igual en un almacén que en una factura, y el <c>428</c> de una petición sin
/// <c>If-Match</c> tiene que ser el mismo en los dos. Cada controlador base lo envuelve en un
/// método protegido para que las acciones se sigan leyendo como las demás.
/// </para>
/// <para>
/// <b>La cabecera llega como parámetro de la acción</b> —<c>[FromHeader(Name = "If-Match")]</c>— y
/// no se lee de <c>Request.Headers</c> aquí dentro. Así el que la exige se ve en la firma: lo
/// documenta OpenAPI, y el barrido que comprueba que ninguna escritura se queda sin ella puede
/// leerlo por reflexión en vez de adivinarlo.
/// </para>
/// </remarks>
public static class RespuestasConVersion
{
    /// <summary>Publica el recurso leído con su <c>ETag</c>.</summary>
    /// <typeparam name="T">El DTO del recurso.</typeparam>
    /// <param name="controlador">El controlador que responde.</param>
    /// <param name="resultado">Desenlace de la lectura.</param>
    public static IActionResult ConEtiqueta<T>(ControllerBase controlador, Resultado<ConVersion<T>> resultado)
    {
        ArgumentNullException.ThrowIfNull(controlador);
        ArgumentNullException.ThrowIfNull(resultado);

        if (!resultado.EsCorrecto)
        {
            return resultado.Error!.AResultadoDeAccion();
        }

        controlador.Response.Headers.ETag = resultado.Valor.Version.Etiqueta;

        return controlador.Ok(resultado.Valor.Recurso);
    }

    /// <summary>Ejecuta la escritura sobre la versión que exige la petición, y responde.</summary>
    /// <typeparam name="T">Lo que devuelve el caso de uso.</typeparam>
    /// <param name="controlador">El controlador que responde.</param>
    /// <param name="ifMatch">El valor de la cabecera <c>If-Match</c>.</param>
    /// <param name="operacion">La escritura, que recibe la versión ya leída.</param>
    public static async Task<IActionResult> ExigiendoVersionAsync<T>(
        ControllerBase controlador,
        string? ifMatch,
        Func<VersionDeRecurso, Task<Resultado<T>>> operacion)
    {
        ArgumentNullException.ThrowIfNull(controlador);
        ArgumentNullException.ThrowIfNull(operacion);

        Resultado<VersionDeRecurso> version = VersionDeRecurso.DeLaCabecera(ifMatch);

        if (!version.EsCorrecto)
        {
            return version.Error!.AResultadoDeAccion();
        }

        Resultado<T> resultado = await operacion(version.Valor).ConfigureAwait(false);

        return resultado.EsCorrecto ? controlador.Ok(resultado.Valor) : resultado.Error!.AResultadoDeAccion();
    }

    /// <summary>Lo mismo, para una escritura que no devuelve nada: <c>204</c>.</summary>
    /// <param name="controlador">El controlador que responde.</param>
    /// <param name="ifMatch">El valor de la cabecera <c>If-Match</c>.</param>
    /// <param name="operacion">La escritura, que recibe la versión ya leída.</param>
    public static async Task<IActionResult> ExigiendoVersionSinContenidoAsync(
        ControllerBase controlador,
        string? ifMatch,
        Func<VersionDeRecurso, Task<Resultado>> operacion)
    {
        ArgumentNullException.ThrowIfNull(controlador);
        ArgumentNullException.ThrowIfNull(operacion);

        Resultado<VersionDeRecurso> version = VersionDeRecurso.DeLaCabecera(ifMatch);

        if (!version.EsCorrecto)
        {
            return version.Error!.AResultadoDeAccion();
        }

        Resultado resultado = await operacion(version.Valor).ConfigureAwait(false);

        return resultado.EsCorrecto ? controlador.NoContent() : resultado.Error!.AResultadoDeAccion();
    }
}
