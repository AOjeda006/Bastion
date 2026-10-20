using System.Data;
using Bastion.BuildingBlocks.Domain.Fechas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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
/// <b>Cada mes contra el anterior, desde el 2.18</b> (ADR-0067 §6). La instantánea de un mes tiene
/// que ser la del mes anterior más el libro de su mes, por clave, que es lo mismo que la escribió.
/// Y las existencias y la valoración tienen que ser la instantánea del corte más el libro desde el
/// corte. Así ninguna sentencia lee el libro entero: hasta el 2.17 el cuadre lo comparaba todo
/// contra el libro desde el principio, y crecía igual que el avance (ADR-0064 §4).
/// </para>
/// <para>
/// <b>Dos formas de llamarlo.</b> <see cref="CuadrarAsync"/> lo compara todo de una vez, en una sola
/// lectura: es el de los casos y el de quien recalcula a mano. <see cref="CuadrarLoPendienteAsync"/>
/// compara un mes por sentencia, desde donde se quedó, y lo deja anotado en la fila del corte: es el
/// del trabajo mensual, y un cuadre que falla sigue en la vuelta siguiente.
/// </para>
/// <para>
/// <b>La valoración es la tercera copia desde el 2.8</b>, y se cuadra en sus tres columnas: la
/// cantidad contra la suma de las cantidades, el valor contra la suma de los valores, y la última
/// fecha contra la más alta de sus fechas de operación (ADR-0050 §4). El precio medio no se
/// cuadra porque no se guarda: se deduce de las dos primeras.
/// </para>
/// <para>
/// <b>El tránsito, desde el 2.11, se cuadra contra otra verdad</b> (ADR-0053 §1 y §11). No está en
/// el libro: es lo que sigue en vuelo, las líneas de las transferencias <c>Enviada</c>. Se compara
/// en la existencia del destino, por la clave entera, y en su valoración, en cantidad y en valor.
/// Dice cuántas claves comparó en cada una, y solo cuenta las que tienen algo en vuelo por un lado
/// o por el otro: con todo cuadrado y nada viajando, la cifra es cero, y quien llama sabe cuántas
/// tenía que ver.
/// </para>
/// <para>
/// <b>La empresa la pone quien llama</b>, desde el 2.14 (ADR-0063 §7), y no el inquilino: el
/// trabajo que cuadra después de avanzar el corte no tiene inquilino.
/// </para>
/// </remarks>
/// <param name="contexto">El contexto de Inventario.</param>
/// <param name="reloj">De dónde sale «hoy».</param>
internal sealed class ElCuadreDeLasExistencias(InventarioDbContext contexto, TimeProvider reloj)
{
    /// <summary>
    /// La cadena de las instantáneas de la empresa <c>{0}</c>, en los meses del <c>rango</c> que
    /// pone quien la usa: un fragmento de <c>WITH</c> que acaba en <c>cadena</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Cada instantánea de un mes se compara con la del mes anterior más el libro de su mes</b>,
    /// por clave y mes. Una que falte, una que sobre, una que diga otra cosa y dos para la misma
    /// clave y mes salen las cuatro: lo esperado viene del mes anterior y del libro, y lo guardado,
    /// de la tabla, con su cuenta de filas. Una clave se espera en un mes si tenía instantánea el
    /// mes anterior o se mueve en este, y así se esperan sin huecos, desde su primer movimiento
    /// hasta el corte.
    /// </para>
    /// <para>
    /// <b>Una instantánea mal descuadra su mes y el siguiente</b>, porque es lo esperado del
    /// siguiente. Y una que esté bien en un mes que se escribió mal no se ve aquí: se ve en el mes en
    /// que dejó de estar bien, que es donde hay que buscar.
    /// </para>
    /// <para>
    /// <b>Se junta con <c>UNION ALL</c> y se agrupa, en vez de cruzar con un <c>FULL JOIN</c></b>.
    /// El lote y el número de serie son nulos en casi todas las claves, y una igualdad con nulos no
    /// casa; con <c>IS NOT DISTINCT FROM</c> sí, pero entonces PostgreSQL no sabe hacer ese cruce. El
    /// <c>GROUP BY</c> trata los nulos como iguales, que es lo que la clave necesita.
    /// </para>
    /// <para>
    /// <b>En sus dos columnas</b>, cada una con su descuadre, como la valoración: la cantidad, con
    /// las filas, en «instantanea», y el valor en «instantanea-valor» (ADR-0063 §2).
    /// </para>
    /// <para>
    /// <b>Los límites del rango van en subconsultas escalares</b>, que el motor resuelve antes de
    /// leer: son los que podan las particiones del libro y acotan el índice de empresa y mes.
    /// </para>
    /// </remarks>
    internal const string LaCadena =
        """
        guardadas AS (
            SELECT e.articulo_id, e.almacen_id, e.ubicacion_id, e.lote_id, e.numero_de_serie_id, i.mes,
                   i.fisico, i.valor
            FROM inventario.instantaneas_mensuales AS i
            JOIN inventario.existencias AS e ON e.id = i.existencia_id
            WHERE i.empresa_id = {0} AND e.empresa_id = {0}
                AND i.mes >= (SELECT (r.desde - interval '1 month')::date FROM rango AS r)
                AND i.mes <= (SELECT r.hasta FROM rango AS r)
        ),
        cadena AS (
            SELECT x.articulo_id, x.almacen_id, x.ubicacion_id, x.lote_id, x.numero_de_serie_id, x.mes,
                   sum(x.esperado) AS esperado, sum(x.guardado) AS guardado,
                   sum(x.valor_esperado) AS valor_esperado, sum(x.valor_guardado) AS valor_guardado,
                   sum(x.filas) AS filas, max(x.debida) AS debida
            FROM (
                SELECT g.articulo_id, g.almacen_id, g.ubicacion_id, g.lote_id, g.numero_de_serie_id, g.mes,
                       0::numeric AS esperado, g.fisico AS guardado,
                       0::numeric AS valor_esperado, g.valor AS valor_guardado, 1 AS filas, 0 AS debida
                FROM guardadas AS g
                WHERE g.mes >= (SELECT r.desde FROM rango AS r)
                UNION ALL
                SELECT g.articulo_id, g.almacen_id, g.ubicacion_id, g.lote_id, g.numero_de_serie_id,
                       (g.mes + interval '1 month')::date, g.fisico, 0, g.valor, 0, 0, 1
                FROM guardadas AS g
                WHERE g.mes < (SELECT r.hasta FROM rango AS r)
                UNION ALL
                SELECT m.articulo_id, m.almacen_id, m.ubicacion_id, m.lote_id, m.numero_de_serie_id,
                       date_trunc('month', m.fecha_de_operacion::timestamp)::date,
                       m.cantidad_en_unidad_base, 0, m.valor, 0, 0, 1
                FROM inventario.movimiento_stock AS m
                WHERE m.empresa_id = {0}
                    AND m.fecha_de_operacion >= (SELECT r.desde FROM rango AS r)
                    AND m.fecha_de_operacion < (SELECT (r.hasta + interval '1 month')::date FROM rango AS r)
            ) AS x
            GROUP BY x.articulo_id, x.almacen_id, x.ubicacion_id, x.lote_id, x.numero_de_serie_id, x.mes
        )
        """;

