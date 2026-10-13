using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Inventario.Application.Saldos;
using Bastion.Inventario.Contracts.Saldos;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Inventario.Infrastructure.Persistencia.Saldos;

/// <inheritdoc cref="ILecturaDeSaldos"/>
/// <remarks>
/// <para>
/// <b>El saldo a la fecha D es la instantánea del mes M más el libro desde el día 1 del mes
/// siguiente hasta D</b> (ADR-0063 §2). M es el mes anterior al de D, o el corte de la empresa si el
/// corte es anterior; sin corte, todo el libro hasta D. Una clave sin instantánea en el mes M no se
/// había movido todavía y suma cero: las instantáneas son densas desde el primer mes de cada clave
/// hasta el corte (ADR-0044 §3).
/// </para>
/// <para>
/// <b>Una sola sentencia, sin transacción</b>: la página, su total, el saldo y el tránsito salen de
/// la misma foto. Es una lectura, y nadie decide con ella nada que vuelva a escribir el libro.
/// </para>
/// </remarks>
/// <param name="contexto">El contexto de Inventario.</param>
/// <param name="inquilino">De dónde sale la empresa, porque la sentencia cruda no pasa por el filtro.</param>
internal sealed class LosSaldosAUnaFecha(InventarioDbContext contexto, IInquilinoActual inquilino) : ILecturaDeSaldos
{
    /// <summary>
    /// Una página de saldos de la empresa <c>{0}</c> al final del día <c>{1}</c>, acotada a los
    /// almacenes <c>{2}</c> y a los artículos <c>{3}</c> (vacíos, todos), desde el par <c>{4}</c> y
    /// con <c>{5}</c> como mucho.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Los pares son los de la valoración</b> (ADR-0063 §1 y §3): los que se han movido alguna vez,
    /// con su divisa. Un par anterior a su primer movimiento sale con ceros, y no falta.
    /// </para>
    /// <para>
    /// <b>El mes M sale en la propia sentencia</b>, porque el corte está en la base. El mes anterior
    /// al de D se calcula desde <c>timestamp</c>, como en el resto del módulo, y un corte que falta
    /// deja M nulo: entonces no hay instantánea que leer, y el libro se lee entero hasta D. Por eso
    /// <c>least</c> va dentro de una subconsulta escalar y no fuera: con un corte nulo devolvería el
    /// otro argumento, y leería la instantánea de un mes que nadie ha cortado.
    /// </para>
    /// <para>
    /// <b>El libro se acota por su fecha por los dos lados</b>, que es lo que poda las particiones: un
    /// mes por partición, y desde el corte solo se leen las del tramo.
    /// </para>
    /// <para>
    /// <b>El tránsito hacia el almacén va aparte</b> (ADR-0063 §4): las líneas de las transferencias
    /// originales que no son borradores, enviadas en D o antes, sin recibir en D, y sin un inverso que
    /// en D ya hubiera devuelto la mercancía. El inverso nace recibido con las dos fechas del día de la
    /// anulación (ADR-0053 §5). La cantidad es la de la línea redondeada como la del libro, y el valor,
    /// el que fijó el envío. La línea no lleva empresa: es la de su transferencia.
    /// </para>
    /// <para>
    /// <b>Una página vacía da una fila</b>, con el total y lo demás nulo, para que el total llegue
    /// igual. El orden de la página se pide fuera, al componer: dentro de la subconsulta que monta
    /// EF Core no está garantizado (ADR-0043).
    /// </para>
    /// </remarks>
    internal const string SqlDelSaldo =
        """
        WITH tramo AS (
            SELECT (
                SELECT least((date_trunc('month', {1}::timestamp) - interval '1 month')::date, c.hasta_el_mes)
                FROM inventario.cortes_de_la_instantanea AS c
                WHERE c.empresa_id = {0}) AS mes
        ),
        pares AS (
            SELECT v.articulo_id, v.almacen_id, v.divisa
            FROM inventario.valoraciones AS v
            WHERE v.empresa_id = {0}
                AND (cardinality({2}::uuid[]) = 0 OR v.almacen_id = ANY ({2}::uuid[]))
                AND (cardinality({3}::uuid[]) = 0 OR v.articulo_id = ANY ({3}::uuid[]))
        ),
        pagina AS (
            SELECT p.articulo_id, p.almacen_id, p.divisa
            FROM pares AS p
            ORDER BY p.articulo_id, p.almacen_id
            OFFSET {4} LIMIT {5}
        )
        SELECT q.total, s.articulo_id, s.almacen_id, s.divisa, s.cantidad, s.valor,
               s.cantidad_en_transito, s.valor_en_transito
        FROM (SELECT count(*) AS total FROM pares) AS q
        LEFT JOIN (
            SELECT g.articulo_id, g.almacen_id, g.divisa,
                   coalesce(f.fisico, 0) + coalesce(b.cantidad, 0) AS cantidad,
                   coalesce(f.valor, 0) + coalesce(b.valor, 0) AS valor,
                   coalesce(x.cantidad, 0) AS cantidad_en_transito,
                   coalesce(x.valor, 0) AS valor_en_transito
            FROM pagina AS g
            CROSS JOIN tramo
            CROSS JOIN LATERAL (
                SELECT sum(i.fisico) AS fisico, sum(i.valor) AS valor
                FROM inventario.instantaneas_mensuales AS i
                JOIN inventario.existencias AS e ON e.id = i.existencia_id
                WHERE i.empresa_id = {0}
                    AND e.empresa_id = {0}
                    AND e.articulo_id = g.articulo_id
                    AND e.almacen_id = g.almacen_id
                    AND i.mes = tramo.mes
            ) AS f
            CROSS JOIN LATERAL (
                SELECT sum(m.cantidad_en_unidad_base) AS cantidad, sum(m.valor) AS valor
                FROM inventario.movimiento_stock AS m
                WHERE m.empresa_id = {0}
                    AND m.articulo_id = g.articulo_id
                    AND m.almacen_id = g.almacen_id
                    AND m.fecha_de_operacion >= coalesce((tramo.mes + interval '1 month')::date, '-infinity'::date)
                    AND m.fecha_de_operacion <= {1}
            ) AS b
            CROSS JOIN LATERAL (
                SELECT sum(round(l.cantidad_introducida * l.factor_a_unidad_base, 6)) AS cantidad,
                       sum(l.valor) AS valor
                FROM inventario.transferencias AS t
                JOIN inventario.lineas_transferencia AS l ON l.transferencia_id = t.id
                WHERE t.empresa_id = {0}
                    AND t.almacen_destino_id = g.almacen_id
                    AND l.articulo_id = g.articulo_id
                    AND t.anula_a_id IS NULL
                    AND t.estado <> 'Borrador'
                    AND t.fecha_de_envio <= {1}
                    AND (t.fecha_de_recepcion IS NULL OR t.fecha_de_recepcion > {1})
                    AND NOT EXISTS (
                        SELECT 1
                        FROM inventario.transferencias AS n
                        WHERE n.empresa_id = {0}
                            AND n.anula_a_id = t.id
                            AND n.fecha_de_recepcion <= {1})
            ) AS x
        ) AS s ON true
        """;

