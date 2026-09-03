using System.Linq.Expressions;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Listados;
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

    private static readonly CriteriosDe<Divisa> s_criterios = new()
    {
        Ordenables = new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["codigo"] = (Expression<Func<Divisa, string>>)(divisa => divisa.Codigo),
            ["nombre"] = (Expression<Func<Divisa, string>>)(divisa => divisa.Nombre),
        },
        PorOmision = "codigo",
        Desempate = ordenada => ordenada.ThenBy(divisa => divisa.Id),
        Filtro = texto =>
        {
            string patron = Filtros.Contiene(texto);

            return divisa => EF.Functions.ILike(divisa.Codigo, patron, Filtros.Escape)
                || EF.Functions.ILike(divisa.Nombre, patron, Filtros.Escape);
        },
    };

    public IReadOnlySet<string> CamposOrdenables => s_criterios.CamposOrdenables;

    public Task<PaginaDe<Divisa>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion) =>
        contexto.Divisas.PaginarAsync(paginacion, s_criterios, cancelacion);

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

    // Sin filtro de texto: una cotización no tiene ningún campo de texto que buscar. Declararlo
    // vacío diría que `?q=` funciona y no filtra nada, que es peor que decir que no hay.
    private static readonly CriteriosDe<TipoCambio> s_criterios = new()
    {
        Ordenables = new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["fecha"] = (Expression<Func<TipoCambio, DateOnly>>)(cambio => cambio.Fecha),
            ["tasa"] = (Expression<Func<TipoCambio, decimal>>)(cambio => cambio.Tasa),
        },
        PorOmision = "fecha",
        // La más reciente primero: quien mira cotizaciones busca la de hoy, no la del año pasado.
        DescendentePorOmision = true,
        Desempate = ordenada => ordenada.ThenBy(cambio => cambio.Id),
    };

    public IReadOnlySet<string> CamposOrdenables => s_criterios.CamposOrdenables;

    public Task<PaginaDe<TipoCambio>> ListarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion) =>
        contexto.TiposDeCambio.PaginarAsync(paginacion, s_criterios, cancelacion);

    public void Agregar(TipoCambio tipoCambio) => contexto.TiposDeCambio.Add(tipoCambio);
}
