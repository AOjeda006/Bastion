using System.Linq.Expressions;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Catalogo.Application.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Catalogo.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeCategorias"/>
internal sealed class RepositorioDeCategorias(CatalogoDbContext contexto) : IRepositorioDeCategorias
{
    private static readonly CriteriosDe<Categoria> s_criterios = new()
    {
        Ordenables = new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["codigo"] = (Expression<Func<Categoria, string>>)(categoria => categoria.Codigo),
            ["nombre"] = (Expression<Func<Categoria, string>>)(categoria => categoria.Nombre),
        },
        PorOmision = "codigo",
        Desempate = ordenada => ordenada.ThenBy(categoria => categoria.Id),
        Filtro = texto =>
        {
            string patron = Filtros.Contiene(texto);

            return categoria => EF.Functions.ILike(categoria.Codigo, patron, Filtros.Escape)
                || EF.Functions.ILike(categoria.Nombre, patron, Filtros.Escape);
        },
    };

    public IReadOnlySet<string> CamposOrdenables => s_criterios.CamposOrdenables;

    public Task<Categoria?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Categorias.FirstOrDefaultAsync(categoria => categoria.Id == id, cancelacion);

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// <b>Sin rastrear y con solo tres columnas.</b> El ascenso de <c>ElArbolSigueSiendoUnArbol</c>
    /// hace una consulta por nivel, y traer el agregado entero en cada una metería hasta once
    /// entidades en el rastreador que nadie va a modificar — y que en la modificación competirían
    /// con la que sí se está cambiando: EF Core devuelve la instancia ya rastreada, así que un
    /// ascenso que pasara por la propia categoría en curso le devolvería la que tiene los cambios
    /// a medio aplicar. La proyección hace imposible ese enredo, no solo lo evita.
    /// </para>
    /// <para>
    /// El filtro de inquilinato va puesto por el contexto (R8), así que esto solo encuentra
    /// categorías de la empresa de la sesión: un padre prestado de otra empresa sale como «no
    /// existe» sin que haga falta una condición aquí que alguien pueda olvidar en la siguiente
    /// consulta.
    /// </para>
    /// </remarks>
    public Task<EslabonDeCategoria?> EslabonAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Categorias
            .Where(categoria => categoria.Id == id)
            .Select(categoria => new EslabonDeCategoria(
                categoria.Id, categoria.PadreId, categoria.Codigo))
            .AsNoTracking()
            .FirstOrDefaultAsync(cancelacion);

    public Task<bool> ExisteElCodigoAsync(
        Guid empresaId,
        string codigo,
        CancellationToken cancelacion) =>
        contexto.Categorias.AnyAsync(
            categoria => categoria.EmpresaId == empresaId && categoria.Codigo == codigo,
            cancelacion);

    public Task<PaginaDe<Categoria>> ListarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion) =>
        contexto.Categorias.PaginarAsync(paginacion, s_criterios, cancelacion);

    public void Agregar(Categoria categoria) => contexto.Categorias.Add(categoria);
}