    /// <inheritdoc/>
    public async Task<PaginaDe<SaldoDto>> LeerAsync(
        DateOnly fecha,
        Guid? almacenId,
        Guid? articuloId,
        Paginacion paginacion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(paginacion);

        Guid empresaId = inquilino.EmpresaDelFiltro ?? throw new InvalidOperationException(
            "Se está leyendo el saldo dentro de un ámbito sin inquilino, y el libro es siempre de " +
            "una empresa: sin ella la sentencia sumaría el de cualquiera.");

        Guid[] almacenes = almacenId is { } almacen ? [almacen] : [];
        Guid[] articulos = articuloId is { } articulo ? [articulo] : [];

        List<FilaDelSaldo> filas = await contexto.Database
            .SqlQueryRaw<FilaDelSaldo>(
                SqlDelSaldo, empresaId, fecha, almacenes, articulos, paginacion.Salto, paginacion.Tamanio)
            .OrderBy(fila => fila.ArticuloId)
            .ThenBy(fila => fila.AlmacenId)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<SaldoDto>(
            [.. filas
                .Where(fila => fila.ArticuloId is not null)
                .Select(fila => new SaldoDto(
                    fila.ArticuloId!.Value,
                    fila.AlmacenId!.Value,
                    fila.Cantidad!.Value,
                    fila.Valor!.Value,
                    fila.CantidadEnTransito!.Value,
                    fila.ValorEnTransito!.Value,
                    fila.Divisa!))],
            paginacion.Pagina,
            paginacion.Tamanio,
            filas.Count == 0 ? 0 : filas[0].Total);
    }
}

/// <summary>Una fila del saldo de un par, tal como llega.</summary>
/// <remarks>
/// <b>Todo nulable salvo el total</b>: una página vacía llega como una fila con el total y nada más.
/// Las columnas se leen por el nombre que les da la convención del contexto.
/// </remarks>
internal sealed class FilaDelSaldo
{
    public long Total { get; init; }

    public Guid? ArticuloId { get; init; }

    public Guid? AlmacenId { get; init; }

    public string? Divisa { get; init; }

    public decimal? Cantidad { get; init; }

    public decimal? Valor { get; init; }

    public decimal? CantidadEnTransito { get; init; }

    public decimal? ValorEnTransito { get; init; }
}