    /// <summary>Lo que devuelve la cadena: su resumen y sus descuadres.</summary>
    internal const string LaSalidaDeLaCadena =
        """
        SELECT 'instantanea' AS que, true AS es_resumen, count(*) AS comparadas,
               NULL::uuid AS articulo_id, NULL::uuid AS almacen_id, NULL::uuid AS ubicacion_id,
               NULL::uuid AS lote_id, NULL::uuid AS numero_de_serie_id, NULL::date AS mes,
               NULL::numeric AS esperado, NULL::numeric AS guardado, NULL::bigint AS filas,
               NULL::date AS fecha_esperada, NULL::date AS fecha_guardada
        FROM cadena
        UNION ALL
        SELECT 'instantanea', false, 0, c.articulo_id, c.almacen_id, c.ubicacion_id, c.lote_id,
               c.numero_de_serie_id, c.mes, c.esperado, c.guardado, c.filas, NULL, NULL
        FROM cadena AS c
        WHERE c.esperado <> c.guardado OR c.filas <> c.debida
        UNION ALL
        SELECT 'instantanea-valor', false, 0, c.articulo_id, c.almacen_id, c.ubicacion_id, c.lote_id,
               c.numero_de_serie_id, c.mes, c.valor_esperado, c.valor_guardado, c.filas, NULL, NULL
        FROM cadena AS c
        WHERE c.valor_esperado <> c.valor_guardado
        """;

