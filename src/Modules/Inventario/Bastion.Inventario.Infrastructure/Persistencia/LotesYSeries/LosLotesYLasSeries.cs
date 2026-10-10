using Bastion.Inventario.Domain.LotesYSeries;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Inventario.Infrastructure.Persistencia.LotesYSeries;

/// <summary>
/// Las sentencias que dan una fila a cada lote y a cada serie de un documento, creándola la primera
/// vez que se nombra (ADR-0048 §2).
/// </summary>
/// <remarks>
/// <para>
/// <b>Crea con <c>INSERT … ON CONFLICT DO NOTHING</c> y lee después.</b> <c>DO NOTHING</c> y no
/// <c>DO UPDATE</c>: la fila del lote no se escribe nunca, así que no hace falta bloquearla, y la
/// serialización la ponen la valoración y la existencia. Dos primeras entradas del mismo lote a la
/// vez se ordenan solas en el índice único: la segunda espera y, cuando la primera confirma, su
/// lectura ve la fila, porque en <c>READ COMMITTED</c> cada sentencia toma su foto.
/// </para>
/// <para>
/// <b>En orden de artículo y código, y lo pone el motor.</b> Es lo que impide el interbloqueo entre
/// dos documentos que traen los mismos lotes nuevos: los dos esperan en el índice en el mismo orden.
/// No vale el orden de la aplicación, porque <see cref="Guid"/> no compara como <c>uuid</c>.
/// </para>
/// <para>
/// <b>La lectura va por el ORM</b>, con el filtro global puesto, y no con <c>RETURNING</c>: un lote
/// que ya existía no vuelve en el <c>RETURNING</c> de un <c>DO NOTHING</c>.
/// </para>
/// </remarks>
internal static class LosLotesYLasSeries
{
    /// <summary>Crea los lotes que falten, en orden de artículo y código.</summary>
    internal const string SqlQueCreaLosLotes =
        """
        INSERT INTO inventario.lotes (id, empresa_id, articulo_id, codigo)
        SELECT c.id, {0}, c.articulo_id, c.codigo
        FROM unnest({1}::uuid[], {2}::uuid[], {3}::text[]) AS c (id, articulo_id, codigo)
        ORDER BY c.articulo_id, c.codigo COLLATE "C"
        ON CONFLICT (empresa_id, articulo_id, codigo) DO NOTHING
        """;

    /// <summary>Crea las series que falten, en orden de artículo y número.</summary>
    internal const string SqlQueCreaLasSeries =
        """
        INSERT INTO inventario.numeros_de_serie (id, empresa_id, articulo_id, numero)
        SELECT c.id, {0}, c.articulo_id, c.numero
        FROM unnest({1}::uuid[], {2}::uuid[], {3}::text[]) AS c (id, articulo_id, numero)
        ORDER BY c.articulo_id, c.numero COLLATE "C"
        ON CONFLICT (empresa_id, articulo_id, numero) DO NOTHING
        """;

    /// <summary>Da una fila a cada lote y a cada serie, y las lee.</summary>
    /// <remarks>
    /// Primero todos los lotes y después todas las series, en todos los documentos: el orden entre
    /// las dos tablas también es parte de lo que impide el interbloqueo.
    /// </remarks>
    /// <param name="contexto">El contexto de Inventario, con la transacción abierta.</param>
    /// <param name="empresaId">La empresa del inquilino.</param>
    /// <param name="lotes">Los lotes del documento.</param>
    /// <param name="series">Las series del documento.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>La fila de cada uno.</returns>
    /// <exception cref="InvalidOperationException">
    /// No hay transacción abierta, o algún código no ha vuelto en la lectura.
    /// </exception>
    internal static async Task<LotesYSeriesResueltos> ResolverAsync(
        InventarioDbContext contexto,
        Guid empresaId,
        IReadOnlyList<CodigoDeUnArticulo> lotes,
        IReadOnlyList<CodigoDeUnArticulo> series,
        CancellationToken cancelacion)
    {
        if (lotes.Count == 0 && series.Count == 0)
        {
            return LotesYSeriesResueltos.Ninguno;
        }

        // REVIENTA SI NO HAY TRANSACCIÓN: el lote se confirmaría solo, y un documento que no llega
        // a confirmarse dejaría detrás lotes que nadie movió.
        if (contexto.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "No hay transacción abierta en el contexto de Inventario, así que los lotes y las " +
                "series se crearían por su cuenta aunque el documento no llegara a confirmarse. El " +
                "dueño de la transacción es el filtro de idempotencia: la acción que confirma tiene " +
                "que declarar la Idempotency-Key obligatoria.");
        }

        CodigoDeUnArticulo[] pedidosDeLote = [.. lotes.Distinct()];
        CodigoDeUnArticulo[] pedidosDeSerie = [.. series.Distinct()];

        await CrearAsync(contexto, SqlQueCreaLosLotes, empresaId, pedidosDeLote, cancelacion)
            .ConfigureAwait(false);
        await CrearAsync(contexto, SqlQueCreaLasSeries, empresaId, pedidosDeSerie, cancelacion)
            .ConfigureAwait(false);

        LotesYSeriesResueltos leidos = await LeerAsync(contexto, pedidosDeLote, pedidosDeSerie, cancelacion)
            .ConfigureAwait(false);

        // TIENEN QUE ESTAR TODOS: la sentencia de antes los acababa de crear o ya estaban.
        ExigirTodos(pedidosDeLote, leidos.Lotes);
        ExigirTodos(pedidosDeSerie, leidos.Series);

