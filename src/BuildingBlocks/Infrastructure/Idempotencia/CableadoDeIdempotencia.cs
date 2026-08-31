using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bastion.BuildingBlocks.Infrastructure.Idempotencia;

/// <summary>Registro del mecanismo de idempotencia (R10) en el <i>composition root</i>.</summary>
public static class CableadoDeIdempotencia
{
    /// <summary>
    /// Pone el filtro en la tubería de MVC, para todas las acciones.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Global y no acción por acción</b>, aunque solo unas pocas admitan la cabecera. Es lo que
    /// permite que una acción NO marcada que reciba una <c>Idempotency-Key</c> conteste
    /// <c>400</c> en vez de ignorarla: un filtro puesto solo donde el mecanismo se ofrece nunca
    /// llegaría a ver esas peticiones. Cuando no hay cabecera, el filtro se aparta en la primera
    /// línea y no toca nada.
    /// </para>
    /// <para>
    /// Va <b>antes</b> de los módulos, como el inquilinato, la auditoría y la bandeja: los almacenes
    /// que resuelve por clave se registran después, cada uno con su módulo.
    /// </para>
    /// </remarks>
    /// <param name="servicios">Colección de servicios del <i>composition root</i>.</param>
    /// <returns>La misma colección, para encadenar.</returns>
    public static IServiceCollection AgregarIdempotencia(this IServiceCollection servicios)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        servicios.TryAddSingleton(TimeProvider.System);

        servicios.Configure<MvcOptions>(opciones => opciones.Filters.Add<FiltroDeIdempotencia>());

        return servicios;
    }

    /// <summary>
    /// Registra el almacén de un módulo, bajo la clave con la que ese módulo aparece en la ruta.
    /// </summary>
    /// <remarks>
    /// <b>Con clave, y la clave es el segmento de la ruta.</b> Registrado bajo el tipo a secas, el
    /// último módulo que se registrara desplazaría a los demás y las claves de Organización se
    /// apuntarían en la transacción de Identidad: dos transacciones distintas para un trabajo que
    /// tenía que ser uno, sin error y sin rastro. Es la misma trampa que la unidad de trabajo del
    /// 0.4, resuelta aquí con clave en vez de con un tipo por módulo porque lo que varía es el
    /// contexto, no el contrato.
    /// </remarks>
    /// <typeparam name="T">El almacén del módulo.</typeparam>
    /// <param name="servicios">Colección de servicios.</param>
    /// <param name="modulo">
    /// El segmento del módulo en <c>/api/v1/{modulo}/…</c>, que es también el nombre de su esquema.
    /// </param>
    /// <returns>La misma colección, para encadenar.</returns>
    public static IServiceCollection AgregarAlmacenDeIdempotencia<T>(
        this IServiceCollection servicios, string modulo)
        where T : class, IAlmacenDeIdempotencia
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentException.ThrowIfNullOrWhiteSpace(modulo);

        return servicios.AddKeyedScoped<IAlmacenDeIdempotencia, T>(modulo);
    }
}
