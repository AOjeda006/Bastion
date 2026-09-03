using System.Linq.Expressions;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Organizacion.Application.Unidades;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Domain.Unidades;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeUnidadesDeMedida"/>
internal sealed class RepositorioDeUnidadesDeMedida(OrganizacionDbContext contexto)
    : IRepositorioDeUnidadesDeMedida
{
    public Task<UnidadMedida?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        contexto.UnidadesDeMedida.FirstOrDefaultAsync(unidad => unidad.Id == id, cancelacion);

    public Task<bool> ExisteElCodigoAsync(string codigo, CancellationToken cancelacion) =>
        contexto.UnidadesDeMedida.AnyAsync(unidad => unidad.Codigo == codigo, cancelacion);

    public async Task<bool> ExistenAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(ids);

        int encontradas = await contexto.UnidadesDeMedida
            .CountAsync(unidad => ids.Contains(unidad.Id), cancelacion)
            .ConfigureAwait(false);

        return encontradas == ids.Distinct().Count();
    }

    private static readonly CriteriosDe<UnidadMedida> s_criterios = new()
    {
        Ordenables = new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["codigo"] = (Expression<Func<UnidadMedida, string>>)(unidad => unidad.Codigo),
            ["nombre"] = (Expression<Func<UnidadMedida, string>>)(unidad => unidad.Nombre),
        },
        PorOmision = "codigo",
        Desempate = ordenada => ordenada.ThenBy(unidad => unidad.Id),
        Filtro = texto =>
        {
            string patron = Filtros.Contiene(texto);

            return unidad => EF.Functions.ILike(unidad.Codigo, patron, Filtros.Escape)
                || EF.Functions.ILike(unidad.Nombre, patron, Filtros.Escape);
        },
    };

    public IReadOnlySet<string> CamposOrdenables => s_criterios.CamposOrdenables;

    public Task<PaginaDe<UnidadMedida>> ListarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion) =>
        contexto.UnidadesDeMedida.PaginarAsync(paginacion, s_criterios, cancelacion);

    public void Agregar(UnidadMedida unidad) => contexto.UnidadesDeMedida.Add(unidad);
}

/// <inheritdoc cref="IRepositorioDeConversiones"/>
internal sealed class RepositorioDeConversiones(OrganizacionDbContext contexto)
    : IRepositorioDeConversiones
{
    public Task<ConversionUM?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        contexto.ConversionesDeUnidades.FirstOrDefaultAsync(
            conversion => conversion.Id == id, cancelacion);

    public Task<bool> ExisteAsync(
        Guid unidadOrigenId,
        Guid unidadDestinoId,
        CancellationToken cancelacion) =>
        contexto.ConversionesDeUnidades.AnyAsync(
            conversion => conversion.UnidadOrigenId == unidadOrigenId
                && conversion.UnidadDestinoId == unidadDestinoId,
            cancelacion);

    // Sin filtro de texto: una conversión son dos identificadores y un factor, y no hay ningún
    // texto que buscar.
    private static readonly CriteriosDe<ConversionUM> s_criterios = new()
    {
        Ordenables = new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["origen"] = (Expression<Func<ConversionUM, Guid>>)(conversion => conversion.UnidadOrigenId),
            ["factor"] = (Expression<Func<ConversionUM, decimal>>)(conversion => conversion.Factor),
        },
        PorOmision = "origen",
        Desempate = ordenada => ordenada
            .ThenBy(conversion => conversion.UnidadDestinoId)
            .ThenBy(conversion => conversion.Id),
    };

    public IReadOnlySet<string> CamposOrdenables => s_criterios.CamposOrdenables;

    public Task<PaginaDe<ConversionUM>> ListarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion) =>
        contexto.ConversionesDeUnidades.PaginarAsync(paginacion, s_criterios, cancelacion);

    public void Agregar(ConversionUM conversion) => contexto.ConversionesDeUnidades.Add(conversion);
}
