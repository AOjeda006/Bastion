using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Bloqueos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bastion.BuildingBlocks.Infrastructure.Multiempresa;

/// <summary>Registro del inquilinato (R8) y del acceso a lo bloqueado (R16).</summary>
public static class Inquilinato
{
    /// <summary>
    /// Registra de dónde sale la empresa por la que filtran los <c>DbContext</c> de módulo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Va <b>antes</b> de los módulos y no dentro de ninguno: los tres que hoy tienen persistencia
    /// dependen de esto, y si cada uno lo registrara por su cuenta habría tres criterios sobre
    /// quién es el inquilino en la misma base de datos.
    /// </para>
    /// <para>
    /// Es <c>scoped</c>: uno por petición, la misma vida que los contextos que lo consultan. Y no
    /// hace falta acordarse de llamarlo para que el sistema falle bien: un host que se lo salte no
    /// resuelve ningún <c>DbContext</c> y no llega a atender la primera petición.
    /// </para>
    /// </remarks>
    /// <param name="servicios">Colección de servicios del <i>composition root</i>.</param>
    /// <returns>La misma colección, para encadenar.</returns>
    public static IServiceCollection AgregarInquilinato(this IServiceCollection servicios)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        servicios.AddHttpContextAccessor();
        servicios.TryAddScoped<IInquilinoActual, InquilinoActual>();

        // Y con él, la puerta declarada de R16. Va aquí y no en un método aparte porque es la
        // otra mitad de lo mismo: los dos son cosas que un `DbContext` de módulo necesita para
        // construirse, y separarlos dejaría un host que registra una y se olvida de la otra
        // reventando al resolver el primer contexto, en vez de no compilar.
        servicios.TryAddScoped<IAccesoALoBloqueado, AccesoALoBloqueado>();

        // Y con QUIEN pregunta, que desde el ítem 1.4 es una dependencia de la línea anterior: la
        // apertura del ámbito anota en el registro el usuario además del motivo, porque una traza
        // del art. 32 que dice que alguien miró sin decir quién no es una traza.
        //
        // Está aquí y no solo en `AgregarAutorizacionPorPermisos` por lo mismo que dice el párrafo
        // de arriba, y esta vez comprobado por el efecto: un host que registra el inquilinato SIN
        // la autorización —el del publicador de la bandeja, que corre fuera de una petición y no
        // necesita permisos— dejó de poder construir `AccesoALoBloqueado` y tumbó diez casos de
        // integración con «Unable to resolve service for type 'IUsuarioActual'». `TryAdd`, así que
        // el host completo, que sí llama a las dos, sigue registrándolo una sola vez.
        //
        // `UsuarioActual` fuera de una petición contesta «no autenticado» en vez de reventar
        // —lee `IHttpContextAccessor.HttpContext?.User`, que ahí es nulo—, que es justo lo que el
        // trabajo de fondo necesita: ámbito abierto y usuario nulo, escrito como nulo.
        servicios.TryAddScoped<IUsuarioActual, UsuarioActual>();

        return servicios;
    }
}
