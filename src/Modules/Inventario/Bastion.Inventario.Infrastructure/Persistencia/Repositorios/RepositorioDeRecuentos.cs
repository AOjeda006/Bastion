using System.Linq.Expressions;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Inventario.Application.Recuentos;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Recuentos;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Inventario.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeRecuentos"/>
/// <param name="contexto">El contexto del módulo, con el filtro de la empresa puesto (R8).</param>
/// <param name="inquilino">
/// De dónde sale la empresa del cerrojo: el mismo sitio del que la toma el filtro, que no alcanza al
/// SQL crudo.
/// </param>
internal sealed class RepositorioDeRecuentos(InventarioDbContext contexto, IInquilinoActual inquilino)
    : IRepositorioDeRecuentos
{
    /// <summary>
    /// <b>La sentencia que bloquea</b>: la fila del recuento, con el cerrojo del <c>UPDATE</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es SQL crudo porque ninguna cláusula de bloqueo tiene traducción en EF Core</b>, como el
    /// cerrojo del artículo en Catálogo.
    /// </para>
    /// <para>
    /// <b>La empresa la compara la sentencia</b>: el identificador viene de la ruta, y sin esa
    /// comparación quien conociera el de un recuento ajeno podría dejarlo bloqueado desde otra
    /// empresa.
    /// </para>
    /// <para>
    /// <b>Sin punto y coma final</b>: EF Core compone esta cadena dentro de otra sentencia, y un punto
    /// y coma ahí dentro es un error de sintaxis en tiempo de ejecución (ADR-0043).
    /// </para>
    /// </remarks>
    internal const string SqlDelCerrojo =
        "SELECT r.id AS \"Value\"" +
        " FROM inventario.recuentos AS r" +
        " WHERE r.id = {0}" +
        " AND r.empresa_id = {1}" +
        " FOR NO KEY UPDATE";

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
    public async Task<bool> BloquearAsync(Guid id, CancellationToken cancelacion)
    {
        // REVIENTA SI NO HAY TRANSACCIÓN, como el cerrojo del artículo: EF Core abre una transacción
        // IMPLÍCITA por cada orden, así que sin esto el cerrojo se soltaría al acabar esta lectura y
        // otra escritura en el recuento se colaría entre la lectura y el `COMMIT`. Lanza y no
        // devuelve un fallo de negocio (ADR-0004): está mal cableado.
        if (contexto.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "No hay transacción abierta en el contexto de Inventario, así que el cerrojo sobre la " +
                "fila del recuento se soltaría al acabar esta lectura. Quien lo pide tiene que ir " +
                "dentro de `EnTransaccionAsync` de la unidad de trabajo del módulo.");
        }

        Guid empresaId = inquilino.EmpresaDelFiltro ?? throw new InvalidOperationException(
            "Se está bloqueando un recuento dentro de un ámbito sin inquilino, y un recuento es " +
            "siempre de una empresa: sin ella la sentencia bloquearía el de cualquiera.");

        List<Guid> filas = await contexto.Database
            .SqlQueryRaw<Guid>(SqlDelCerrojo, id, empresaId)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return filas.Count > 0;
    }

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