    /// <summary>
    /// Lo demás del cuadre de la empresa <c>{0}</c> el día <c>{1}</c>: las existencias, la
    /// valoración y las instantáneas de después del corte, contra el corte guardado; y el tránsito.
    /// Un fragmento de <c>WITH</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Las existencias y la valoración, contra la instantánea del corte más el libro desde el
    /// corte</b> (ADR-0067 §7). Lo de antes del corte ya está en la instantánea, y la cadena
    /// comprueba que lo esté. Sin corte, todo el libro. Una instantánea del corte que diga otra
    /// cosa descuadra también su existencia y su valoración, porque se comparan contra ella.
    /// </para>
    /// <para>
    /// <b>El libro hasta hoy, no entero.</b> Confirmar rechaza las fechas futuras, así que las dos
    /// cosas deberían ser la misma; si no lo son, una fila del libro con fecha de mañana hace que
    /// la existencia cuente algo que todavía no ha pasado, y eso también es un descuadre.
    /// </para>
    /// <para>
    /// <b>La fecha de la valoración no está en la instantánea</b>, así que no sale de ella. Si el par
    /// se ha movido desde el corte, la más alta está en lo leído. Si no, la guardada se da por buena
    /// cuando el libro tiene un movimiento del par ese día y ninguno después, hasta el corte: son dos
    /// búsquedas por el índice, cada una en las particiones de sus fechas. Solo cuando no se da por
    /// buena se busca la más alta, que es recorrer el par en todas las particiones, y es un
    /// descuadre.
    /// </para>
    /// <para>
    /// <b>Las instantáneas de después del corte no tendrían que existir</b>, porque nadie escribe ahí:
    /// cada una es un descuadre, con lo que guarda como lo guardado y cero como lo esperado. Sin
    /// corte, todas.
    /// </para>
    /// <para>
    /// <b>La valoración, por artículo y almacén y sin mirar la divisa.</b> Una clave que cambia de
    /// divisa lo hace vacía, y vaciarla se lleva todo el valor (ADR-0046 §5), así que las filas de
    /// la divisa de antes suman cero y la suma de todas es la de la divisa de ahora. Una valoración
    /// sin nada detrás cuadra solo si está a cero, igual que una fila viva.
    /// </para>
    /// <para>
    /// <b>La fecha se compara con <c>IS DISTINCT FROM</c></b>, porque las dos pueden ser nulas: la
    /// de una valoración que falta o que no la lleva, y la de un par sin filas en el libro. Con
    /// <c>&lt;&gt;</c>, una fecha contra un nulo no saldría, y es justo la de una valoración que
    /// falta. Es una copia que decide: el caso de uso rechaza un documento anterior a ella.
    /// </para>
    /// <para>
    /// <b>Lo que vuela se lee de las líneas</b>, que no llevan la clave entera: llevan el código del
    /// lote o de la serie, y la cantidad tecleada con su factor. El identificador sale de las tablas
    /// de los lotes y de las series, por empresa, artículo y código, y la cantidad en unidad base se
    /// redondea a seis decimales alejándose del cero, como <c>MovimientoStock.EnUnidadBase</c>. La
    /// línea no tiene empresa propia: es de la de su transferencia, y es esa la que se compara.
    /// </para>
    /// <para>
    /// <b>El valor en vuelo se compara con <c>IS DISTINCT FROM</c></b>: si ninguna línea en vuelo de
    /// la clave lleva valor y la valoración no tiene fila en tránsito, la suma esperada es nula, y con
    /// <c>&lt;&gt;</c> no saldría. Las filas sin nada en vuelo no entran en ninguno de los dos lados,
    /// así que una clave que no viaja no cuenta como comparada.
    /// </para>
    /// </remarks>
    internal const string ElResto =
        """
        corte AS (
            SELECT c.hasta_el_mes
            FROM inventario.cortes_de_la_instantanea AS c
            WHERE c.empresa_id = {0}
        ),
        desde_el_corte AS (
            SELECT coalesce(
                (SELECT (corte.hasta_el_mes + interval '1 month')::date FROM corte), '-infinity'::date) AS dia
        ),
        en_el_corte AS (
            SELECT e.articulo_id, e.almacen_id, e.ubicacion_id, e.lote_id, e.numero_de_serie_id, i.fisico, i.valor
            FROM inventario.instantaneas_mensuales AS i
            JOIN inventario.existencias AS e ON e.id = i.existencia_id
            WHERE i.empresa_id = {0} AND e.empresa_id = {0}
                AND i.mes = (SELECT corte.hasta_el_mes FROM corte)
        ),
        libro_desde_el_corte AS (
            SELECT m.articulo_id, m.almacen_id, m.ubicacion_id, m.lote_id, m.numero_de_serie_id,
                   m.cantidad_en_unidad_base AS cantidad, m.valor, m.fecha_de_operacion
            FROM inventario.movimiento_stock AS m
            WHERE m.empresa_id = {0}
                AND m.fecha_de_operacion >= (SELECT d.dia FROM desde_el_corte AS d)
                AND m.fecha_de_operacion <= {1}
        ),
        existencias_cuadradas AS (
            SELECT x.articulo_id, x.almacen_id, x.ubicacion_id, x.lote_id, x.numero_de_serie_id,
                   sum(x.esperado) AS esperado, sum(x.guardado) AS guardado, sum(x.filas) AS filas
            FROM (
                SELECT c.articulo_id, c.almacen_id, c.ubicacion_id, c.lote_id, c.numero_de_serie_id,
                       c.fisico AS esperado, 0::numeric AS guardado, 0 AS filas
                FROM en_el_corte AS c
                UNION ALL
                SELECT l.articulo_id, l.almacen_id, l.ubicacion_id, l.lote_id, l.numero_de_serie_id, l.cantidad, 0, 0
                FROM libro_desde_el_corte AS l
                UNION ALL
                SELECT e.articulo_id, e.almacen_id, e.ubicacion_id, e.lote_id, e.numero_de_serie_id, 0, e.fisico, 1
                FROM inventario.existencias AS e
                WHERE e.empresa_id = {0}
            ) AS x
            GROUP BY x.articulo_id, x.almacen_id, x.ubicacion_id, x.lote_id, x.numero_de_serie_id
        ),
        valoraciones_cuadradas AS (
            SELECT x.articulo_id, x.almacen_id,
                   sum(x.cantidad_esperada) AS cantidad_esperada,
                   sum(x.cantidad_guardada) AS cantidad_guardada,
                   sum(x.valor_esperado) AS valor_esperado, sum(x.valor_guardado) AS valor_guardado,
                   max(x.fecha_desde_el_corte) AS fecha_desde_el_corte, max(x.fecha_guardada) AS fecha_guardada,
                   sum(x.filas) AS filas
            FROM (
                SELECT c.articulo_id, c.almacen_id,
                       c.fisico AS cantidad_esperada, 0::numeric AS cantidad_guardada,
                       c.valor AS valor_esperado, 0::numeric AS valor_guardado,
                       NULL::date AS fecha_desde_el_corte, NULL::date AS fecha_guardada, 0 AS filas
                FROM en_el_corte AS c
                UNION ALL
                SELECT l.articulo_id, l.almacen_id, l.cantidad, 0, l.valor, 0, l.fecha_de_operacion, NULL, 0
                FROM libro_desde_el_corte AS l
                UNION ALL
                SELECT v.articulo_id, v.almacen_id, 0, v.cantidad, 0, v.valor, NULL, v.ultima_fecha, 1
                FROM inventario.valoraciones AS v
                WHERE v.empresa_id = {0}
            ) AS x
            GROUP BY x.articulo_id, x.almacen_id
        ),
        fechas_cuadradas AS (
            SELECT c.articulo_id, c.almacen_id, c.filas, c.fecha_guardada,
                   CASE
                       WHEN c.fecha_desde_el_corte IS NOT NULL THEN c.fecha_desde_el_corte
                       WHEN c.fecha_guardada <= {1}
                           AND c.fecha_guardada < (SELECT d.dia FROM desde_el_corte AS d)
                           AND EXISTS (
                               SELECT 1
                               FROM inventario.movimiento_stock AS m
                               WHERE m.empresa_id = {0}
                                   AND m.articulo_id = c.articulo_id
                                   AND m.almacen_id = c.almacen_id
                                   AND m.fecha_de_operacion = c.fecha_guardada)
                           AND NOT EXISTS (
                               SELECT 1
                               FROM inventario.movimiento_stock AS m
                               WHERE m.empresa_id = {0}
                                   AND m.articulo_id = c.articulo_id
                                   AND m.almacen_id = c.almacen_id
                                   AND m.fecha_de_operacion > c.fecha_guardada
                                   AND m.fecha_de_operacion < (SELECT d.dia FROM desde_el_corte AS d))
                           THEN c.fecha_guardada
                       ELSE (
                           SELECT max(m.fecha_de_operacion)
                           FROM inventario.movimiento_stock AS m
                           WHERE m.empresa_id = {0}
                               AND m.articulo_id = c.articulo_id
                               AND m.almacen_id = c.almacen_id
                               AND m.fecha_de_operacion < (SELECT d.dia FROM desde_el_corte AS d)
                               AND m.fecha_de_operacion <= {1})
                   END AS fecha_esperada
            FROM valoraciones_cuadradas AS c
        ),
        mas_alla_del_corte AS (
            SELECT e.articulo_id, e.almacen_id, e.ubicacion_id, e.lote_id, e.numero_de_serie_id, i.mes,
                   sum(i.fisico) AS guardado, sum(i.valor) AS valor_guardado, count(*) AS filas
            FROM inventario.instantaneas_mensuales AS i
            JOIN inventario.existencias AS e ON e.id = i.existencia_id
            WHERE i.empresa_id = {0} AND e.empresa_id = {0}
                AND i.mes >= (SELECT d.dia FROM desde_el_corte AS d)
            GROUP BY e.articulo_id, e.almacen_id, e.ubicacion_id, e.lote_id, e.numero_de_serie_id, i.mes
        ),
        en_vuelo AS (
            SELECT l.articulo_id, t.almacen_destino_id AS almacen_id, l.ubicacion_destino_id AS ubicacion_id,
                   lo.id AS lote_id, ns.id AS numero_de_serie_id,
                   round(l.cantidad_introducida * l.factor_a_unidad_base, 6) AS cantidad, l.valor
            FROM inventario.transferencias AS t
            JOIN inventario.lineas_transferencia AS l ON l.transferencia_id = t.id
            LEFT JOIN inventario.lotes AS lo
                   ON lo.empresa_id = {0} AND lo.articulo_id = l.articulo_id AND lo.codigo = l.codigo_de_lote
            LEFT JOIN inventario.numeros_de_serie AS ns
                   ON ns.empresa_id = {0} AND ns.articulo_id = l.articulo_id AND ns.numero = l.numero_de_serie
            WHERE t.empresa_id = {0} AND t.estado = 'Enviada'
        ),
        existencias_en_transito AS (
            SELECT x.articulo_id, x.almacen_id, x.ubicacion_id, x.lote_id, x.numero_de_serie_id,
                   sum(x.esperado) AS esperado, sum(x.guardado) AS guardado, sum(x.filas) AS filas
            FROM (
                SELECT v.articulo_id, v.almacen_id, v.ubicacion_id, v.lote_id, v.numero_de_serie_id,
                       v.cantidad AS esperado, 0::numeric AS guardado, 0 AS filas
                FROM en_vuelo AS v
                UNION ALL
                SELECT e.articulo_id, e.almacen_id, e.ubicacion_id, e.lote_id, e.numero_de_serie_id, 0,
                       e.en_transito, 1
                FROM inventario.existencias AS e
                WHERE e.empresa_id = {0} AND e.en_transito <> 0
            ) AS x
            GROUP BY x.articulo_id, x.almacen_id, x.ubicacion_id, x.lote_id, x.numero_de_serie_id
        ),
        valoraciones_en_transito AS (
            SELECT x.articulo_id, x.almacen_id,
                   sum(x.cantidad_esperada) AS cantidad_esperada,
                   sum(x.cantidad_guardada) AS cantidad_guardada,
                   sum(x.valor_esperado) AS valor_esperado, sum(x.valor_guardado) AS valor_guardado,
                   sum(x.filas) AS filas
            FROM (
                SELECT v.articulo_id, v.almacen_id,
                       v.cantidad AS cantidad_esperada, 0::numeric AS cantidad_guardada,
                       v.valor AS valor_esperado, 0::numeric AS valor_guardado, 0 AS filas
                FROM en_vuelo AS v
                UNION ALL
                SELECT w.articulo_id, w.almacen_id, 0, w.en_transito, 0, w.valor_en_transito, 1
                FROM inventario.valoraciones AS w
                WHERE w.empresa_id = {0} AND (w.en_transito <> 0 OR w.valor_en_transito <> 0)
            ) AS x
            GROUP BY x.articulo_id, x.almacen_id
        )
        """;

