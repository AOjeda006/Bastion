using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Terceros.Application.Comun;
using Bastion.Terceros.Contracts.Terceros;
using Bastion.Terceros.Domain.Terceros;

namespace Bastion.Terceros.Application.Terceros;

/// <summary>Devuelve un tercero por su identificador.</summary>
public interface IObtenerTercero
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del tercero.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ConVersion<TerceroDto>>> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <summary>Devuelve una página de terceros.</summary>
public interface IListarTerceros : IListado<TerceroDto>
{
}

/// <summary>Un tercero con su límite de crédito: lo que la exportación escribe de él.</summary>
/// <remarks>
/// La exportación sale con la cabecera de la importación, que lleva el límite (*La puerta del 3.2*,
/// respuesta 2), y el listado no lo publica. El límite vive en la propia fila de la ficha, así que
/// sale de la misma página, sin otra consulta, y se lee con el mismo permiso que su <c>GET</c>.
/// </remarks>
/// <param name="Tercero">El tercero, como lo publica el listado.</param>
/// <param name="Limite">Su límite, como lo publica su <c>GET …/limite-credito</c>.</param>
public sealed record TerceroConLimite(TerceroDto Tercero, LimiteCreditoDto Limite);

/// <summary>
/// Devuelve una página del listado de terceros con el límite de cada uno, para la exportación.
/// </summary>
/// <remarks>
/// Lo implementa <see cref="ListarTerceros"/>, el mismo que atiende el listado, sobre la misma
/// lectura del repositorio: los filtros, el orden y lo bloqueado son los del listado por
/// construcción, como en las demás exportaciones (ADR-0073).
/// </remarks>
public interface IListarTercerosConLimite : IListado<TerceroConLimite>
{
}

/// <inheritdoc cref="IObtenerTercero"/>
internal sealed class ObtenerTercero(
    IRepositorioDeTerceros terceros,
    IVersionesDeTerceros versiones) : IObtenerTercero
{
    public async Task<Resultado<ConVersion<TerceroDto>>> EjecutarAsync(
        Guid id,
        CancellationToken cancelacion)
    {
        Tercero? tercero = await terceros.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        return tercero is null
            ? Resultado.Fallo<ConVersion<TerceroDto>>(ErroresDeTercero.NoEncontrado(id))
            : Resultado.Correcto(new ConVersion<TerceroDto>(tercero.ADto(), versiones.De(tercero)));
    }
}

/// <inheritdoc cref="IListarTerceros"/>
internal sealed class ListarTerceros(IRepositorioDeTerceros terceros) : IListarTerceros, IListarTercerosConLimite
{
    public IReadOnlySet<string> CamposOrdenables => terceros.CamposOrdenables;

    public Task<PaginaDe<TerceroDto>> EjecutarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion) =>
        ListarAsync(paginacion, tercero => tercero.ADto(), cancelacion);

    Task<PaginaDe<TerceroConLimite>> IListado<TerceroConLimite>.EjecutarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion) =>
        ListarAsync(paginacion, tercero => new TerceroConLimite(tercero.ADto(), tercero.ALimiteDto()), cancelacion);

    // Una sola lectura para las dos: lo que cambia es lo que se publica de cada tercero.
    private async Task<PaginaDe<TDto>> ListarAsync<TDto>(
        Paginacion paginacion,
        Func<Tercero, TDto> publicar,
        CancellationToken cancelacion)
    {
        PaginaDe<Tercero> pagina = await terceros.ListarAsync(paginacion, cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<TDto>(
            [.. pagina.Elementos.Select(publicar)],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}
