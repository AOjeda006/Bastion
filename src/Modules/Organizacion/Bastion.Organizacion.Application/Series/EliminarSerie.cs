using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Domain.Series;

namespace Bastion.Organizacion.Application.Series;

/// <summary>
/// Suprime una serie que todavía no ha numerado nada.
/// </summary>
/// <remarks>
/// Es el «<c>204</c> al borrar un borrador» del §9: mientras el contador está a cero, la serie no
/// ha dejado rastro en ningún libro y quitarla no rompe nada. En cuanto numera una sola vez deja
/// de ser un borrador y pasa a ser parte del histórico.
/// </remarks>
public interface IEliminarSerie
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la serie.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, VersionDeRecurso version, CancellationToken cancelacion);
}

/// <inheritdoc cref="IEliminarSerie"/>
internal sealed class EliminarSerie(
    IRepositorioDeSeries series,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones) : IEliminarSerie
{
    public async Task<Resultado> EjecutarAsync(Guid id, VersionDeRecurso version, CancellationToken cancelacion)
    {
        Serie? serie = await series.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (serie is null)
        {
            return Resultado.Fallo(ErroresDeSerie.NoEncontrada(id));
        }

        versiones.Exigir(serie, version);

        // Quien decide si se puede suprimir es la propia serie: la regla —«mientras no haya
        // numerado»— es suya, y aquí solo se pregunta. Repetirla como `serie.Contador == 0`
        // pondría la misma regla en dos sitios que se separarían a la primera excepción.
        if (!serie.SePuedeSuprimir)
        {
            return Resultado.Fallo(ErroresDeSerie.YaHaNumerado(serie.Contador));
        }

        series.Eliminar(serie);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}
