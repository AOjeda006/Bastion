using Bastion.BuildingBlocks.Application.Multiempresa;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bastion.BuildingBlocks.Infrastructure.Multiempresa;

/// <summary>Registro del inquilinato (R8) en el <i>composition root</i>.</summary>
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

        return servicios;
    }
}
