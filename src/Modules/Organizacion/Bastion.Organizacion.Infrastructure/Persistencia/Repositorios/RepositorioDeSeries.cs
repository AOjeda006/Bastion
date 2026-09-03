using System.Linq.Expressions;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Organizacion.Application.Series;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Domain.Series;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeSeries"/>
internal sealed class RepositorioDeSeries(OrganizacionDbContext contexto) : IRepositorioDeSeries
{
    public Task<Serie?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Series.FirstOrDefaultAsync(serie => serie.Id == id, cancelacion);

    public Task<bool> ExisteElCodigoAsync(
        Guid empresaId,
        Guid ejercicioId,
        string codigo,
        CancellationToken cancelacion) =>
        contexto.Series.AnyAsync(
            serie => serie.EmpresaId == empresaId
                && serie.EjercicioId == ejercicioId
                && serie.Codigo == codigo,
            cancelacion);

    private static readonly CriteriosDe<Serie> s_criterios = new()
    {
        Ordenables = new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["codigo"] = (Expression<Func<Serie, string>>)(serie => serie.Codigo),
            ["contador"] = (Expression<Func<Serie, long>>)(serie => serie.Contador),
        },
        PorOmision = "codigo",
        Desempate = ordenada => ordenada.ThenBy(serie => serie.Id),
        Filtro = texto =>
        {
            string patron = Filtros.Contiene(texto);

            return serie => EF.Functions.ILike(serie.Codigo, patron, Filtros.Escape);
        },
    };

    public IReadOnlySet<string> CamposOrdenables => s_criterios.CamposOrdenables;

    public Task<PaginaDe<Serie>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion) =>
        contexto.Series.PaginarAsync(paginacion, s_criterios, cancelacion);

    public void Agregar(Serie serie) => contexto.Series.Add(serie);

    public void Eliminar(Serie serie) => contexto.Series.Remove(serie);
}
