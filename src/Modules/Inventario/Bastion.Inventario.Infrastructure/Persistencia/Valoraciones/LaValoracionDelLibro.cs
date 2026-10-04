using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Inventario.Infrastructure.Persistencia.Existencias;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Inventario.Infrastructure.Persistencia.Valoraciones;

/// <summary>
/// Las tres sentencias de la valoración: una bloquea, otra lee lo bloqueado y la última suma sobre
/// ello (ADR-0046 §2).
/// </summary>
/// <remarks>
/// <para>
/// <b>Se lee, y eso es lo que la separa de la existencia.</b> La existencia se suma sin leerla. La
/// valoración no puede: el valor de una salida depende del precio medio de ese momento, y el de una
/// línea, de lo que dejó la anterior del mismo artículo. Eso lo calcula el dominio, con el redondeo
/// de la R6 en un solo sitio. Así que se lee, pero con la fila ya bloqueada: la lectura no se queda
/// vieja porque nadie más puede tocar la fila hasta el <c>COMMIT</c>.
/// </para>
/// <para>
/// <b>Bloquear es crear.</b> El <c>DO UPDATE</c> que no cambia nada bloquea la fila, y además la
/// clave que todavía no existe. Dos primeras entradas simultáneas de un artículo no encontrarían
/// nada que bloquear con un <c>FOR UPDATE</c>, y las dos valorarían desde cero.
/// </para>
/// <para>
/// <b>El criterio del SQL crudo, en sus cuatro cláusulas</b> (ADR-0040, ADR-0046 §9):
/// </para>
/// <list type="number">
/// <item>van en la transacción del filtro de idempotencia, y sin ella revientan;</item>
/// <item>la que lee va después de la que bloquea, y la que escribe suma sobre filas de esta
/// transacción: ningún valor lo decide quien llama, salen del libro;</item>
/// <item>EF Core no traduce el <c>ON CONFLICT</c>, y leer por el ORM y escribir después es la
/// ventana que el cerrojo cierra;</item>
/// <item>las tres filtran por la empresa del inquilino, que es parte de la clave.</item>
/// </list>
/// <para>
/// <b>Sin punto y coma final</b>, como las demás cadenas crudas del proyecto: la lectura va dentro
/// de un <c>SELECT … FROM (…)</c>, y ahí un punto y coma es un error de sintaxis.
/// </para>
/// </remarks>
internal static class LaValoracionDelLibro
{
    /// <summary>Crea vacía la valoración de cada clave nueva y bloquea la de todas, en orden de clave.</summary>
    /// <remarks>
    /// <b>Propone cero, que cumple las tres guardas</b> tanto si la fila entra como si choca: el
    /// motor las mira sobre la fila propuesta antes de mirar si choca (ADR-0046 §4). La divisa es
    /// la del documento, y solo la toma una fila que nace.
    /// </remarks>
    internal const string SqlQueCreaYBloquea =
        """
        INSERT INTO inventario.valoraciones AS v
            (empresa_id, articulo_id, almacen_id, cantidad, valor, divisa, en_transito, valor_en_transito)
        SELECT {0}, c.articulo_id, c.almacen_id, 0, 0, {1}, 0, 0
        FROM unnest({2}::uuid[], {3}::uuid[]) AS c (articulo_id, almacen_id)
        GROUP BY c.articulo_id, c.almacen_id
        ORDER BY c.articulo_id, c.almacen_id
        ON CONFLICT (empresa_id, articulo_id, almacen_id)
        DO UPDATE SET cantidad = v.cantidad
        """;

    /// <summary>Lee las valoraciones que la sentencia anterior acaba de bloquear.</summary>
    /// <remarks>
    /// <b>Va aparte y no con <c>RETURNING</c></b>, porque <c>SqlQueryRaw</c> mete el texto dentro de
    /// un <c>SELECT … FROM (…)</c>, y PostgreSQL no admite un <c>INSERT</c> ahí. En
    /// <c>READ COMMITTED</c> cada sentencia toma su foto, así que esta ve lo que dejó quien tenía
    /// la fila antes de que el cerrojo la soltara.
    /// </remarks>
    internal const string SqlQueLeeLoBloqueado =
        """
        SELECT v.articulo_id, v.almacen_id, v.cantidad, v.valor, v.divisa, v.ultima_fecha, v.en_transito
        FROM inventario.valoraciones AS v
        JOIN unnest({1}::uuid[], {2}::uuid[]) AS c (articulo_id, almacen_id)
            ON c.articulo_id = v.articulo_id
            AND c.almacen_id = v.almacen_id
        WHERE v.empresa_id = {0}
        """;