    /// <summary>
    /// Lo que devuelve lo demás: sus resúmenes y sus descuadres. Va siempre detrás de la cadena, que
    /// es la que nombra las columnas.
    /// </summary>
    internal const string LaSalidaDelResto =
        """
        SELECT 'existencia', true, count(*), NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL
        FROM existencias_cuadradas
        UNION ALL
        SELECT 'instantanea', true, count(*), NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL
        FROM mas_alla_del_corte
        UNION ALL
        SELECT 'valoracion', true, count(*), NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL
        FROM valoraciones_cuadradas
        UNION ALL
        SELECT 'existencia-en-transito', true, count(*), NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
               NULL, NULL, NULL
        FROM existencias_en_transito
        UNION ALL
        SELECT 'valoracion-en-transito', true, count(*), NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
               NULL, NULL, NULL
        FROM valoraciones_en_transito
        UNION ALL
        SELECT 'existencia', false, 0, c.articulo_id, c.almacen_id, c.ubicacion_id, c.lote_id,
               c.numero_de_serie_id, NULL, c.esperado, c.guardado, c.filas, NULL, NULL
        FROM existencias_cuadradas AS c
        WHERE c.esperado <> c.guardado OR c.filas <> 1
        UNION ALL
        SELECT 'instantanea', false, 0, c.articulo_id, c.almacen_id, c.ubicacion_id, c.lote_id,
               c.numero_de_serie_id, c.mes, 0, c.guardado, c.filas, NULL, NULL
        FROM mas_alla_del_corte AS c
        UNION ALL
        SELECT 'instantanea-valor', false, 0, c.articulo_id, c.almacen_id, c.ubicacion_id, c.lote_id,
               c.numero_de_serie_id, c.mes, 0, c.valor_guardado, c.filas, NULL, NULL
        FROM mas_alla_del_corte AS c
        WHERE c.valor_guardado <> 0
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
        FROM fechas_cuadradas AS c
        WHERE c.fecha_esperada IS DISTINCT FROM c.fecha_guardada
        UNION ALL
        SELECT 'existencia-en-transito', false, 0, c.articulo_id, c.almacen_id, c.ubicacion_id, c.lote_id,
               c.numero_de_serie_id, NULL, c.esperado, c.guardado, c.filas, NULL, NULL
        FROM existencias_en_transito AS c
        WHERE c.esperado <> c.guardado
        UNION ALL
        SELECT 'valoracion-en-transito-cantidad', false, 0, c.articulo_id, c.almacen_id, NULL, NULL, NULL,
               NULL, c.cantidad_esperada, c.cantidad_guardada, c.filas, NULL, NULL
        FROM valoraciones_en_transito AS c
        WHERE c.cantidad_esperada <> c.cantidad_guardada
        UNION ALL
        SELECT 'valoracion-en-transito-valor', false, 0, c.articulo_id, c.almacen_id, NULL, NULL, NULL,
               NULL, c.valor_esperado, c.valor_guardado, c.filas, NULL, NULL
        FROM valoraciones_en_transito AS c
        WHERE c.valor_esperado IS DISTINCT FROM c.valor_guardado
        """;

