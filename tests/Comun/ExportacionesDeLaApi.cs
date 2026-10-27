using Bastion.BuildingBlocks.Application.Listados;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;

namespace Bastion.Pruebas.Comun;

/// <summary>
/// Las exportaciones que publica la API, tal como las ve su explorador, y la foto que pide cada una
/// (ADR-0073).
/// </summary>
/// <remarks>
/// <b>Compartido y enlazado</b>, como <see cref="ListadosDeLaApi"/>: lo usan la regla de las
/// hermanas, en el carril funcional, y la de la foto de cada módulo, contra la base. Si las dos
/// reconocieran las exportaciones cada una a su manera, una podría dejar de ver una que la otra
/// sigue viendo, y la primera saldría verde por no mirarla.
/// </remarks>
public static class ExportacionesDeLaApi
{
    /// <summary>Cómo acaba la ruta de una exportación: la de su listado, más esto.</summary>
    public const string Segmento = "/exportacion";

    /// <summary>Las exportaciones de la API que sirve <paramref name="servicios"/>.</summary>
    /// <param name="servicios">Los servicios de la aplicación, ya construida.</param>
    public static IEnumerable<ApiDescription> De(IServiceProvider servicios)
    {
        IApiDescriptionGroupCollectionProvider explorador =
            servicios.GetRequiredService<IApiDescriptionGroupCollectionProvider>();

        return from grupo in explorador.ApiDescriptionGroups.Items
               from descripcion in grupo.Items
               where ListadosDeLaApi.SeConsulta(descripcion)
                   && descripcion.RelativePath!.EndsWith(Segmento, StringComparison.Ordinal)
               select descripcion;
    }

    /// <summary>Los parámetros de la acción que son una foto, por su tipo.</summary>
    /// <param name="exportacion">La exportación.</param>
    public static Type[] Fotos(ApiDescription exportacion)
    {
        ArgumentNullException.ThrowIfNull(exportacion);

        if (exportacion.ActionDescriptor is not ControllerActionDescriptor accion)
        {
            throw new InvalidOperationException($"{ListadosDeLaApi.Nombre(exportacion)} no es una acción de controlador.");
        }

        return
        [
            .. accion.MethodInfo.GetParameters()
                .Select(parametro => parametro.ParameterType)
                .Where(tipo => typeof(IFotoDeLectura).IsAssignableFrom(tipo)),
        ];
    }
}
