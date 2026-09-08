using System.Linq.Expressions;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Catalogo.Application.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Catalogo.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeArticulos"/>
internal sealed class RepositorioDeArticulos(CatalogoDbContext contexto) : IRepositorioDeArticulos
{
    // Ni el identificador de la unidad ni el del impuesto están entre los ordenables ni en el
    // filtro del `?q=`, y no es una omisión: ordenar por un `uuid` no ordena por nada que una
    // persona reconozca, y buscar por él es pedir un identificador que quien mira la pantalla no
    // tiene. Lo que se ordena y se busca es lo que se lee: el código y la descripción.
    private static readonly CriteriosDe<Articulo> s_criterios = new()
    {
        Ordenables = new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["codigo"] = (Expression<Func<Articulo, string>>)(articulo => articulo.Codigo),
            ["descripcion"] = (Expression<Func<Articulo, string>>)(articulo => articulo.Descripcion),
        },
        PorOmision = "codigo",
        Desempate = ordenada => ordenada.ThenBy(articulo => articulo.Id),
        Filtro = texto =>
        {
            string patron = Filtros.Contiene(texto);

            return articulo => EF.Functions.ILike(articulo.Codigo, patron, Filtros.Escape)
                || EF.Functions.ILike(articulo.Descripcion, patron, Filtros.Escape);
        },
    };

    public IReadOnlySet<string> CamposOrdenables => s_criterios.CamposOrdenables;

    public Task<Articulo?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Articulos.FirstOrDefaultAsync(articulo => articulo.Id == id, cancelacion);

    public Task<bool> ExisteElCodigoAsync(
        Guid empresaId,
        string codigo,
        CancellationToken cancelacion) =>
        contexto.Articulos.AnyAsync(
            articulo => articulo.EmpresaId == empresaId && articulo.Codigo == codigo,
            cancelacion);

    /// <inheritdoc/>
    /// <remarks>
    /// <b>El acotado por categoría se aplica ANTES de paginar</b>, que es lo mismo que el paginador
    /// hace con las retiradas y por el mismo motivo: el total que viaja en la página tiene que ser
    /// el de lo que se devuelve. Contar todo y entregar menos deja al cliente paginando hacia
    /// páginas vacías.
    /// </remarks>
    public Task<PaginaDe<Articulo>> ListarAsync(
        Paginacion paginacion,
        Guid? categoriaId,
        CancellationToken cancelacion)
    {
        IQueryable<Articulo> consulta = contexto.Articulos;

        if (categoriaId is { } id)
        {
            consulta = consulta.Where(articulo => articulo.CategoriaId == id);
        }

        return consulta.PaginarAsync(paginacion, s_criterios, cancelacion);
    }

    public void Agregar(Articulo articulo) => contexto.Articulos.Add(articulo);
}