    /// <summary>
    /// El cuadre entero de la empresa <c>{0}</c> el día <c>{1}</c>: la cadena de todos los meses
    /// hasta el corte, y lo demás.
    /// </summary>
    /// <remarks>
    /// <b>Una sola lectura</b>, y no una por mes: el libro, las filas vivas, las instantáneas y la
    /// valoración se leen con la misma foto del motor, así que una confirmación que acabe a mitad no
    /// puede aparecer en una y no en la otra. Sin corte, la cadena sale vacía, y todo lo demás se
    /// compara contra el libro entero.
    /// </remarks>
    internal const string SqlDelCuadre =
        $$"""
        WITH rango AS (
            SELECT '-infinity'::date AS desde,
                   (SELECT c.hasta_el_mes FROM inventario.cortes_de_la_instantanea AS c WHERE c.empresa_id = {0})
                       AS hasta
        ),
        {{LaCadena}},
        {{ElResto}}
        {{LaSalidaDeLaCadena}}
        UNION ALL
        {{LaSalidaDelResto}}
        """;

    /// <summary>La cadena de un solo mes, el <c>{1}</c>, de la empresa <c>{0}</c>.</summary>
    internal const string SqlDeUnMes =
        $$"""
        WITH rango AS (SELECT {1}::date AS desde, {1}::date AS hasta),
        {{LaCadena}}
        {{LaSalidaDeLaCadena}}
        """;

