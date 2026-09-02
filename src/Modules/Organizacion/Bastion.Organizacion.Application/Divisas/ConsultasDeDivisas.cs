using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Divisas;
using Bastion.Organizacion.Domain.Divisas;

namespace Bastion.Organizacion.Application.Divisas;

/// <summary>Devuelve una divisa por su identificador.</summary>
public interface IObtenerDivisa
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la divisa.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ConVersion<DivisaDto>>> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <summary>Devuelve una página de divisas.</summary>
public interface IListarDivisas
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="paginacion">Qué página se pide y de qué tamaño.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<PaginaDe<DivisaDto>> EjecutarAsync(Paginacion paginacion, CancellationToken cancelacion);
}

/// <summary>Devuelve una cotización por su identificador.</summary>
public interface IObtenerTipoCambio
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la cotización.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ConVersion<TipoCambioDto>>> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <summary>Devuelve una página de cotizaciones.</summary>
public interface IListarTiposDeCambio
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="paginacion">Qué página se pide y de qué tamaño.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<PaginaDe<TipoCambioDto>> EjecutarAsync(Paginacion paginacion, CancellationToken cancelacion);
}

/// <inheritdoc cref="IObtenerDivisa"/>
internal sealed class ObtenerDivisa(
    IRepositorioDeDivisas divisas,
    IVersionesDeOrganizacion versiones) : IObtenerDivisa
{
    public async Task<Resultado<ConVersion<DivisaDto>>> EjecutarAsync(
        Guid id,
        CancellationToken cancelacion)
    {
        Divisa? divisa = await divisas.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        return divisa is null
            ? Resultado.Fallo<ConVersion<DivisaDto>>(ErroresDeDivisa.NoEncontrada(id))
            : Resultado.Correcto(new ConVersion<DivisaDto>(divisa.ADto(), versiones.De(divisa)));
    }
}

/// <inheritdoc cref="IListarDivisas"/>
internal sealed class ListarDivisas(IRepositorioDeDivisas divisas) : IListarDivisas
{
    public async Task<PaginaDe<DivisaDto>> EjecutarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion)
    {
        PaginaDe<Divisa> pagina = await divisas.ListarAsync(paginacion, cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<DivisaDto>(
            [.. pagina.Elementos.Select(divisa => divisa.ADto())],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}

/// <inheritdoc cref="IObtenerTipoCambio"/>
internal sealed class ObtenerTipoCambio(
    IRepositorioDeTiposDeCambio cambios,
    IVersionesDeOrganizacion versiones) : IObtenerTipoCambio
{
    public async Task<Resultado<ConVersion<TipoCambioDto>>> EjecutarAsync(
        Guid id,
        CancellationToken cancelacion)
    {
        TipoCambio? cambio = await cambios.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        return cambio is null
            ? Resultado.Fallo<ConVersion<TipoCambioDto>>(ErroresDeDivisa.CambioNoEncontrado(id))
            : Resultado.Correcto(new ConVersion<TipoCambioDto>(cambio.ADto(), versiones.De(cambio)));
    }
}

/// <inheritdoc cref="IListarTiposDeCambio"/>
internal sealed class ListarTiposDeCambio(IRepositorioDeTiposDeCambio cambios) : IListarTiposDeCambio
{
    public async Task<PaginaDe<TipoCambioDto>> EjecutarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion)
    {
        PaginaDe<TipoCambio> pagina = await cambios.ListarAsync(paginacion, cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<TipoCambioDto>(
            [.. pagina.Elementos.Select(cambio => cambio.ADto())],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}
