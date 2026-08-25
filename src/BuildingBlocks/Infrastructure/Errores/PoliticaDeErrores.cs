using System.Diagnostics;
using Bastion.BuildingBlocks.Domain.Resultados;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Bastion.BuildingBlocks.Infrastructure.Errores;

/// <summary>
/// Política ÚNICA y central de traducción de errores hacia fuera: todo lo que sale de la API
/// como error sale en formato <c>ProblemDetails</c> (RFC 9457).
/// </summary>
/// <remarks>
/// <para>
/// Central quiere decir que no hay <c>try/catch</c> por controlador devolviendo un 500 a mano.
/// Un manejador por endpoint deja de cubrir justo los sitios donde no hay endpoint —una ruta
/// que no existe, un fallo de enrutado, una excepción en otro middleware—, que son
/// precisamente los que aparecen a las tres de la mañana.
/// </para>
/// <para>
/// La correspondencia entre clase de error y código de estado (§9) vive AQUÍ y solo aquí. El
/// dominio no sabe que existe HTTP: devuelve un <see cref="TipoDeError"/> y este borde lo
/// traduce. Ver ADR-0004.
/// </para>
/// </remarks>
public static class PoliticaDeErrores
{
    /// <summary>Prefijo de los <c>type</c> de ProblemDetails (§9).</summary>
    public const string BaseDeTipos = "/errors/";

    /// <summary>Registra la política de errores en el contenedor.</summary>
    public static IServiceCollection AgregarPoliticaDeErrores(this IServiceCollection servicios)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        servicios.AddProblemDetails(opciones => opciones.CustomizeProblemDetails =
            contexto => Completar(contexto.ProblemDetails, contexto.HttpContext));
        servicios.AddExceptionHandler<ManejadorDeExcepcionesNoControladas>();

        return servicios;
    }

    /// <summary>
    /// Cablea la política en la tubería. Va lo PRIMERO: un manejador de excepciones solo cubre
    /// lo que tiene por dentro.
    /// </summary>
    public static IApplicationBuilder UsarPoliticaDeErrores(this IApplicationBuilder aplicacion)
    {
        ArgumentNullException.ThrowIfNull(aplicacion);

        aplicacion.UseExceptionHandler();

        // Y también las respuestas de error que NO vienen de una excepción: un 404 de enrutado
        // o un 405 salen sin cuerpo, y un cliente que espera problem+json se encuentra con nada.
        aplicacion.UseStatusCodePages();

        return aplicacion;
    }

    /// <summary>Identificador estable del error como URI, que es contrato publicado.</summary>
    public static string TipoDe(string codigo) => BaseDeTipos + codigo;

    /// <summary>Código de estado HTTP que le corresponde a una clase de error (§9).</summary>
    public static int CodigoDeEstadoDe(TipoDeError tipo) => tipo switch
    {
        TipoDeError.Validacion => StatusCodes.Status400BadRequest,
        TipoDeError.PermisoDenegado => StatusCodes.Status403Forbidden,
        TipoDeError.NoEncontrado => StatusCodes.Status404NotFound,
        TipoDeError.Conflicto => StatusCodes.Status409Conflict,
        TipoDeError.ReglaDeNegocio => StatusCodes.Status422UnprocessableEntity,
        _ => throw new NotSupportedException($"No hay código de estado definido para {tipo}."),
    };

    /// <summary>Resumen corto y estable de la clase de error, para el <c>title</c>.</summary>
    public static string TituloDe(TipoDeError tipo) => tipo switch
    {
        TipoDeError.Validacion => "Datos de entrada no válidos",
        TipoDeError.PermisoDenegado => "Permiso denegado",
        TipoDeError.NoEncontrado => "Recurso no encontrado",
        TipoDeError.Conflicto => "Conflicto con el estado actual",
        TipoDeError.ReglaDeNegocio => "Regla de negocio incumplida",
        _ => throw new NotSupportedException($"No hay título definido para {tipo}."),
    };

    // Lo que TODA respuesta de error lleva, venga de donde venga: de un error de negocio, de una
    // excepción no controlada o de un 404 de enrutado. Un solo sitio, para que no haya respuestas
    // de segunda clase sin identificador de traza.
    private static void Completar(ProblemDetails problema, HttpContext contexto)
    {
        problema.Instance ??= contexto.Request.Path.Value;

        // El MISMO valor que Serilog escribe como @tr, no el TraceIdentifier de Kestrel ni el
        // `traceparent` entero: si no coinciden, pedir "dime el traceId" no localiza nada.
        problema.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? contexto.TraceIdentifier;
    }
}
