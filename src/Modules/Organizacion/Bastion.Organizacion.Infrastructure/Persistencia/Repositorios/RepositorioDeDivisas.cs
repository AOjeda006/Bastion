using Bastion.Organizacion.Application.Divisas;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Domain.Divisas;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeDivisas"/>
internal sealed class RepositorioDeDivisas(OrganizacionDbContext contexto) : IRepositorioDeDivisas
{
    public Task<Divisa?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Divisas.FirstOrDefaultAsync(divisa => divisa.Id == id, cancelacion);

    public Task<bool> ExisteElCodigoAsync(string codigo, CancellationToken cancelacion) =>
        contexto.Divisas.AnyAsync(divisa => divisa.Codigo == codigo, cancelacion);

    public async Task<bool> ExistenAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(ids);

        // Se cuentan los DISTINTOS: con `ids` repetidos, un `Count == ids.Count` diría que falta
        // una divisa que sí está. Aquí no puede pasar —origen y destino se comprueban distintos
        // antes—, pero la consulta no depende de eso.
        int encontradas = await contexto.Divisas
            .CountAsync(divisa => ids.Contains(divisa.Id), cancelacion)
            .ConfigureAwait(false);

        return encontradas == ids.Distinct().Count();
    }

    public Task<PaginaDe<Divisa>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion) =>
        contexto.Divisas
            .OrderBy(divisa => divisa.Codigo)
            .ThenBy(divisa => divisa.Id)
            .PaginarAsync(paginacion, cancelacion);

    public void Agregar(Divisa divisa) => contexto.Divisas.Add(divisa);
}

/// <inheritdoc cref="IRepositorioDeTiposDeCambio"/>
internal sealed class RepositorioDeTiposDeCambio(OrganizacionDbContext contexto)
    : IRepositorioDeTiposDeCambio
{
    public Task<TipoCambio?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        contexto.TiposDeCambio.FirstOrDefaultAsync(cambio => cambio.Id == id, cancelacion);

    public Task<bool> ExisteAsync(
        Guid divisaOrigenId,
        Guid divisaDestinoId,
        DateOnly fecha,
        CancellationToken cancelacion) =>
        contexto.TiposDeCambio.AnyAsync(
            cambio => cambio.DivisaOrigenId == divisaOrigenId
                && cambio.DivisaDestinoId == divisaDestinoId
                && cambio.Fecha == fecha,
            cancelacion);

    public Task<PaginaDe<TipoCambio>> ListarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion) =>
        // La más reciente primero: quien mira cotizaciones busca la de hoy, no la del año pasado.
        contexto.TiposDeCambio
            .OrderByDescending(cambio => cambio.Fecha)
            .ThenBy(cambio => cambio.Id)
            .PaginarAsync(paginacion, cancelacion);

    public void Agregar(TipoCambio tipoCambio) => contexto.TiposDeCambio.Add(tipoCambio);
}
