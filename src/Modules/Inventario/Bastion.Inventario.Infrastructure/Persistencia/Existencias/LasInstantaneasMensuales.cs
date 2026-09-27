using Bastion.BuildingBlocks.Application.Multiempresa;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bastion.Inventario.Infrastructure.Persistencia.Existencias;

/// <summary>
/// Tira las instantáneas mensuales de una empresa y las vuelve a sacar del libro, hasta el mes
/// que se le diga (ADR-0044).
/// </summary>
/// <remarks>
/// <para>
/// <b>Es lo que hace de la instantánea una optimización y no una segunda verdad.</b> Si se puede
/// borrar y recalcular sin que cambie un número, la instantánea no dice nada que el libro no diga;
/// y si un día dijera otra cosa, recalcular la corrige. El caso que lo comprueba la borra, la
/// recalcula y compara.
/// </para>
/// <para>
/// <b>En el 2.7 no tiene quien la llame en producción, y es a propósito.</b> Quien avanza el corte
/// cada mes —y cuadra después— llega con el primer lector de la instantánea, el ítem 2.14. Hasta
/// entonces la llaman los casos, que es lo que el criterio del 2.7 pide.
/// </para>
/// </remarks>
/// <param name="contexto">El contexto de Inventario, sin transacción abierta.</param>
/// <param name="inquilino">De donde sale la empresa que se recalcula.</param>
internal sealed class LasInstantaneasMensuales(InventarioDbContext contexto, IInquilinoActual inquilino)
{
    /// <summary>
    /// Las instantáneas que el libro dice que tiene que haber, para la empresa <c>{0}</c> y hasta
    /// su corte guardado: un fragmento de <c>WITH</c> que acaba en <c>debidas</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Lo comparten el recálculo y el cuadre, y es la razón de que sea un fragmento.</b> Si cada
    /// uno escribiera el suyo, el cuadre compararía la instantánea contra su propia idea de lo que
    /// tiene que haber, y dos ideas distintas darían un cuadre limpio sobre una instantánea mal
    /// recalculada.
    /// </para>
    /// <para>
    /// <b>La instantánea de un mes es el saldo al acabar ese mes</b>: la suma del libro con fecha
    /// anterior al primer día del mes siguiente. Hay una por clave y por mes, <b>sin huecos</b>,
    /// desde el primer mes en que la clave se movió hasta el corte. Sin huecos porque la sentencia
    /// que anota el libro suma en todas las posteriores: un mes que faltara no recibiría la suma, y
    /// al crearlo después no se sabría cuánto le tocaba sin volver al libro.
    /// </para>
    /// <para>
    /// <b>Sin corte, ninguna</b>: <c>corte</c> sale vacío y todo lo que se cruza con él, también.
    /// </para>
    /// <para>
    /// El mes se trunca desde <c>timestamp</c> y no desde <c>date</c>: con una fecha a secas,
    /// PostgreSQL elige la versión de <c>date_trunc</c> con zona horaria.
    /// </para>
    /// </remarks>
    internal const string LasDebidas =
        """
        corte AS (
            SELECT c.hasta_el_mes
            FROM inventario.cortes_de_la_instantanea AS c
            WHERE c.empresa_id = {0}
        ),
        por_mes AS (
            SELECT m.articulo_id, m.almacen_id, m.ubicacion_id,
                   date_trunc('month', m.fecha_de_operacion::timestamp)::date AS mes,
                   sum(m.cantidad_en_unidad_base) AS cantidad
            FROM inventario.movimiento_stock AS m
            CROSS JOIN corte
            WHERE m.empresa_id = {0}
                AND m.fecha_de_operacion < corte.hasta_el_mes + interval '1 month'
            GROUP BY m.articulo_id, m.almacen_id, m.ubicacion_id,
                     date_trunc('month', m.fecha_de_operacion::timestamp)
        ),
        rejilla AS (
            SELECT p.articulo_id, p.almacen_id, p.ubicacion_id, g.mes::date AS mes
            FROM (
                SELECT pm.articulo_id, pm.almacen_id, pm.ubicacion_id, min(pm.mes) AS primero
                FROM por_mes AS pm
                GROUP BY pm.articulo_id, pm.almacen_id, pm.ubicacion_id
            ) AS p
            CROSS JOIN corte
            CROSS JOIN LATERAL generate_series(
                p.primero::timestamp, corte.hasta_el_mes::timestamp, interval '1 month') AS g (mes)
        ),
        debidas AS (
            SELECT r.articulo_id, r.almacen_id, r.ubicacion_id, r.mes,
                   sum(coalesce(pm.cantidad, 0)) OVER (
                       PARTITION BY r.articulo_id, r.almacen_id, r.ubicacion_id
                       ORDER BY r.mes) AS fisico
            FROM rejilla AS r
            LEFT JOIN por_mes AS pm
                ON pm.articulo_id = r.articulo_id
                AND pm.almacen_id = r.almacen_id
                AND pm.ubicacion_id = r.ubicacion_id
                AND pm.mes = r.mes
        )
        """;

