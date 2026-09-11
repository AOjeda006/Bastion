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

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// <b>UNA consulta y el árbol entero de la empresa, plano.</b> Dos columnas, sin rastrear, y el
    /// ascenso recorrido en memoria. El número de viajes a la base <b>no crece con la
    /// profundidad</b>, que es la afirmación entera de este camino.
    /// </para>
    /// <para>
    /// <b>Trae más filas de las que hacen falta, y ese es el intercambio, dicho en voz alta.</b> Se
    /// leen todas las categorías de la empresa para usar once como mucho. A cambio: una consulta en
    /// vez de once, el filtro de inquilinato puesto por el contexto —que un <c>WITH RECURSIVE</c>
    /// escrito a mano no tendría (0.6)— y ninguna excepción nueva que declarar en la lista de
    /// saltos al filtro. El árbol de clasificación de una pyme son cientos de filas de dos columnas
    /// con un tope de once niveles escrito en el dominio; no es una tabla de movimientos, y por eso
    /// el intercambio sale a cuenta aquí y no saldría en otro sitio.
    /// </para>
    /// <para>
    /// El recorrido va <b>acotado</b> y además no vuelve a pisar lo ya visto: sobre datos que ya
    /// tuvieran un ciclo —una restauración a medias, un <c>UPDATE</c> a mano— un ascenso sin cota
    /// no da error, gira. Aquí giraría en el proceso y no en el servidor de base de datos, que es
    /// la única ventaja que tiene girar, y ni eso pasa.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<Guid>> AscendenciaAsync(
        Guid categoriaId,
        CancellationToken cancelacion)
    {
        Dictionary<Guid, Guid?> arbol = await contexto.Categorias
            .AsNoTracking()
            .Select(categoria => new { categoria.Id, categoria.PadreId })
            .ToDictionaryAsync(fila => fila.Id, fila => fila.PadreId, cancelacion)
            .ConfigureAwait(false);

        List<Guid> cadena = [];
        Guid? actual = categoriaId;

        // `ProfundidadMaxima + 1` vueltas: la cota es la misma que la del ascenso de
        // `ElArbolSigueSiendoUnArbol`, leída de donde está escrita con su motivo y no repetida.
        for (int nivel = 0; nivel <= Categoria.ProfundidadMaxima; nivel++)
        {
            // Una categoría que no está en el mapa no existe EN ESTA EMPRESA: el filtro de
            // inquilinato lo puso el contexto al traer el árbol, así que una prestada de otra
            // empresa corta el ascenso sin una condición aquí que alguien pueda olvidar.
            if (actual is not { } id || !arbol.TryGetValue(id, out Guid? padreId) || cadena.Contains(id))
            {
                break;
            }

            cadena.Add(id);
            actual = padreId;
        }

        return cadena;
    }

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
