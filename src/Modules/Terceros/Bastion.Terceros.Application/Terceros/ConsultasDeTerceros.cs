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
internal sealed class ListarTerceros(IRepositorioDeTerceros terceros) : IListarTerceros
{
    public IReadOnlySet<string> CamposOrdenables => terceros.CamposOrdenables;

    public async Task<PaginaDe<TerceroDto>> EjecutarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion)
    {
        PaginaDe<Tercero> pagina = await terceros.ListarAsync(paginacion, cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<TerceroDto>(
            [.. pagina.Elementos.Select(tercero => tercero.ADto())],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}