    /// <summary>
    /// <b>El cerrojo que decide.</b> Espera a toda confirmación que ya haya movido la proyección y
    /// no deja empezar a ninguna hasta el <c>COMMIT</c> del recálculo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Hace falta aunque el recálculo tire y reponga lo de una sola empresa.</b> Sin él, una
    /// confirmación en vuelo de una clave nueva no se ve —su fila viva todavía no está
    /// confirmada—, así que el recálculo no le crea instantáneas; y ella las creó con el corte de
    /// antes. Al confirmarse las dos, a esa clave le faltan los meses que el recálculo acaba de
    /// añadir. Y al revés: una confirmación que empieza durante el recálculo leería el corte viejo.
    /// </para>
    /// <para>
    /// <b>El modo es el más débil que choca con el de quien escribe</b>. La sentencia que anota el
    /// libro toma <c>ROW EXCLUSIVE</c> sobre la tabla —aunque no llegue a escribir ninguna
    /// instantánea: lo toma al analizarse— y lo suelta en su <c>COMMIT</c>. <c>SHARE ROW
    /// EXCLUSIVE</c> choca con él y consigo mismo —dos recálculos se esperan—, pero no con las
    /// lecturas: el cuadre y quien consulte siguen leyendo mientras tanto.
    /// </para>
    /// <para>
    /// <b>No hay interbloqueo</b> porque el recálculo no necesita nada que una confirmación tenga
    /// cogido: lee el libro y las filas vivas sin cerrojo, y la única fila que escribe fuera de
    /// esta tabla —la del corte— la confirmación solo la lee.
    /// </para>
    /// </remarks>
    internal const string SqlDelCerrojo =
        "LOCK TABLE inventario.instantaneas_mensuales IN SHARE ROW EXCLUSIVE MODE";

    /// <summary>Pone el corte de la empresa <c>{0}</c> en el mes <c>{1}</c>.</summary>
    internal const string SqlDelCorte =
        """
        INSERT INTO inventario.cortes_de_la_instantanea AS c (empresa_id, hasta_el_mes)
        VALUES ({0}, {1})
        ON CONFLICT (empresa_id) DO UPDATE SET hasta_el_mes = excluded.hasta_el_mes
        """;

    /// <summary>Tira las instantáneas de la empresa <c>{0}</c>.</summary>
    internal const string SqlDelBorrado =
        "DELETE FROM inventario.instantaneas_mensuales AS i WHERE i.empresa_id = {0}";

    /// <summary>Repone las instantáneas de la empresa <c>{0}</c> desde el libro.</summary>
    /// <remarks>
    /// <b>Cada una cuelga de su fila viva</b>, que se busca por la clave con el lote nulo: el libro
    /// todavía no lleva lote (lo trae el 2.9). Una clave del libro sin fila viva se queda sin
    /// instantáneas, y no es un olvido: es un descuadre, y lo denuncia el cuadre, que compara
    /// contra las debidas y no contra lo que el recálculo haya podido escribir.
    /// </remarks>
    internal const string SqlDeLaReposicion =
        $$"""
        WITH {{LasDebidas}}
        INSERT INTO inventario.instantaneas_mensuales (existencia_id, mes, empresa_id, fisico)
        SELECT e.id, d.mes, {0}, d.fisico
        FROM debidas AS d
        JOIN inventario.existencias AS e
            ON e.empresa_id = {0}
            AND e.articulo_id = d.articulo_id
            AND e.almacen_id = d.almacen_id
            AND e.ubicacion_id = d.ubicacion_id
            AND e.lote_id IS NULL
        """;

    /// <summary>Recalcula las instantáneas de la empresa del inquilino hasta un mes.</summary>
    /// <remarks>
    /// <para>
    /// <b>Abre su propia transacción, y revienta si ya hay una.</b> El cerrojo tiene que ser lo
    /// primero que toma: todo lo que se lea después se lee con lo que las confirmaciones a las que
    /// ha esperado ya dejaron escrito. Dentro de la transacción de otro, lo leído antes del cerrojo
    /// podría estar viejo, y lo escrito quedaría a merced de un <c>COMMIT</c> que no es suyo.
    /// </para>
    /// <para>
    /// <b>El corte va primero y el borrado después</b>, y el orden solo importa para que la
    /// reposición lea el corte nuevo de la tabla, con el mismo fragmento que el cuadre.
    /// </para>
    /// </remarks>
    /// <param name="hastaElMes">El primer día del último mes con instantánea.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>Cuántas instantáneas ha repuesto.</returns>
    /// <exception cref="ArgumentOutOfRangeException">El mes no es un primer día.</exception>
    /// <exception cref="InvalidOperationException">
    /// Ya hay una transacción abierta, o no hay empresa en el inquilino.
    /// </exception>
    internal async Task<int> RecalcularAsync(DateOnly hastaElMes, CancellationToken cancelacion)
    {
        if (hastaElMes.Day != 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hastaElMes),
                hastaElMes,
                "El corte de las instantáneas es un mes, y se escribe con su primer día.");
        }

        if (contexto.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                "El recálculo de las instantáneas abre su propia transacción y toma el cerrojo " +
                "lo primero: dentro de otra, lo que ya se hubiera leído podría estar viejo.");
        }

        Guid empresaId = inquilino.EmpresaDelFiltro ?? throw new InvalidOperationException(
            "Se están recalculando las instantáneas dentro de un ámbito sin inquilino, y una " +
            "instantánea es siempre de una empresa: sin ella las sentencias tirarían las de " +
            "cualquiera.");

        await using IDbContextTransaction transaccion = await contexto.Database
            .BeginTransactionAsync(cancelacion)
            .ConfigureAwait(false);

        await contexto.Database
            .ExecuteSqlRawAsync(SqlDelCerrojo, cancelacion)
            .ConfigureAwait(false);

        await contexto.Database
            .ExecuteSqlRawAsync(SqlDelCorte, [empresaId, hastaElMes], cancelacion)
            .ConfigureAwait(false);

        await contexto.Database
            .ExecuteSqlRawAsync(SqlDelBorrado, [empresaId], cancelacion)
            .ConfigureAwait(false);

        int repuestas = await contexto.Database
            .ExecuteSqlRawAsync(SqlDeLaReposicion, [empresaId], cancelacion)
            .ConfigureAwait(false);

        await transaccion.CommitAsync(cancelacion).ConfigureAwait(false);

        return repuestas;
    }
}
