using Bastion.BuildingBlocks.Application.Multiempresa;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Inventario.Infrastructure.Persistencia.Existencias;

/// <summary>
/// Recorre las existencias, las instantáneas y la valoración de una empresa y las compara con el
/// libro (R3, ADR-0044, ADR-0046).
/// </summary>
/// <remarks>
/// <para>
/// <b>Devuelve cuántas ha comparado, además de lo que no cuadra</b>, y es la mitad del criterio
/// del 2.7. Un cuadre que no encuentra nada que comparar sale limpio: si un día el filtro de la
/// empresa se torciera, o la consulta dejara de casar, la lista de descuadres vendría vacía y
/// parecería una buena noticia. Quien lo llama afirma que el conjunto comparado no era vacío.
/// </para>
/// <para>
/// <b>Una sola lectura</b>, y no una por tabla: el libro, las filas vivas, las instantáneas y la
/// valoración se leen con la misma foto del motor, así que una confirmación que acabe a mitad no
/// puede aparecer en una y no en la otra.
/// </para>
/// <para>
/// <b>La valoración es la tercera copia desde el 2.8</b>, y se cuadra en sus tres columnas: la
/// cantidad contra la suma de las cantidades del libro, el valor contra la suma de sus valores, y la
/// última fecha contra la más alta de sus fechas de operación (ADR-0050 §4). El precio medio no se
/// cuadra porque no se guarda: se deduce de las dos primeras.
/// </para>
/// <para>
/// <b>En el 2.7 no tiene quien lo llame en producción</b>, igual que el recálculo: el trabajo
/// periódico que avanza el corte y cuadra llega con el ítem 2.14.
/// </para>
/// </remarks>
/// <param name="contexto">El contexto de Inventario.</param>
/// <param name="inquilino">De donde sale la empresa que se cuadra.</param>
/// <param name="reloj">De dónde sale «hoy».</param>
internal sealed class ElCuadreDeLasExistencias(
    InventarioDbContext contexto, IInquilinoActual inquilino, TimeProvider reloj)
{
    /// <summary>La consulta del cuadre, para la empresa <c>{0}</c> y el día <c>{1}</c>.</summary>
    /// <remarks>
    /// <para>
    /// <b>El libro hasta hoy, no entero.</b> Confirmar rechaza las fechas futuras, así que las dos
    /// cosas deberían ser la misma; si no lo son, una fila del libro con fecha de mañana hace que
    /// la existencia cuente algo que todavía no ha pasado, y eso también es un descuadre.
    /// </para>
    /// <para>
    /// <b>Se junta con <c>UNION ALL</c> y se agrupa, en vez de cruzar con un <c>FULL JOIN</c></b>.
    /// El lote y el número de serie son nulos en casi todas las claves, y una igualdad con nulos no casa; con
    /// <c>IS NOT DISTINCT FROM</c> sí, pero entonces PostgreSQL no sabe hacer ese cruce. El
    /// <c>GROUP BY</c> trata los nulos como iguales, que es lo que la clave necesita, y de paso
    /// cuenta cuántas filas vivas tiene cada clave: dos filas para la misma es un descuadre aunque
    /// sumen lo que deben.
    /// </para>
    /// <para>
    /// <b>Las instantáneas se comparan contra las debidas del fragmento del recálculo</b>, por
    /// clave y mes, así que una que falte, una que sobre y una que diga otra cosa salen las tres.
    /// </para>
    /// <para>
    /// <b>La valoración, por artículo y almacén y sin mirar la divisa.</b> Una clave que cambia de
    /// divisa lo hace vacía, y vaciarla se lleva todo el valor (ADR-0046 §5), así que las filas de
    /// la divisa de antes suman cero y la suma de todas es la de la divisa de ahora. Una valoración
    /// sin filas en el libro cuadra solo si está a cero, igual que una fila viva.
    /// </para>
    /// <para>
    /// <b>La fecha se compara con <c>IS DISTINCT FROM</c></b>, porque las dos pueden ser nulas: la
    /// de una valoración que falta o que no la lleva, y la de una clave sin filas en el libro. Con
    /// <c>&lt;&gt;</c>, una fecha contra un nulo no saldría, y es justo la de una valoración que
    /// falta. Es una copia que decide: el caso de uso rechaza un documento anterior a ella.
    /// </para>
    /// </remarks>
    internal const string SqlDelCuadre =
        $$"""
        WITH {{LasInstantaneasMensuales.LasDebidas}},
        existencias_cuadradas AS (
            SELECT x.articulo_id, x.almacen_id, x.ubicacion_id, x.lote_id, x.numero_de_serie_id,
                   sum(x.esperado) AS esperado, sum(x.guardado) AS guardado, sum(x.filas) AS filas
            FROM (
                SELECT m.articulo_id, m.almacen_id, m.ubicacion_id, m.lote_id, m.numero_de_serie_id,
                       m.cantidad_en_unidad_base AS esperado, 0::numeric AS guardado, 0 AS filas
                FROM inventario.movimiento_stock AS m
                WHERE m.empresa_id = {0} AND m.fecha_de_operacion <= {1}
                UNION ALL
                SELECT e.articulo_id, e.almacen_id, e.ubicacion_id, e.lote_id, e.numero_de_serie_id, 0, e.fisico, 1
                FROM inventario.existencias AS e
                WHERE e.empresa_id = {0}
            ) AS x
            GROUP BY x.articulo_id, x.almacen_id, x.ubicacion_id, x.lote_id, x.numero_de_serie_id
        ),
        instantaneas_cuadradas AS (
            SELECT x.articulo_id, x.almacen_id, x.ubicacion_id, x.lote_id, x.numero_de_serie_id, x.mes,
                   sum(x.esperado) AS esperado, sum(x.guardado) AS guardado, sum(x.filas) AS filas
            FROM (
                SELECT d.articulo_id, d.almacen_id, d.ubicacion_id, d.lote_id, d.numero_de_serie_id, d.mes,
                       d.fisico AS esperado, 0::numeric AS guardado, 0 AS filas
                FROM debidas AS d
                UNION ALL
                SELECT e.articulo_id, e.almacen_id, e.ubicacion_id, e.lote_id, e.numero_de_serie_id, i.mes, 0,
                       i.fisico, 1
                FROM inventario.instantaneas_mensuales AS i
                JOIN inventario.existencias AS e ON e.id = i.existencia_id
                WHERE i.empresa_id = {0} AND e.empresa_id = {0}
            ) AS x
            GROUP BY x.articulo_id, x.almacen_id, x.ubicacion_id, x.lote_id, x.numero_de_serie_id, x.mes
        ),
        valoraciones_cuadradas AS (
            SELECT x.articulo_id, x.almacen_id,
                   sum(x.cantidad_esperada) AS cantidad_esperada,
                   sum(x.cantidad_guardada) AS cantidad_guardada,
                   sum(x.valor_esperado) AS valor_esperado, sum(x.valor_guardado) AS valor_guardado,
                   max(x.fecha_esperada) AS fecha_esperada, max(x.fecha_guardada) AS fecha_guardada,
                   sum(x.filas) AS filas
            FROM (
                SELECT m.articulo_id, m.almacen_id,
                       m.cantidad_en_unidad_base AS cantidad_esperada, 0::numeric AS cantidad_guardada,
                       m.valor AS valor_esperado, 0::numeric AS valor_guardado,
                       m.fecha_de_operacion AS fecha_esperada, NULL::date AS fecha_guardada, 0 AS filas
                FROM inventario.movimiento_stock AS m
                WHERE m.empresa_id = {0} AND m.fecha_de_operacion <= {1}
                UNION ALL
                SELECT v.articulo_id, v.almacen_id, 0, v.cantidad, 0, v.valor, NULL, v.ultima_fecha, 1
                FROM inventario.valoraciones AS v
                WHERE v.empresa_id = {0}
            ) AS x
            GROUP BY x.articulo_id, x.almacen_id
        )
        SELECT 'existencia' AS que, true AS es_resumen, count(*) AS comparadas,
               NULL::uuid AS articulo_id, NULL::uuid AS almacen_id, NULL::uuid AS ubicacion_id,
               NULL::uuid AS lote_id, NULL::uuid AS numero_de_serie_id, NULL::date AS mes,
               NULL::numeric AS esperado, NULL::numeric AS guardado, NULL::bigint AS filas,
               NULL::date AS fecha_esperada, NULL::date AS fecha_guardada
        FROM existencias_cuadradas
        UNION ALL
        SELECT 'instantanea', true, count(*), NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
               NULL, NULL
        FROM instantaneas_cuadradas
        UNION ALL
        SELECT 'valoracion', true, count(*), NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
               NULL, NULL
        FROM valoraciones_cuadradas
        UNION ALL
        SELECT 'existencia', false, 0, c.articulo_id, c.almacen_id, c.ubicacion_id, c.lote_id,
               c.numero_de_serie_id, NULL, c.esperado, c.guardado, c.filas, NULL, NULL
        FROM existencias_cuadradas AS c
        WHERE c.esperado <> c.guardado OR c.filas <> 1
        UNION ALL
        SELECT 'instantanea', false, 0, c.articulo_id, c.almacen_id, c.ubicacion_id, c.lote_id,
               c.numero_de_serie_id, c.mes, c.esperado, c.guardado, c.filas, NULL, NULL
        FROM instantaneas_cuadradas AS c
        WHERE c.esperado <> c.guardado OR c.filas <> 1
        UNION ALL
        SELECT 'valoracion-cantidad', false, 0, c.articulo_id, c.almacen_id, NULL, NULL, NULL, NULL,
               c.cantidad_esperada, c.cantidad_guardada, c.filas, NULL, NULL
        FROM valoraciones_cuadradas AS c
        WHERE c.cantidad_esperada <> c.cantidad_guardada OR c.filas <> 1
        UNION ALL
        SELECT 'valoracion-valor', false, 0, c.articulo_id, c.almacen_id, NULL, NULL, NULL, NULL,
               c.valor_esperado, c.valor_guardado, c.filas, NULL, NULL
        FROM valoraciones_cuadradas AS c
        WHERE c.valor_esperado <> c.valor_guardado
        UNION ALL
        SELECT 'valoracion-fecha', false, 0, c.articulo_id, c.almacen_id, NULL, NULL, NULL, NULL,
               NULL, NULL, c.filas, c.fecha_esperada, c.fecha_guardada
        FROM valoraciones_cuadradas AS c
        WHERE c.fecha_esperada IS DISTINCT FROM c.fecha_guardada
        """;

    /// <summary>Cuadra la empresa del inquilino.</summary>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>Cuántas claves, instantáneas y valoraciones ha comparado, y lo que no cuadra.</returns>
    /// <exception cref="InvalidOperationException">No hay empresa en el inquilino.</exception>
    internal async Task<CuadreDeLasExistencias> CuadrarAsync(CancellationToken cancelacion)
    {
        Guid empresaId = inquilino.EmpresaDelFiltro ?? throw new InvalidOperationException(
            "Se están cuadrando las existencias dentro de un ámbito sin inquilino, y una " +
            "existencia es siempre de una empresa: sin ella el cuadre mezclaría las de todas.");

        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);

        List<FilaDelCuadre> filas = await contexto.Database
            .SqlQueryRaw<FilaDelCuadre>(SqlDelCuadre, empresaId, hoy)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return new CuadreDeLasExistencias(
            filas.Single(fila => fila.EsResumen && fila.Que == Existencia).Comparadas,
            filas.Single(fila => fila.EsResumen && fila.Que == Instantanea).Comparadas,
            filas.Single(fila => fila.EsResumen && fila.Que == Valoracion).Comparadas,
            [.. filas
                .Where(fila => !fila.EsResumen)
                .Select(fila => new Descuadre(
                    fila.Que,
                    fila.ArticuloId!.Value,
                    fila.AlmacenId!.Value,
                    fila.UbicacionId,
                    fila.LoteId,
                    fila.NumeroDeSerieId,
                    fila.Mes,
                    fila.Esperado,
                    fila.Guardado,
                    fila.Filas!.Value,
                    fila.FechaEsperada,
                    fila.FechaGuardada))]);
    }

    private const string Existencia = "existencia";
    private const string Instantanea = "instantanea";
    private const string Valoracion = "valoracion";
}