        return leidos;
    }

    /// <summary>
    /// Busca la fila de cada lote y de cada serie <b>sin crear</b> los que falten, y devuelve solo
    /// los que existen (ADR-0059 §6).
    /// </summary>
    /// <remarks>
    /// Es la de quien saca sin haber entrado nada: un consumo de reserva. Un lote que no existe no
    /// tiene nada que sacar, y crearlo dejaría una fila que nadie movió.
    /// </remarks>
    /// <param name="contexto">El contexto de Inventario.</param>
    /// <param name="lotes">Los lotes que se nombran.</param>
    /// <param name="series">Las series que se nombran.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>La fila de cada uno que existe en la empresa.</returns>
    internal static Task<LotesYSeriesResueltos> BuscarAsync(
        InventarioDbContext contexto,
        IReadOnlyList<CodigoDeUnArticulo> lotes,
        IReadOnlyList<CodigoDeUnArticulo> series,
        CancellationToken cancelacion) =>
        lotes.Count == 0 && series.Count == 0
            ? Task.FromResult(LotesYSeriesResueltos.Ninguno)
            : LeerAsync(contexto, [.. lotes.Distinct()], [.. series.Distinct()], cancelacion);

    private static async Task<LotesYSeriesResueltos> LeerAsync(
        InventarioDbContext contexto,
        CodigoDeUnArticulo[] pedidosDeLote,
        CodigoDeUnArticulo[] pedidosDeSerie,
        CancellationToken cancelacion)
    {
        Dictionary<CodigoDeUnArticulo, Guid> filasDeLote = [];

        if (pedidosDeLote.Length > 0)
        {
            (Guid[] articulos, string[] codigos) = Partes(pedidosDeLote);

            var leidos = await contexto.Lotes
                .AsNoTracking()
                .Where(lote => articulos.Contains(lote.ArticuloId) && codigos.Contains(lote.Codigo))
                .Select(lote => new { lote.Id, lote.ArticuloId, lote.Codigo })
                .ToListAsync(cancelacion)
                .ConfigureAwait(false);

            filasDeLote = Emparejar(
                pedidosDeLote, leidos.Select(lote => (new CodigoDeUnArticulo(lote.ArticuloId, lote.Codigo), lote.Id)));
        }

        Dictionary<CodigoDeUnArticulo, Guid> filasDeSerie = [];

        if (pedidosDeSerie.Length > 0)
        {
            (Guid[] articulos, string[] numeros) = Partes(pedidosDeSerie);

            var leidas = await contexto.NumerosDeSerie
                .AsNoTracking()
                .Where(serie => articulos.Contains(serie.ArticuloId) && numeros.Contains(serie.Numero))
                .Select(serie => new { serie.Id, serie.ArticuloId, serie.Numero })
                .ToListAsync(cancelacion)
                .ConfigureAwait(false);

            filasDeSerie = Emparejar(
                pedidosDeSerie, leidas.Select(serie => (new CodigoDeUnArticulo(serie.ArticuloId, serie.Numero), serie.Id)));
        }

        return new LotesYSeriesResueltos(filasDeLote, filasDeSerie);
    }

    private static Task CrearAsync(
        InventarioDbContext contexto,
        string sql,
        Guid empresaId,
        CodigoDeUnArticulo[] pedidos,
        CancellationToken cancelacion)
    {
        if (pedidos.Length == 0)
        {
            return Task.CompletedTask;
        }

        Guid[] ids = [.. pedidos.Select(_ => Guid.CreateVersion7())];
        Guid[] articulos = [.. pedidos.Select(pedido => pedido.ArticuloId)];
        string[] codigos = [.. pedidos.Select(pedido => pedido.Codigo)];

        return contexto.Database.ExecuteSqlRawAsync(sql, [empresaId, ids, articulos, codigos], cancelacion);
    }

    private static (Guid[] Articulos, string[] Codigos) Partes(CodigoDeUnArticulo[] pedidos) =>
    (
        [.. pedidos.Select(pedido => pedido.ArticuloId).Distinct()],
        [.. pedidos.Select(pedido => pedido.Codigo).Distinct(StringComparer.Ordinal)]
    );

    /// <summary>Se queda con la fila de cada código pedido.</summary>
    /// <remarks>
    /// La lectura filtra por artículo y por texto por separado, así que puede traer un par que el
    /// documento no nombra -el lote de un artículo con el texto de otro-, y se descarta.
    /// </remarks>
    private static Dictionary<CodigoDeUnArticulo, Guid> Emparejar(
        CodigoDeUnArticulo[] pedidos, IEnumerable<(CodigoDeUnArticulo Codigo, Guid Id)> leidas)
    {
        HashSet<CodigoDeUnArticulo> buscados = [.. pedidos];
        Dictionary<CodigoDeUnArticulo, Guid> resueltas = [];

        foreach ((CodigoDeUnArticulo codigo, Guid id) in leidas)
        {
            if (buscados.Contains(codigo))
            {
                resueltas[codigo] = id;
            }
        }

        return resueltas;
    }

    /// <summary>Exige que la lectura haya traído todos los códigos que se acaban de crear.</summary>
    private static void ExigirTodos(
        CodigoDeUnArticulo[] pedidos, IReadOnlyDictionary<CodigoDeUnArticulo, Guid> resueltas)
    {
        if (resueltas.Count != pedidos.Length)
        {
            throw new InvalidOperationException(
                $"Se pidieron {pedidos.Length} códigos y la lectura ha traído {resueltas.Count}: la " +
                "sentencia de antes los acababa de crear o ya estaban, así que falta alguno que el " +
                "filtro de empresa no deja ver.");
        }
    }
}