    /// <summary>
    /// El mes del corte, el <c>{2}</c>, de la empresa <c>{0}</c> el día <c>{1}</c>: su cadena y lo
    /// demás, en la misma lectura.
    /// </summary>
    internal const string SqlDelMesDelCorte =
        $$"""
        WITH rango AS (SELECT {2}::date AS desde, {2}::date AS hasta),
        {{LaCadena}},
        {{ElResto}}
        {{LaSalidaDeLaCadena}}
        UNION ALL
        {{LaSalidaDelResto}}
        """;

    /// <summary>
    /// El corte de la empresa <c>{0}</c>, hasta dónde está cuadrado, y la versión de su fila.
    /// </summary>
    /// <remarks>
    /// <b>La versión es el <c>xmin</c> de la fila</b>, el mismo testigo que usa el resto del proyecto
    /// (<c>TestigoDeConcurrencia</c>): cambia cada vez que algo escribe la fila, y no tiene más
    /// significado que ese. Cada paso del avance y de la vuelta atrás escribe la fila del corte en
    /// la misma transacción que sus instantáneas, así que, si la versión no ha cambiado, tampoco lo
    /// ha hecho nada de lo que el cuadre acaba de comparar.
    /// </remarks>
    internal const string SqlDelCorteYSuVersion =
        """
        SELECT c.hasta_el_mes, c.cuadrado_hasta, c.xmin::text::bigint AS version
        FROM inventario.cortes_de_la_instantanea AS c
        WHERE c.empresa_id = {0}
        """;

    /// <summary>
    /// El primer mes por cuadrar de la empresa <c>{0}</c> cuando no tiene nada cuadrado: el de su
    /// primera instantánea o el de su primer movimiento, el que vaya antes, y nunca después del corte,
    /// <c>{1}</c>.
    /// </summary>
    /// <remarks>
    /// <b>También el del primer movimiento</b>, porque una clave del libro sin fila viva no tiene
    /// instantánea, y solo la ve la cadena del mes en que se movió. Buscarlo recorre el libro de la
    /// empresa, y solo pasa en el primer cuadre después de rehacer el corte.
    /// </remarks>
    internal const string SqlDelPrimerMesPorCuadrar =
        """
        SELECT least(
            coalesce(
                least(
                    (SELECT min(i.mes) FROM inventario.instantaneas_mensuales AS i WHERE i.empresa_id = {0}),
                    (SELECT date_trunc('month', min(m.fecha_de_operacion)::timestamp)::date
                     FROM inventario.movimiento_stock AS m
                     WHERE m.empresa_id = {0})),
                {1}),
            {1}) AS "Value"
        """;

    /// <summary>
    /// Anota que la empresa <c>{0}</c> está cuadrada hasta el mes <c>{1}</c>, si su corte sigue en la
    /// versión <c>{2}</c>.
    /// </summary>
    /// <remarks>
    /// <b>Con la versión que se leyó antes de comparar</b>: si un paso del avance o de la vuelta atrás
    /// ha escrito el corte entre medias, no toca ninguna fila, y el cuadre para. Lo que comparó era de
    /// antes, y la vuelta siguiente lo compara otra vez sobre lo que haya.
    /// </remarks>
    internal const string SqlDeLaMarca =
        """
        UPDATE inventario.cortes_de_la_instantanea AS c
        SET cuadrado_hasta = {1}
        WHERE c.empresa_id = {0} AND c.xmin::text::bigint = {2}
        """;

    /// <summary>Cuadra una empresa entera, de una vez.</summary>
    /// <remarks>
    /// No mira ni anota hasta dónde está cuadrada, y vale dentro de la transacción de quien lo llama:
    /// es una sola lectura.
    /// </remarks>
    /// <param name="empresaId">La empresa que se cuadra.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>Cuántas claves, instantáneas y valoraciones ha comparado, y lo que no cuadra.</returns>
    /// <exception cref="ArgumentException">La empresa es el identificador vacío.</exception>
    internal async Task<CuadreDeLasExistencias> CuadrarAsync(Guid empresaId, CancellationToken cancelacion)
    {
        ExigirLaEmpresa(empresaId);

        List<FilaDelCuadre> filas = await contexto.Database
            .SqlQueryRaw<FilaDelCuadre>(SqlDelCuadre, empresaId, DiaDeNegocio.Hoy(reloj))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return Cifras(filas);
    }