/// <summary>Lo que devuelve el cuadre.</summary>
/// <param name="ExistenciasComparadas">Cuántas claves ha comparado entre el libro y las filas vivas.</param>
/// <param name="InstantaneasComparadas">Cuántos pares de clave y mes ha comparado.</param>
/// <param name="ValoracionesComparadas">Cuántos pares de artículo y almacén ha comparado.</param>
/// <param name="Descuadres">Lo que no cuadra; vacía si todo cuadra.</param>
internal sealed record CuadreDeLasExistencias(
    long ExistenciasComparadas,
    long InstantaneasComparadas,
    long ValoracionesComparadas,
    IReadOnlyList<Descuadre> Descuadres);

/// <summary>Una clave —o una clave y un mes— en la que la copia no dice lo que el libro.</summary>
/// <param name="Que">
/// «existencia», «instantanea», «valoracion-cantidad», «valoracion-valor» o «valoracion-fecha»; las
/// tres últimas son las tres columnas de la valoración, cada una con su descuadre.
/// </param>
/// <param name="ArticuloId">El artículo de la clave.</param>
/// <param name="AlmacenId">El almacén de la clave.</param>
/// <param name="UbicacionId">La ubicación de la clave; nula en la valoración, que no la lleva.</param>
/// <param name="LoteId">El lote de la clave, o nulo; nulo también en la valoración.</param>
/// <param name="NumeroDeSerieId">
/// El número de serie de la clave, o nulo; nulo también en la valoración.
/// </param>
/// <param name="Mes">El mes, en las instantáneas.</param>
/// <param name="Esperado">Lo que dice el libro; nulo en el de la fecha, que lleva fechas.</param>
/// <param name="Guardado">Lo que dice la copia; nulo en el de la fecha.</param>
/// <param name="Filas">Cuántas filas de la copia tiene la clave: tiene que ser una.</param>
/// <param name="FechaEsperada">
/// En el de la fecha, la fecha de operación más alta del libro hasta hoy, o nula si la clave no
/// tiene filas en él; nula en los demás.
/// </param>
/// <param name="FechaGuardada">
/// En el de la fecha, la <c>ultima_fecha</c> de la valoración, o nula si falta o no la lleva; nula
/// en los demás.
/// </param>
internal sealed record Descuadre(
    string Que,
    Guid ArticuloId,
    Guid AlmacenId,
    Guid? UbicacionId,
    Guid? LoteId,
    Guid? NumeroDeSerieId,
    DateOnly? Mes,
    decimal? Esperado,
    decimal? Guardado,
    long Filas,
    DateOnly? FechaEsperada,
    DateOnly? FechaGuardada);

/// <summary>Una fila de la consulta del cuadre, tal como llega.</summary>
/// <remarks>
/// Las columnas se leen por el nombre que les da la convención del contexto, en
/// <c>snake_case</c>, que es el mismo que llevan los alias de la consulta.
/// </remarks>
internal sealed class FilaDelCuadre
{
    public string Que { get; init; } = string.Empty;

    public bool EsResumen { get; init; }

    public long Comparadas { get; init; }

    public Guid? ArticuloId { get; init; }

    public Guid? AlmacenId { get; init; }

    public Guid? UbicacionId { get; init; }

    public Guid? LoteId { get; init; }

    public Guid? NumeroDeSerieId { get; init; }

    public DateOnly? Mes { get; init; }

    public decimal? Esperado { get; init; }

    public decimal? Guardado { get; init; }

    public long? Filas { get; init; }

    public DateOnly? FechaEsperada { get; init; }

    public DateOnly? FechaGuardada { get; init; }
}
