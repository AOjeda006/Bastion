using System.Linq.Expressions;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Inventario.Application.Recuentos;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Recuentos;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Inventario.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeRecuentos"/>
/// <param name="contexto">El contexto del módulo, con el filtro de la empresa puesto (R8).</param>
internal sealed class RepositorioDeRecuentos(InventarioDbContext contexto) : IRepositorioDeRecuentos
{
    private static readonly CriteriosDe<Recuento> s_criterios = new()
    {
        Ordenables = new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["fechaDeApertura"] = (Expression<Func<Recuento, DateOnly>>)(recuento => recuento.FechaDeApertura),
            ["numero"] = (Expression<Func<Recuento, long?>>)(recuento => recuento.Numero),
            ["fechaDeConfirmacion"] =
                (Expression<Func<Recuento, DateOnly?>>)(recuento => recuento.FechaDeConfirmacion),
        },
        PorOmision = "fechaDeApertura",
        // Del más reciente al más antiguo: quien abre la lista busca el que está contando, que es
        // el último que se abrió.
        DescendentePorOmision = true,
        Desempate = ordenada => ordenada.ThenBy(recuento => recuento.Id),
        Filtro = texto =>
        {
            string patron = Filtros.Contiene(texto);

            return recuento => EF.Functions.ILike(recuento.Motivo, patron, Filtros.Escape);
        },
    };

    public IReadOnlySet<string> CamposOrdenables => s_criterios.CamposOrdenables;

    /// <inheritdoc/>
    /// <remarks>Las líneas vienen con el documento: la navegación es <c>AutoInclude</c>.</remarks>
    public Task<Recuento?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Recuentos.FirstOrDefaultAsync(recuento => recuento.Id == id, cancelacion);

    /// <inheritdoc/>
    public void Agregar(Recuento recuento) => contexto.Recuentos.Add(recuento);

    /// <inheritdoc/>
    public Task<bool> HayUnoEnCursoAsync(Guid almacenId, CancellationToken cancelacion) =>
        contexto.Recuentos
            .IgnoreAutoIncludes()
            .AnyAsync(
                recuento => recuento.AlmacenId == almacenId && recuento.Estado == EstadoDeRecuento.EnCurso,
                cancelacion);

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// <b>Una sola consulta, con el lote y la serie por su código</b>: la línea guarda el código, que
    /// es lo que se lee en la etiqueta, y la existencia guarda el identificador. Cada código sale de
    /// una subconsulta escalar, que es nula cuando la existencia no lleva lote o serie.
    /// </para>
    /// <para>
    /// <b>Las filas a cero no vienen</b>: una clave que se vació no se precarga ni tiene teórico que
    /// decir, y una línea cuya clave no aparece tiene teórico cero (ADR-0055 §2).
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<ExistenciaDeUnaClave>> ExistenciasAsync(
        Guid almacenId,
        IReadOnlyCollection<Guid>? articulos,
        CancellationToken cancelacion)
    {
        IQueryable<Domain.Existencias.Existencia> delAlmacen = contexto.Existencias
            .AsNoTracking()
            .Where(existencia => existencia.AlmacenId == almacenId
                && (existencia.Fisico > 0m || existencia.EnTransito > 0m));

        if (articulos is not null)
        {
            delAlmacen = delAlmacen.Where(existencia => articulos.Contains(existencia.ArticuloId));
        }

        var filas = await delAlmacen
            .Select(existencia => new
            {
                existencia.UbicacionId,
                existencia.ArticuloId,
                CodigoDeLote = contexto.Lotes
                    .Where(lote => lote.Id == existencia.LoteId)
                    .Select(lote => lote.Codigo)
                    .FirstOrDefault(),
                NumeroDeSerie = contexto.NumerosDeSerie
                    .Where(serie => serie.Id == existencia.NumeroDeSerieId)
                    .Select(serie => serie.Numero)
                    .FirstOrDefault(),
                existencia.Fisico,
                existencia.EnTransito,
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return [.. filas.Select(fila => new ExistenciaDeUnaClave(
            ClaveDelRecuento.De(fila.UbicacionId, fila.ArticuloId, fila.CodigoDeLote, fila.NumeroDeSerie),
            fila.Fisico,
            fila.EnTransito))];
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Por la flecha de vuelta: el ajuste apunta a su recuento, y el índice
    /// <c>ix_ajustes_recuento_id</c> dice que como mucho hay uno. Viene con sus líneas, que es de
    /// donde sale lo que movió cada una.
    /// </remarks>
    public Task<Ajuste?> AjusteDeAsync(Guid recuentoId, CancellationToken cancelacion) =>
        contexto.Ajustes
            .AsNoTracking()
            .FirstOrDefaultAsync(ajuste => ajuste.RecuentoId == recuentoId, cancelacion);

    /// <inheritdoc/>
    /// <remarks>
    /// <b>Sin las líneas</b>: el resumen no las enseña, y un recuento de un almacén entero puede tener
    /// miles.
    /// </remarks>
    public Task<PaginaDe<Recuento>> ListarAsync(
        Paginacion paginacion,
        EstadoDeRecuento? estado,
        Guid? almacenId,
        CancellationToken cancelacion)
    {
        IQueryable<Recuento> consulta = contexto.Recuentos.IgnoreAutoIncludes();

        if (estado is { } elEstado)
        {
            consulta = consulta.Where(recuento => recuento.Estado == elEstado);
        }

        if (almacenId is { } elAlmacen)
        {
            consulta = consulta.Where(recuento => recuento.AlmacenId == elAlmacen);
        }

        return consulta.PaginarAsync(paginacion, s_criterios, cancelacion);
    }
}
