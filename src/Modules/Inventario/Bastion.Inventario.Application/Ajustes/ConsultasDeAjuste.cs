using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;

namespace Bastion.Inventario.Application.Ajustes;

/// <summary>Los valores que admite el criterio propio del listado de ajustes.</summary>
/// <remarks>
/// <b>Una constante, para que el borde valide con el mismo texto</b>: el patrón se compone con el
/// nombre de cada estado, como el del recuento, así que renombrar uno no deja el borde validando el
/// viejo.
/// </remarks>
public static class LosFiltrosDelAjuste
{
    /// <summary>Lo que admite el criterio del estado: el nombre de cada estado, tal como se publica.</summary>
    public const string PatronDelEstado =
        "^(" + nameof(EstadoDeAjuste.Borrador)
        + "|" + nameof(EstadoDeAjuste.Confirmado)
        + "|" + nameof(EstadoDeAjuste.Anulado) + ")$";
}

/// <summary>Devuelve la ficha de un ajuste, con sus líneas y su versión.</summary>
public interface IObtenerAjuste
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del ajuste.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>La ficha y su versión, o <c>404</c>.</returns>
    Task<Resultado<ConVersion<FichaDeAjusteDto>>> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <summary>Devuelve una página de ajustes, opcionalmente acotada a un estado y a un almacén.</summary>
public interface IListarAjustes : IOrdenaPor
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="paginacion">Qué página, de qué tamaño, con qué orden y qué filtro.</param>
    /// <param name="estado">El estado al que se acota, ya validado por el borde, o nulo.</param>
    /// <param name="almacenId">El almacén al que se acota, o nulo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>La página.</returns>
    Task<PaginaDe<AjusteResumenDto>> EjecutarAsync(
        Paginacion paginacion,
        string? estado,
        Guid? almacenId,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IObtenerAjuste"/>
/// <remarks>
/// <b>La versión es la que pide el descarte</b> en su <c>If-Match</c> (ADR-0065 §5). Un borrador
/// descartado no se encuentra: lo esconde el filtro del contexto, y la ficha contesta <c>404</c>.
/// </remarks>
/// <param name="ajustes">El documento, con sus líneas.</param>
/// <param name="versiones">La versión del documento, que es su <c>ETag</c>.</param>
internal sealed class ObtenerAjuste(
    IRepositorioDeAjustes ajustes,
    IVersionesDeInventario versiones) : IObtenerAjuste
{
    public async Task<Resultado<ConVersion<FichaDeAjusteDto>>> EjecutarAsync(
        Guid id,
        CancellationToken cancelacion)
    {
        Ajuste? ajuste = await ajustes.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        return ajuste is null
            ? Resultado.Fallo<ConVersion<FichaDeAjusteDto>>(ErroresDeAjuste.NoEncontrado(id))
            : Resultado.Correcto(new ConVersion<FichaDeAjusteDto>(ajuste.AFicha(), versiones.De(ajuste)));
    }
}

/// <inheritdoc cref="IListarAjustes"/>
/// <param name="ajustes">Los documentos.</param>
internal sealed class ListarAjustes(IRepositorioDeAjustes ajustes) : IListarAjustes
{
    public IReadOnlySet<string> CamposOrdenables => ajustes.CamposOrdenables;

    public async Task<PaginaDe<AjusteResumenDto>> EjecutarAsync(
        Paginacion paginacion,
        string? estado,
        Guid? almacenId,
        CancellationToken cancelacion)
    {
        EstadoDeAjuste? acotado = estado is null ? null : Enum.Parse<EstadoDeAjuste>(estado);

        PaginaDe<Ajuste> pagina = await ajustes
            .ListarAsync(paginacion, acotado, almacenId, cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<AjusteResumenDto>(
            [.. pagina.Elementos.Select(ajuste => ajuste.AResumen())],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}