    /// <summary>
    /// Cuadra lo que le quede por cuadrar a una empresa, un mes por transacción, y lo anota en la fila
    /// de su corte.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Desde el mes siguiente al último cuadrado hasta el corte</b> (ADR-0067 §5). Cada mes de
    /// antes del corte compara su cadena; el del corte, su cadena y lo demás. Después de cada uno
    /// anota el mes. Si falla a mitad, lo anotado se queda, y la vuelta siguiente sigue desde ahí: el
    /// cuadre que fallaba después del <c>COMMIT</c> del avance no se repetía nunca (ADR-0064 §4).
    /// </para>
    /// <para>
    /// <b>Anota también lo que descuadra.</b> Lo que hace el cuadre es decirlo, y lo dice una vez:
    /// repetir cada hora el mismo aviso no lo arregla. Lo arregla recalcular.
    /// </para>
    /// <para>
    /// <b>Cada mes se lee en su propia transacción con <c>REPEATABLE READ</c></b>, que solo lee: la
    /// fila del corte primero y el mes después, con la misma foto: lo comparado es el corte que se
    /// leyó, y no uno que haya movido otro entre las dos sentencias. La marca va fuera, con la
    /// versión de esa fila (<see cref="SqlDeLaMarca"/>).
    /// </para>
    /// <para>
    /// <b>No toma el cerrojo de las instantáneas</b>: solo lee, y lo que lo protege de un paso del
    /// avance a la vez es la versión. Una confirmación que entra a mitad mueve a la vez su mes y los
    /// siguientes hasta el corte, y cada sentencia la ve entera o no la ve.
    /// </para>
    /// </remarks>
    /// <param name="empresaId">La empresa que se cuadra.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>
    /// Los meses que ha comparado y lo que ha encontrado, o <c>null</c> si no tenía nada pendiente.
    /// </returns>
    /// <exception cref="ArgumentException">La empresa es el identificador vacío.</exception>
    /// <exception cref="InvalidOperationException">Ya hay una transacción abierta.</exception>
    internal async Task<CuadreDeLoPendiente?> CuadrarLoPendienteAsync(Guid empresaId, CancellationToken cancelacion)
    {
        ExigirLaEmpresa(empresaId);

        if (contexto.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                "El cuadre de lo pendiente abre una transacción por mes para leerlo con una sola foto, " +
                "y anota cada mes fuera de ella: dentro de otra, la marca quedaría a merced de un " +
                "COMMIT que no es suyo.");
        }

        DateOnly hoy = DiaDeNegocio.Hoy(reloj);
        DateOnly? desde = null;
        DateOnly hasta = default;
        List<FilaDelCuadre> todas = [];

        while (await LeerElMesPendienteAsync(empresaId, hoy, cancelacion).ConfigureAwait(false) is { } leido)
        {
            desde ??= leido.Mes;
            hasta = leido.Mes;
            todas.AddRange(leido.Filas);

            int marcadas = await contexto.Database
                .ExecuteSqlRawAsync(SqlDeLaMarca, [empresaId, leido.Mes, leido.Version], cancelacion)
                .ConfigureAwait(false);

            if (marcadas == 0 || leido.EsElDelCorte)
            {
                break;
            }
        }

        return desde is { } primero ? new CuadreDeLoPendiente(primero, hasta, Cifras(todas)) : null;
    }

    private async Task<MesLeido?> LeerElMesPendienteAsync(Guid empresaId, DateOnly hoy, CancellationToken cancelacion)
    {
        await using IDbContextTransaction foto = await contexto.Database
            .BeginTransactionAsync(IsolationLevel.RepeatableRead, cancelacion)
            .ConfigureAwait(false);

        FilaDelCorte? corte = (await contexto.Database
                .SqlQueryRaw<FilaDelCorte>(SqlDelCorteYSuVersion, empresaId)
                .ToListAsync(cancelacion)
                .ConfigureAwait(false))
            .SingleOrDefault();

        if (corte is null || corte.CuadradoHasta == corte.HastaElMes)
        {
            await foto.CommitAsync(cancelacion).ConfigureAwait(false);

            return null;
        }

        DateOnly mes = corte.CuadradoHasta is { } cuadrado
            ? cuadrado.AddMonths(1)
            : (await contexto.Database
                    .SqlQueryRaw<DateOnly>(SqlDelPrimerMesPorCuadrar, empresaId, corte.HastaElMes)
                    .ToListAsync(cancelacion)
                    .ConfigureAwait(false))
                .Single();

        bool esElDelCorte = mes == corte.HastaElMes;

        List<FilaDelCuadre> filas = esElDelCorte
            ? await contexto.Database
                .SqlQueryRaw<FilaDelCuadre>(SqlDelMesDelCorte, empresaId, hoy, mes)
                .ToListAsync(cancelacion)
                .ConfigureAwait(false)
            : await contexto.Database
                .SqlQueryRaw<FilaDelCuadre>(SqlDeUnMes, empresaId, mes)
                .ToListAsync(cancelacion)
                .ConfigureAwait(false);

        await foto.CommitAsync(cancelacion).ConfigureAwait(false);

        return new MesLeido(mes, esElDelCorte, corte.Version, filas);
    }

    private static CuadreDeLasExistencias Cifras(List<FilaDelCuadre> filas) =>
        new(
            Comparadas(filas, Existencia),
            Comparadas(filas, Instantanea),
            Comparadas(filas, Valoracion),
            Comparadas(filas, ExistenciaEnTransito),
            Comparadas(filas, ValoracionEnTransito),
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

    // Las instantáneas tienen dos resúmenes en el cuadre entero, el de la cadena y el de después
    // del corte, y los meses del pendiente traen uno cada uno: se suman.
    private static long Comparadas(List<FilaDelCuadre> filas, string que) =>
        filas.Where(fila => fila.EsResumen && fila.Que == que).Sum(fila => fila.Comparadas);

    private static void ExigirLaEmpresa(Guid empresaId)
    {
        if (empresaId == Guid.Empty)
        {
            throw new ArgumentException(
                "Una existencia es siempre de una empresa, y con el identificador vacío el cuadre " +
                "no compararía nada y saldría limpio.",
                nameof(empresaId));
        }
    }

    private const string Existencia = "existencia";
    private const string Instantanea = "instantanea";
    private const string Valoracion = "valoracion";
    private const string ExistenciaEnTransito = "existencia-en-transito";
    private const string ValoracionEnTransito = "valoracion-en-transito";

    /// <summary>Un mes por cuadrar, ya leído, con la versión del corte con que se leyó.</summary>
    private sealed record MesLeido(DateOnly Mes, bool EsElDelCorte, long Version, List<FilaDelCuadre> Filas);
}

