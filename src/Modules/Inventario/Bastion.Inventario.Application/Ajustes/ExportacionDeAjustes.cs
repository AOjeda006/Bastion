using Bastion.BuildingBlocks.Application.Exportacion;
using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Maestros;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Inventario.Contracts.Ajustes;

namespace Bastion.Inventario.Application.Ajustes;

/// <summary>
/// Un ajuste como sale en su exportación: lo que publica el listado, con su almacén nombrado
/// (ADR-0074).
/// </summary>
/// <param name="Ajuste">El ajuste, tal como lo publica el listado.</param>
/// <param name="Almacen">El almacén del ajuste, de Organización.</param>
public sealed record AjusteExportado(AjusteResumenDto Ajuste, MaestroCitado Almacen);

/// <summary>
/// Una página interna de la exportación de ajustes: la del listado, con el almacén nombrado.
/// </summary>
/// <remarks>
/// <b>Llama al caso de uso del listado</b>, así que el orden, el texto, el estado, el almacén y lo
/// que la empresa ve son los suyos por construcción. Lo que añade es el nombre del almacén, una
/// pregunta por página, y solo con el permiso de verlo (ADR-0074 §3).
/// </remarks>
public interface IExportarAjustes : IOrdenaPor
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="paginacion">Qué página interna, con qué orden y qué filtro.</param>
    /// <param name="estado">El estado al que se acota, ya validado por el borde, o nulo.</param>
    /// <param name="almacenId">El almacén al que se acota, o nulo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<PaginaDe<AjusteExportado>> EjecutarAsync(
        Paginacion paginacion,
        string? estado,
        Guid? almacenId,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IExportarAjustes"/>
internal sealed class ExportarAjustes(IListarAjustes listar, LosNombresDeLosMaestros nombres) : IExportarAjustes
{
    public IReadOnlySet<string> CamposOrdenables => listar.CamposOrdenables;

    public async Task<PaginaDe<AjusteExportado>> EjecutarAsync(
        Paginacion paginacion,
        string? estado,
        Guid? almacenId,
        CancellationToken cancelacion)
    {
        PaginaDe<AjusteResumenDto> pagina = await listar
            .EjecutarAsync(paginacion, estado, almacenId, cancelacion)
            .ConfigureAwait(false);

        IReadOnlyDictionary<Guid, NombreDeMaestro> deAlmacenes = await nombres
            .DeAlmacenesAsync(pagina.Elementos.Select(ajuste => (Guid?)ajuste.AlmacenId), cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<AjusteExportado>(
            [
                .. pagina.Elementos.Select(ajuste =>
                    new AjusteExportado(ajuste, MaestroCitado.De(ajuste.AlmacenId, deAlmacenes))),
            ],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}