    /// <summary>
    /// <b>La sentencia que decide.</b> Suma a cada valoración la cantidad y el valor de las filas
    /// del libro de su clave.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La divisa pasa a ser la del documento</b>, y eso solo cambia algo en una clave que se
    /// había quedado sin cantidad: con cantidad en otra divisa, el caso de uso ya contestó
    /// <c>ajuste-valoracion-en-otra-divisa</c>. La condición del <c>WHERE</c> lo vuelve a exigir,
    /// y una clave que no la cumpla no se toca, así que el recuento lo denuncia.
    /// </para>
    /// <para>
    /// <b>Vacía es sin cantidad y sin nada en vuelo</b> desde el 2.11 (ADR-0053 §1). Lo que vuela
    /// hacia una clave lleva el valor en la divisa de su documento, y cambiarle la divisa a la clave
    /// dejaría ese valor con otra moneda al aterrizar.
    /// </para>
    /// <para>
    /// <b>La fecha no va hacia atrás</b> (ADR-0047 §3). Una clave cuyo último movimiento sea
    /// posterior a la fila más temprana del documento no se toca, y el recuento lo denuncia, como
    /// con la divisa. El caso de uso ya contestó <c>ajuste-fecha-anterior-al-ultimo-movimiento</c>
    /// contra la fila bloqueada, así que llegar aquí es haberse saltado el dominio. No es un
    /// <c>CHECK</c> porque un <c>CHECK</c> no ve la fila de antes, que es justo sobre lo que habla
    /// la regla.
    /// </para>
    /// <para>
    /// <b>Las tres guardas se miran sobre la fila ya sumada.</b> Va después de la sentencia de la
    /// existencia, así que una salida sin stock ya ha chocado allí, con su nombre.
    /// </para>
    /// </remarks>
    internal const string SqlQueSuma =
        """
        UPDATE inventario.valoraciones AS v
        SET cantidad = v.cantidad + d.cantidad,
            valor = v.valor + d.valor,
            divisa = {1},
            ultima_fecha = d.ultima
        FROM (
            SELECT m.articulo_id, m.almacen_id, sum(m.cantidad) AS cantidad, sum(m.valor) AS valor,
                min(m.fecha) AS primera, max(m.fecha) AS ultima
            FROM unnest({2}::uuid[], {3}::uuid[], {4}::numeric[], {5}::numeric[], {6}::date[])
                AS m (articulo_id, almacen_id, cantidad, valor, fecha)
            GROUP BY m.articulo_id, m.almacen_id) AS d
        WHERE v.empresa_id = {0}
            AND v.articulo_id = d.articulo_id
            AND v.almacen_id = d.almacen_id
            AND (v.divisa = {1} OR (v.cantidad = 0 AND v.en_transito = 0))
            AND (v.ultima_fecha IS NULL OR v.ultima_fecha <= d.primera)
        """;

    /// <summary>Bloquea la valoración de cada clave y la lee ya bloqueada.</summary>
    /// <param name="contexto">El contexto de Inventario, con la transacción abierta.</param>
    /// <param name="empresaId">La empresa del inquilino.</param>
    /// <param name="claves">Las claves del documento.</param>
    /// <param name="divisa">La del documento, para las claves que nazcan.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>El saldo bloqueado de cada clave.</returns>
    /// <exception cref="InvalidOperationException">
    /// No hay transacción abierta, o alguna clave bloqueada no ha vuelto en la lectura.
    /// </exception>
    internal static async Task<IReadOnlyDictionary<ClaveDeValoracion, SaldoValorado>> BloquearYLeerAsync(
        InventarioDbContext contexto,
        Guid empresaId,
        IReadOnlyCollection<ClaveDeValoracion> claves,
        string divisa,
        CancellationToken cancelacion)
    {
        ExigirLaTransaccion(contexto);

        ClaveDeValoracion[] distintas = [.. claves.Distinct()];

        if (distintas.Length == 0)
        {
            return new Dictionary<ClaveDeValoracion, SaldoValorado>();
        }

        Guid[] articulos = [.. distintas.Select(clave => clave.ArticuloId)];
        Guid[] almacenes = [.. distintas.Select(clave => clave.AlmacenId)];

        await contexto.Database
            .ExecuteSqlRawAsync(
                SqlQueCreaYBloquea,
                [empresaId, CatalogoDeDivisas.Normalizar(divisa), articulos, almacenes],
                cancelacion)
            .ConfigureAwait(false);

        List<FilaDeValoracion> filas = await contexto.Database
            .SqlQueryRaw<FilaDeValoracion>(SqlQueLeeLoBloqueado, empresaId, articulos, almacenes)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        if (filas.Count != distintas.Length)
        {
            throw new InvalidOperationException(
                $"Se bloquearon {distintas.Length} valoraciones y la lectura ha traído {filas.Count}: " +
                "valorar sin la fila de cada clave sería valorar sin cerrojo.");
        }

        return filas.ToDictionary(
            fila => new ClaveDeValoracion(fila.ArticuloId, fila.AlmacenId),
            fila => new SaldoValorado(
                fila.Cantidad, Importe.De(fila.Valor, fila.Divisa), fila.UltimaFecha, fila.EnTransito));
    }

