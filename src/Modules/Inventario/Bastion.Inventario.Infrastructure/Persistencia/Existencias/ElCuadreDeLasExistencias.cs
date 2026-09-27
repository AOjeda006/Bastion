using Bastion.BuildingBlocks.Application.Multiempresa;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Inventario.Infrastructure.Persistencia.Existencias;

/// <summary>
/// Recorre las existencias y las instantáneas de una empresa y las compara con el libro (R3,
/// ADR-0044).
/// </summary>
/// <remarks>
/// <para>
/// <b>Devuelve cuántas ha comparado, además de lo que no cuadra</b>, y es la mitad del criterio
/// del 2.7. Un cuadre que no encuentra nada que comparar sale limpio: si un día el filtro de la
/// empresa se torciera, o la consulta dejara de casar, la lista de descuadres vendría vacía y
/// parecería una buena noticia. Quien lo llama afirma que el conjunto comparado no era vacío.
/// </para>
/// <para>
/// <b>Una sola lectura</b>, y no una por tabla: el libro, las filas vivas y las instantáneas se
/// leen con la misma foto del motor, así que una confirmación que acabe a mitad no puede aparecer
/// en una y no en la otra.
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
    /// El lote es nulo en todas las claves de hoy, y una igualdad con nulos no casa; con
    /// <c>IS NOT DISTINCT FROM</c> sí, pero entonces PostgreSQL no sabe hacer ese cruce. El
    /// <c>GROUP BY</c> trata los nulos como iguales, que es lo que la clave necesita, y de paso
    /// cuenta cuántas filas vivas tiene cada clave: dos filas para la misma es un descuadre aunque
    /// sumen lo que deben.
    /// </para>
    /// <para>
    /// <b>Las instantáneas se comparan contra las debidas del fragmento del recálculo</b>, por
    /// clave y mes, así que una que falte, una que sobre y una que diga otra cosa salen las tres.
    /// </para>
    /// </remarks>
    internal const string SqlDelCuadre =
        $$"""
        WITH {{LasInstantaneasMensuales.LasDebidas}},
        existencias_cuadradas AS (
            SELECT x.articulo_id, x.almacen_id, x.ubicacion_id, x.lote_id,
                   sum(x.esperado) AS esperado, sum(x.guardado) AS guardado, sum(x.filas) AS filas
            FROM (
                SELECT m.articulo_id, m.almacen_id, m.ubicacion_id, NULL::uuid AS lote_id,
                       m.cantidad_en_unidad_base AS esperado, 0::numeric AS guardado, 0 AS filas
                FROM inventario.movimiento_stock AS m
                WHERE m.empresa_id = {0} AND m.fecha_de_operacion <= {1}
                UNION ALL
                SELECT e.articulo_id, e.almacen_id, e.ubicacion_id, e.lote_id, 0, e.fisico, 1
                FROM inventario.existencias AS e
                WHERE e.empresa_id = {0}
            ) AS x
            GROUP BY x.articulo_id, x.almacen_id, x.ubicacion_id, x.lote_id
        ),
        instantaneas_cuadradas AS (
            SELECT x.articulo_id, x.almacen_id, x.ubicacion_id, x.lote_id, x.mes,
                   sum(x.esperado) AS esperado, sum(x.guardado) AS guardado, sum(x.filas) AS filas
            FROM (
                SELECT d.articulo_id, d.almacen_id, d.ubicacion_id, NULL::uuid AS lote_id, d.mes,
                       d.fisico AS esperado, 0::numeric AS guardado, 0 AS filas
                FROM debidas AS d
                UNION ALL
                SELECT e.articulo_id, e.almacen_id, e.ubicacion_id, e.lote_id, i.mes, 0, i.fisico, 1
                FROM inventario.instantaneas_mensuales AS i
                JOIN inventario.existencias AS e ON e.id = i.existencia_id
                WHERE i.empresa_id = {0} AND e.empresa_id = {0}
            ) AS x
            GROUP BY x.articulo_id, x.almacen_id, x.ubicacion_id, x.lote_id, x.mes
        )
        SELECT 'existencia' AS que, true AS es_resumen, count(*) AS comparadas,
               NULL::uuid AS articulo_id, NULL::uuid AS almacen_id, NULL::uuid AS ubicacion_id,
               NULL::uuid AS lote_id, NULL::date AS mes,
               NULL::numeric AS esperado, NULL::numeric AS guardado, NULL::bigint AS filas
        FROM existencias_cuadradas
        UNION ALL
        SELECT 'instantanea', true, count(*), NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL
        FROM instantaneas_cuadradas
        UNION ALL
        SELECT 'existencia', false, 0, c.articulo_id, c.almacen_id, c.ubicacion_id, c.lote_id,
               NULL, c.esperado, c.guardado, c.filas
        FROM existencias_cuadradas AS c
        WHERE c.esperado <> c.guardado OR c.filas <> 1
        UNION ALL
        SELECT 'instantanea', false, 0, c.articulo_id, c.almacen_id, c.ubicacion_id, c.lote_id,
               c.mes, c.esperado, c.guardado, c.filas
        FROM instantaneas_cuadradas AS c
        WHERE c.esperado <> c.guardado OR c.filas <> 1
        """;

    /// <summary>Cuadra la empresa del inquilino.</summary>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>Cuántas claves y cuántas instantáneas ha comparado, y lo que no cuadra.</returns>
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
            [.. filas
                .Where(fila => !fila.EsResumen)
                .Select(fila => new Descuadre(
                    fila.Que,
                    fila.ArticuloId!.Value,
                    fila.AlmacenId!.Value,
                    fila.UbicacionId!.Value,
                    fila.LoteId,
                    fila.Mes,
                    fila.Esperado!.Value,
                    fila.Guardado!.Value,
                    fila.Filas!.Value))]);
    }

    private const string Existencia = "existencia";
    private const string Instantanea = "instantanea";
}

/// <summary>Lo que devuelve el cuadre.</summary>
/// <param name="ExistenciasComparadas">Cuántas claves ha comparado entre el libro y las filas vivas.</param>
/// <param name="InstantaneasComparadas">Cuántos pares de clave y mes ha comparado.</param>
/// <param name="Descuadres">Lo que no cuadra; vacía si todo cuadra.</param>
internal sealed record CuadreDeLasExistencias(
    long ExistenciasComparadas,
    long InstantaneasComparadas,
    IReadOnlyList<Descuadre> Descuadres);

/// <summary>Una clave —o una clave y un mes— en la que la copia no dice lo que el libro.</summary>
/// <param name="Que">«existencia» o «instantanea».</param>
/// <param name="ArticuloId">El artículo de la clave.</param>
/// <param name="AlmacenId">El almacén de la clave.</param>
/// <param name="UbicacionId">La ubicación de la clave.</param>
/// <param name="LoteId">El lote de la clave, nulo mientras el libro no lo lleve.</param>
/// <param name="Mes">El mes, en las instantáneas.</param>
/// <param name="Esperado">Lo que dice el libro.</param>
/// <param name="Guardado">Lo que dice la copia.</param>
/// <param name="Filas">Cuántas filas de la copia tiene la clave: tiene que ser una.</param>
internal sealed record Descuadre(
    string Que,
    Guid ArticuloId,
    Guid AlmacenId,
    Guid UbicacionId,
    Guid? LoteId,
    DateOnly? Mes,
    decimal Esperado,
    decimal Guardado,
    long Filas);

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

    public DateOnly? Mes { get; init; }

    public decimal? Esperado { get; init; }

    public decimal? Guardado { get; init; }

    public long? Filas { get; init; }
}
