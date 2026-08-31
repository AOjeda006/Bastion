using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Domain.Series;

namespace Bastion.Organizacion.Application.Series;

/// <summary>Cambia el formato de una serie activa.</summary>
public interface IModificarSerie
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la serie.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="peticion">El formato nuevo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<SerieDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarSerieDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IModificarSerie"/>
internal sealed class ModificarSerie(
    IRepositorioDeSeries series,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones) : IModificarSerie
{
    public async Task<Resultado<SerieDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarSerieDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Serie? serie = await series.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (serie is null)
        {
            return Resultado.Fallo<SerieDto>(ErroresDeSerie.NoEncontrada(id));
        }

        versiones.Exigir(serie, version);

        if (serie.Estado == EstadoDeSerie.Cerrada)
        {
            return Resultado.Fallo<SerieDto>(ErroresDeSerie.Cerrada(id));
        }

        serie.Modificar(peticion.Formato);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(serie.ADto());
    }
}
