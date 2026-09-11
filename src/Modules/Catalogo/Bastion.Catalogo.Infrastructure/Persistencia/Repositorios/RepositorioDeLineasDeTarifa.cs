using System.Linq.Expressions;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Catalogo.Application.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Catalogo.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeLineasDeTarifa"/>
internal sealed class RepositorioDeLineasDeTarifa(CatalogoDbContext contexto)
    : IRepositorioDeLineasDeTarifa
{
    private static readonly CriteriosDe<LineaTarifa> s_criterios = new()
    {
        Ordenables = new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["cantidadDesde"] =
                (Expression<Func<LineaTarifa, decimal>>)(linea => linea.CantidadDesde),
        },
        PorOmision = "cantidadDesde",

        // El desempate empieza por el destino: una tarifa tiene una línea por referencia y muchas
        // comparten `CantidadDesde`. Sin esto, la misma página pedida dos veces devolvería filas
        // distintas —que es la clase de fallo que solo se ve como «faltan líneas» al paginar—.
        Desempate = ordenada => ordenada
            .ThenBy(linea => linea.ArticuloId)
            .ThenBy(linea => linea.CategoriaId)
            .ThenBy(linea => linea.Id),

        // Sin `Filtro`: una línea de tarifa no tiene ni un campo que una persona escriba, y un
        // `?q=` sobre identificadores sería pedir que se teclee un `uuid`. El borde contesta que
        // este recurso no admite filtro de texto porque aquí no hay ninguno declarado.
    };

    public IReadOnlySet<string> CamposOrdenables => s_criterios.CamposOrdenables;

    public Task<LineaTarifa?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        contexto.LineasDeTarifa.FirstOrDefaultAsync(linea => linea.Id == id, cancelacion);

    /// <inheritdoc/>
    /// <remarks>
    /// <b>UNA consulta, sea cual sea la profundidad.</b> La ascendencia llega ya resuelta y entra
    /// entera en un <c>= ANY(…)</c>: no hay bucle, no hay una consulta por nivel y no hay nada que
    /// crezca con lo hondo que cuelgue el artículo. El <b>nivel</b> se le pone después a cada fila
    /// según la posición de su categoría en esa lista, que es lo que convierte «más cercana» en un
    /// número — y lo que hace imposible que una categoría que no sea antepasada compita, porque no
    /// está en la lista y por tanto no ha llegado.
    /// </remarks>
    public async Task<IReadOnlyList<LineaCandidata>> CandidatasAsync(
        Guid tarifaId,
        Guid articuloId,
        IReadOnlyList<Guid> ascendencia,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(ascendencia);

        List<Guid> antepasadas = [.. ascendencia];

        var filas = await contexto.LineasDeTarifa
            .AsNoTracking()
            .Where(linea => linea.TarifaId == tarifaId
                && (linea.ArticuloId == articuloId
                    || (linea.CategoriaId != null && antepasadas.Contains(linea.CategoriaId.Value))))
            .Select(linea => new
            {
                linea.Id,
                linea.ArticuloId,
                linea.CategoriaId,
                linea.CantidadDesde,
                linea.PrecioODescuento.Precio,
                linea.PrecioODescuento.DescuentoPorcentaje,
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        Dictionary<Guid, int> nivelDe = [];

        for (int nivel = 0; nivel < ascendencia.Count; nivel++)
        {
            // `TryAdd` y no el indexador: si un árbol estropeado repitiera una categoría en la
            // cadena, el nivel que vale es el MÁS CERCANO, que es el primero que se ve.
            nivelDe.TryAdd(ascendencia[nivel], nivel);
        }

        return
        [
            .. filas.Select(fila => new LineaCandidata(
                fila.Id,
                fila.ArticuloId,
                fila.CategoriaId,
                fila.CategoriaId is { } categoriaId && nivelDe.TryGetValue(categoriaId, out int nivel)
                    ? nivel
                    : null,
                fila.CantidadDesde,
                fila.Precio,
                fila.DescuentoPorcentaje)),
        ];
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>Las dos preguntas salen de la misma consulta</b>, y lo que se trae son las cantidades de
    /// arranque de ese destino: un puñado de números —los tramos de una tabla de precios—, no las
    /// líneas. Separarlas en dos consultas invitaría a comprobar solo una, y son las dos mitades de
    /// «ni hueco ni solape».
    /// </remarks>
    public async Task<TramosDelDestino> TramosDelDestinoAsync(
        Guid tarifaId,
        Guid? articuloId,
        Guid? categoriaId,
        decimal cantidadDesde,
        CancellationToken cancelacion)
    {
        IQueryable<LineaTarifa> delDestino = contexto.LineasDeTarifa
            .Where(linea => linea.TarifaId == tarifaId);

        // La rama se elige en C# y no comparando la columna con un parámetro que puede ser nulo:
        // así no hay que confiar en cómo traduzca EF Core una igualdad contra un nulo, que es una
        // de esas cosas que funcionan hasta que cambian.
        delDestino = articuloId is { } articulo
            ? delDestino.Where(linea => linea.ArticuloId == articulo)
            : delDestino.Where(linea => linea.CategoriaId == categoriaId!.Value);

        List<decimal> arranques = await delDestino
            .AsNoTracking()
            .Select(linea => linea.CantidadDesde)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return new TramosDelDestino(
            arranques.Count > 0,
            arranques.Contains(cantidadDesde));
    }

    public Task<PaginaDe<LineaTarifa>> ListarAsync(
        Paginacion paginacion,
        Guid tarifaId,
        CancellationToken cancelacion) =>
        contexto.LineasDeTarifa
            .Where(linea => linea.TarifaId == tarifaId)
            .PaginarAsync(paginacion, s_criterios, cancelacion);

    public void Agregar(LineaTarifa linea) => contexto.LineasDeTarifa.Add(linea);
}