/// <summary>Lo que deja el cuadre de lo pendiente: de qué mes a qué mes, y lo que ha encontrado.</summary>
/// <param name="Desde">El primer día del primer mes comparado.</param>
/// <param name="Hasta">El primer día del último mes comparado.</param>
/// <param name="Cuadre">
/// Las cifras de todos esos meses juntos. Las existencias, la valoración y el tránsito solo se
/// comparan en el mes del corte: si el cuadre no ha llegado a él, son cero.
/// </param>
internal sealed record CuadreDeLoPendiente(DateOnly Desde, DateOnly Hasta, CuadreDeLasExistencias Cuadre);

/// <summary>Lo que devuelve el cuadre.</summary>
/// <param name="ExistenciasComparadas">Cuántas claves ha comparado entre el libro y las filas vivas.</param>
/// <param name="InstantaneasComparadas">Cuántos pares de clave y mes ha comparado.</param>
/// <param name="ValoracionesComparadas">Cuántos pares de artículo y almacén ha comparado.</param>
/// <param name="ExistenciasEnTransitoComparadas">
/// Cuántas claves con algo en vuelo, en las líneas enviadas o en la fila viva, ha comparado.
/// </param>
/// <param name="ValoracionesEnTransitoComparadas">
/// Cuántos pares de artículo y almacén con algo en vuelo, en las líneas enviadas o en la
/// valoración, ha comparado.
/// </param>
/// <param name="Descuadres">Lo que no cuadra; vacía si todo cuadra.</param>
internal sealed record CuadreDeLasExistencias(
    long ExistenciasComparadas,
    long InstantaneasComparadas,
    long ValoracionesComparadas,
    long ExistenciasEnTransitoComparadas,
    long ValoracionesEnTransitoComparadas,
    IReadOnlyList<Descuadre> Descuadres);

/// <summary>
/// Una clave —o una clave y un mes— en la que la copia no dice lo que el libro; o, en las del
/// tránsito, lo que las líneas de las transferencias enviadas.
/// </summary>
/// <param name="Que">
/// «existencia», «instantanea», «valoracion-cantidad», «valoracion-valor» o «valoracion-fecha»; las
/// tres últimas son las tres columnas de la valoración, cada una con su descuadre. Desde el 2.11,
/// también «existencia-en-transito», «valoracion-en-transito-cantidad» y
/// «valoracion-en-transito-valor», que comparan lo que vuela con las líneas enviadas. Y desde el
/// 2.14, «instantanea-valor», el valor de la instantánea.
/// </param>
/// <param name="ArticuloId">El artículo de la clave.</param>
/// <param name="AlmacenId">El almacén de la clave.</param>
/// <param name="UbicacionId">La ubicación de la clave; nula en la valoración, que no la lleva.</param>
/// <param name="LoteId">El lote de la clave, o nulo; nulo también en la valoración.</param>
/// <param name="NumeroDeSerieId">
/// El número de serie de la clave, o nulo; nulo también en la valoración.
/// </param>
/// <param name="Mes">El mes, en los dos de las instantáneas.</param>
/// <param name="Esperado">
/// Lo que tendría que guardar la copia; nulo en el de la fecha, que lleva fechas. En los de las
/// instantáneas, la del mes anterior más el libro de su mes, y cero después del corte. En los de
/// las existencias y la valoración, la instantánea del corte más el libro desde el corte. En los del
/// tránsito, la suma de las líneas en vuelo, y en el del valor puede ser nula: ninguna de ellas
/// lleva valor.
/// </param>
/// <param name="Guardado">Lo que dice la copia; nulo en el de la fecha.</param>
/// <param name="Filas">
/// Cuántas filas de la copia tiene la clave: tiene que ser una, o ninguna en una instantánea que no
/// se espera. En los del tránsito, cuántas tienen algo en vuelo, que puede ser ninguna.
/// </param>
/// <param name="FechaEsperada">
/// En el de la fecha, la fecha de operación más alta del libro hasta hoy, o nula si el par no tiene
/// filas en él; nula en los demás.
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

/// <summary>La fila del corte que lee el cuadre de lo pendiente, tal como llega.</summary>
/// <remarks>Se lee por los nombres de la convención del contexto, como <see cref="FilaDelCuadre"/>.</remarks>
internal sealed class FilaDelCorte
{
    public DateOnly HastaElMes { get; init; }

    public DateOnly? CuadradoHasta { get; init; }

    public long Version { get; init; }
}