    /// <summary>Suma a cada valoración lo que traen las filas del libro que se van a anotar.</summary>
    /// <param name="contexto">El contexto de Inventario, con la transacción abierta.</param>
    /// <param name="empresaId">La empresa del inquilino, que es la de todas las filas.</param>
    /// <param name="movimientos">Las filas del libro que se van a anotar.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>Una tarea que acaba cuando la sentencia ha corrido.</returns>
    /// <exception cref="InvalidOperationException">
    /// No hay transacción, alguna fila es de otra empresa, las filas mezclan divisas, alguna clave
    /// no se bloqueó antes o alguna se movió después de la fecha del documento.
    /// </exception>
    internal static async Task SumarAsync(
        InventarioDbContext contexto,
        Guid empresaId,
        IReadOnlyCollection<MovimientoStock> movimientos,
        CancellationToken cancelacion)
    {
        LaProyeccionDelLibro.ExigirLoQueLaSentenciaNecesita(contexto, empresaId, movimientos);

        if (movimientos.Count == 0)
        {
            return;
        }

        string[] divisas = [.. movimientos.Select(movimiento => movimiento.Divisa).Distinct()];

        if (divisas.Length != 1)
        {
            throw new InvalidOperationException(
                $"Las filas que se van a anotar vienen en {string.Join(", ", divisas)}: un documento " +
                "habla una sola divisa, la de su cabecera (ADR-0046 §7).");
        }

        Guid[] articulos = [.. movimientos.Select(movimiento => movimiento.ArticuloId)];
        Guid[] almacenes = [.. movimientos.Select(movimiento => movimiento.AlmacenId)];
        decimal[] cantidades = [.. movimientos.Select(movimiento => movimiento.CantidadEnUnidadBase)];
        decimal[] valores = [.. movimientos.Select(movimiento => movimiento.Valor.Cantidad)];
        DateOnly[] fechas = [.. movimientos.Select(movimiento => movimiento.FechaDeOperacion)];

        int sumadas = await contexto.Database
            .ExecuteSqlRawAsync(
                SqlQueSuma,
                [empresaId, divisas[0], articulos, almacenes, cantidades, valores, fechas],
                cancelacion)
            .ConfigureAwait(false);

        int claves = movimientos
            .Select(movimiento => new ClaveDeValoracion(movimiento.ArticuloId, movimiento.AlmacenId))
            .Distinct()
            .Count();

        if (sumadas != claves)
        {
            throw new InvalidOperationException(
                $"El documento mueve {claves} valoraciones y la sentencia ha sumado {sumadas}: alguna " +
                "no se bloqueó antes, está en otra divisa con cantidad o con algo en vuelo, o se movió " +
                "después de la fecha del documento (ADR-0046 §2, ADR-0047 §3, ADR-0053 §1).");
        }
    }

    private static void ExigirLaTransaccion(InventarioDbContext contexto)
    {
        if (contexto.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "No hay transacción abierta en el contexto de Inventario, así que el cerrojo de la " +
                "valoración se soltaría al acabar la sentencia y lo leído se quedaría viejo. El " +
                "dueño de la transacción es el filtro de idempotencia.");
        }
    }
}

/// <summary>Una fila de la lectura de la valoración, tal como llega.</summary>
/// <remarks>
/// Las columnas se leen por el nombre que les da la convención del contexto, en <c>snake_case</c>,
/// que es el mismo que llevan en la consulta.
/// </remarks>
internal sealed class FilaDeValoracion
{
    public Guid ArticuloId { get; init; }

    public Guid AlmacenId { get; init; }

    public decimal Cantidad { get; init; }

    public decimal Valor { get; init; }

    public string Divisa { get; init; } = string.Empty;

    public DateOnly? UltimaFecha { get; init; }

    public decimal EnTransito { get; init; }
}
