using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;

namespace Bastion.Organizacion.Application.Unidades;

/// <summary>
/// Retira una unidad de medida: deja de ofrecerse para operaciones nuevas y sigue resolviendo lo
/// viejo.
/// </summary>
/// <remarks>
/// Es la mitad que le faltaba al puerto <c>IConsultaDeUnidadesDeMedida</c>: hasta este ítem no
/// había manera de que contestara <c>SoloResuelveLoViejo</c>, y su propio comentario decía que ese
/// estado llegaba con la retirada. Desde aquí, Catálogo deja de poder dar de alta artículos con
/// una unidad retirada sin dejar de poder resolver la de un albarán de hace tres años.
/// </remarks>
public interface IRetirarUnidadMedida
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la unidad.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, VersionDeRecurso version, CancellationToken cancelacion);
}

/// <summary>Vuelve a ofrecer una unidad de medida retirada.</summary>
public interface IReincorporarUnidadMedida
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la unidad.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, VersionDeRecurso version, CancellationToken cancelacion);
}

/// <summary>Retira una conversión entre unidades.</summary>
/// <remarks>
/// Una conversión retirada <b>sigue restringiendo a su inversa</b> (decisión 2 del ítem 1.7):
/// «sigue resolviendo» significa que sigue siendo verdad, y si retirarla la sacara de la
/// comprobación de plausibilidad, retirar el sentido incómodo sería la manera de declarar
/// cualquier número en el otro.
/// </remarks>
public interface IRetirarConversionUm
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la conversión.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, VersionDeRecurso version, CancellationToken cancelacion);
}

/// <summary>Vuelve a ofrecer una conversión retirada.</summary>
public interface IReincorporarConversionUm
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la conversión.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, VersionDeRecurso version, CancellationToken cancelacion);
}

/// <inheritdoc cref="IRetirarUnidadMedida"/>
internal sealed class RetirarUnidadMedida(
    IRepositorioDeUnidadesDeMedida unidades,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones) : IRetirarUnidadMedida
{
    public async Task<Resultado> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        CancellationToken cancelacion) =>
        await CambioDeRetirada.AplicarAsync(
            await unidades.ObtenerAsync(id, cancelacion).ConfigureAwait(false),
            version,
            retirar: true,
            ErroresDeUnidad.NoEncontrada(id),
            versiones,
            unidadTrabajo,
            cancelacion).ConfigureAwait(false);
}

/// <inheritdoc cref="IReincorporarUnidadMedida"/>
internal sealed class ReincorporarUnidadMedida(
    IRepositorioDeUnidadesDeMedida unidades,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones) : IReincorporarUnidadMedida
{
    public async Task<Resultado> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        CancellationToken cancelacion) =>
        await CambioDeRetirada.AplicarAsync(
            await unidades.ObtenerAsync(id, cancelacion).ConfigureAwait(false),
            version,
            retirar: false,
            ErroresDeUnidad.NoEncontrada(id),
            versiones,
            unidadTrabajo,
            cancelacion).ConfigureAwait(false);
}

/// <inheritdoc cref="IRetirarConversionUm"/>
internal sealed class RetirarConversionUm(
    IRepositorioDeConversiones conversiones,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones) : IRetirarConversionUm
{
    public async Task<Resultado> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        CancellationToken cancelacion) =>
        await CambioDeRetirada.AplicarAsync(
            await conversiones.ObtenerAsync(id, cancelacion).ConfigureAwait(false),
            version,
            retirar: true,
            ErroresDeUnidad.ConversionNoEncontrada(id),
            versiones,
            unidadTrabajo,
            cancelacion).ConfigureAwait(false);
}

/// <inheritdoc cref="IReincorporarConversionUm"/>
internal sealed class ReincorporarConversionUm(
    IRepositorioDeConversiones conversiones,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones) : IReincorporarConversionUm
{
    public async Task<Resultado> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        CancellationToken cancelacion) =>
        await CambioDeRetirada.AplicarAsync(
            await conversiones.ObtenerAsync(id, cancelacion).ConfigureAwait(false),
            version,
            retirar: false,
            ErroresDeUnidad.ConversionNoEncontrada(id),
            versiones,
            unidadTrabajo,
            cancelacion).ConfigureAwait(false);
}
