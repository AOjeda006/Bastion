using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Domain.Series;

namespace Bastion.Organizacion.Application.Series;

/// <summary>Devuelve una serie por su identificador.</summary>
public interface IObtenerSerie
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la serie.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<SerieDto>> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <summary>Devuelve una página de series.</summary>
public interface IListarSeries
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="paginacion">Qué página se pide y de qué tamaño.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<PaginaDe<SerieDto>> EjecutarAsync(Paginacion paginacion, CancellationToken cancelacion);
}

/// <inheritdoc cref="IObtenerSerie"/>
internal sealed class ObtenerSerie(IRepositorioDeSeries series) : IObtenerSerie
{
    public async Task<Resultado<SerieDto>> EjecutarAsync(Guid id, CancellationToken cancelacion)
    {
        Serie? serie = await series.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        return serie is null
            ? Resultado.Fallo<SerieDto>(ErroresDeSerie.NoEncontrada(id))
            : Resultado.Correcto(serie.ADto());
    }
}

/// <inheritdoc cref="IListarSeries"/>
internal sealed class ListarSeries(IRepositorioDeSeries series) : IListarSeries
{
    public async Task<PaginaDe<SerieDto>> EjecutarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion)
    {
        PaginaDe<Serie> pagina = await series.ListarAsync(paginacion, cancelacion).ConfigureAwait(false);

        return new PaginaDe<SerieDto>(
            [.. pagina.Elementos.Select(serie => serie.ADto())],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}
