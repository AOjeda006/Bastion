using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bastion.BuildingBlocks.Infrastructure.Auditoria;

/// <summary>Registro de la auditoría (R11) en el <i>composition root</i>.</summary>
public static class CableadoDeAuditoria
{
    /// <summary>
    /// Registra el interceptor que escribe la traza. <b>Enchufarlo a cada contexto es aparte</b>:
    /// se hace en el <c>AddDbContext</c> de cada módulo, con su línea a la vista.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Va <b>antes</b> de los módulos, por lo mismo que el inquilinato: los contextos que lo
    /// consumen se registran después. Y es <c>scoped</c>, la misma vida que el contexto que lo usa
    /// y que el inquilino y el usuario de los que lee — un interceptor <i>singleton</i> se quedaría
    /// con el usuario de la primera petición y firmaría con su nombre los cambios de todas las
    /// demás.
    /// </para>
    /// <para>
    /// El reloj se registra aquí también, y con <c>TryAdd</c>: la traza lleva un instante, y el
    /// módulo que ya lo registraba (Identidad) no tiene por qué ser el que arranque primero.
    /// </para>
    /// </remarks>
    /// <param name="servicios">Colección de servicios del <i>composition root</i>.</param>
    /// <returns>La misma colección, para encadenar.</returns>
    public static IServiceCollection AgregarAuditoria(this IServiceCollection servicios)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        servicios.TryAddSingleton(TimeProvider.System);
        servicios.TryAddScoped<InterceptorDeAuditoria>();

        return servicios;
    }
}
