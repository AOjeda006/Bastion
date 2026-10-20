using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bastion.Inventario.Infrastructure.Persistencia.Existencias;

/// <summary>
/// Mueve el corte de las instantáneas mensuales de una empresa un mes cada vez, hacia delante o
/// hacia atrás, y con eso las recalcula desde el libro (ADR-0044, ADR-0067).
/// </summary>
/// <remarks>
/// <para>
/// <b>Es lo que hace de la instantánea una optimización y no una segunda verdad.</b> Si se puede
/// borrar y recalcular sin que cambie un número, la instantánea no dice nada que el libro no diga;
/// y si un día dijera otra cosa, recalcular la corrige. El caso que lo comprueba la borra, la
/// recalcula y compara.
/// </para>
/// <para>
/// <b>Un mes por transacción, desde el 2.18</b> (ADR-0067 §1). Las instantáneas de un mes son las
/// del mes anterior más el libro de ese mes, por clave, así que lo que cuesta un mes depende de
/// las claves y del libro de ese mes, y no de cuántos meses lleve la empresa. Hasta el 2.17 cada
/// avance rehacía todos los meses desde el libro entero en una sola sentencia, que crecía cada mes
/// y en una empresa de 10⁵ claves ya pasaba del plazo de la orden (ADR-0064 §4).
/// </para>
/// <para>
/// <b>La empresa la pone quien llama</b>, desde el 2.14 (ADR-0063 §7), y no el inquilino: el
/// trabajo que avanza el corte no tiene inquilino, y recorre las empresas una a una.
/// </para>
/// </remarks>
/// <param name="contexto">El contexto de Inventario, sin transacción abierta.</param>
internal sealed class LasInstantaneasMensuales(InventarioDbContext contexto)
{
    /// <summary>
    /// <b>El cerrojo que decide.</b> El exclusivo de las instantáneas de la empresa, con las dos
    /// claves de <see cref="ClaveDelCerrojoDeLasInstantaneas"/>: espera a toda confirmación de su
    /// empresa que ya haya pasado por el compartido, y no deja pasar a ninguna hasta el
    /// <c>COMMIT</c> del mes (ADR-0063 §5).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Hace falta aunque cada paso toque un solo mes de una sola empresa.</b> Sin él, una
    /// confirmación en vuelo de una clave nueva no se ve —su fila viva todavía no está
    /// confirmada—, así que el paso no la lleva al mes nuevo; y ella creó las suyas con el corte de
    /// antes. Al confirmarse las dos, a esa clave le falta el mes que el paso acaba de añadir. Y al
    /// revés: una confirmación que empieza durante el paso leería el corte viejo.
    /// </para>
    /// <para>
    /// <b>Se suelta entre un mes y el siguiente</b>, y es lo que cambió en el 2.18 (ADR-0067 §1):
    /// lo que una confirmación espera es un mes, no la puesta al día entera. Una confirmación que
    /// entra entre dos meses lee el corte del mes que acaba de confirmarse, y el mes siguiente ya la
    /// ve.
    /// </para>
    /// <para>
    /// <b>Es de la empresa y no de la tabla</b>, desde el 2.14. Hasta entonces era un
    /// <c>LOCK TABLE</c>, que paraba las confirmaciones de todas las empresas mientras una
    /// recalculaba. El de ahora no toca a las demás, y dos pasos de la misma empresa se esperan. Las
    /// lecturas no lo toman: el cuadre y quien consulte siguen leyendo.
    /// </para>
    /// <para>
    /// <b>No hay interbloqueo</b> porque se toma lo primero, sin nada cogido, y quien lo espera
    /// tampoco tiene nada que el paso necesite: las confirmaciones que llegan después se paran en el
    /// compartido antes de tocar una existencia o una instantánea. Lo que el paso toma luego es la
    /// fila de su corte, las instantáneas de su empresa y el <c>FOR KEY SHARE</c> de la clave ajena
    /// de cada una sobre su existencia (ADR-0063 §6). La fila del corte la toma también el cuadre
    /// al marcar un mes, en una sentencia suelta y sin nada más cogido: si se cruza con un paso,
    /// uno espera al otro, y nada más.
    /// </para>
    /// </remarks>
    internal const string SqlDelCerrojo = "SELECT pg_advisory_xact_lock({0}, {1})";

    /// <summary>El corte de la empresa <c>{0}</c>, si tiene: ninguna fila o una.</summary>
    internal const string SqlDelCorteGuardado =
        """
        SELECT c.hasta_el_mes AS "Value"
        FROM inventario.cortes_de_la_instantanea AS c
        WHERE c.empresa_id = {0}
        """;

    /// <summary>
    /// La fecha del primer movimiento de la empresa <c>{0}</c>, si tiene: ninguna fila o una.
    /// </summary>
    /// <remarks>
    /// <b>Solo se lee sin corte</b>, que es el primer paso de una empresa o el primero después de
    /// volver atrás del todo. Recorre el libro de la empresa: con 10⁶ movimientos cuesta 0,66 s
    /// (ADR-0064, <i>Medido</i>), y hacerlo cada mes no diría nada que el corte no diga.
    /// </remarks>
    internal const string SqlDelPrimerMovimiento =
        """
        SELECT min(m.fecha_de_operacion) AS "Value"
        FROM inventario.movimiento_stock AS m
        WHERE m.empresa_id = {0}
        HAVING count(*) > 0
        """;

    /// <summary>
    /// Las instantáneas del mes <c>{1}</c> de la empresa <c>{0}</c>: las del mes anterior,
    /// <c>{2}</c>, más el libro de su mes, que acaba antes del <c>{3}</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La instantánea de un mes es el saldo al acabar ese mes</b>, por clave, y la del mes
    /// anterior ya es el saldo al acabar el anterior: sumarle el libro del mes da la del mes. Una
    /// clave que no se ha movido arrastra su saldo, y una que se mueve por primera vez aparece. Así
    /// salen <b>sin huecos</b>, desde el primer mes en que la clave se movió hasta el corte, que es
    /// lo que necesita la sentencia que anota el libro: suma en todas las posteriores, y un mes que
    /// faltara no recibiría la suma.
    /// </para>
    /// <para>
    /// <b>Cada una cuelga de su fila viva</b>, que se busca por la clave entera, con el lote y la
    /// serie comparados con <c>IS NOT DISTINCT FROM</c> porque pueden ser nulos (ADR-0048 §6). Una
    /// clave del libro sin fila viva se queda sin instantánea, y no es un olvido: es un descuadre, y
    /// lo denuncia el cuadre, que compara cada mes contra el anterior y el libro, y no contra lo que
    /// el paso haya podido escribir.
    /// </para>
    /// <para>
    /// <b>Sin <c>ON CONFLICT</c></b>: antes del paso, el mes está por encima del corte, y ahí no
    /// escribe nadie. La sentencia que anota el libro llega hasta el corte y no pasa, y volver atrás
    /// borra el mes antes de bajar el corte. Una instantánea por encima del corte es un descuadre, y
    /// el paso que choca con ella falla con su clave duplicada en vez de pisarla: arreglarlo es
    /// recalcular, y eso lo decide quien recalcula.
    /// </para>
    /// <para>
    /// <b>Los meses van como parámetros</b>, y el libro se acota por su fecha por los dos lados:
    /// es lo que poda las particiones, y el paso solo lee la de su mes. Las instantáneas se leen por
    /// el índice de empresa y mes.
    /// </para>
    /// </remarks>
    internal const string SqlDelMes =
        """
        INSERT INTO inventario.instantaneas_mensuales (existencia_id, mes, empresa_id, fisico, valor)
        SELECT x.existencia_id, {1}, {0}, sum(x.fisico), sum(x.valor)
        FROM (
            SELECT i.existencia_id, i.fisico, i.valor
            FROM inventario.instantaneas_mensuales AS i
            WHERE i.empresa_id = {0} AND i.mes = {2}
            UNION ALL
            SELECT e.id, l.cantidad, l.valor
            FROM (
                SELECT m.articulo_id, m.almacen_id, m.ubicacion_id, m.lote_id, m.numero_de_serie_id,
                       sum(m.cantidad_en_unidad_base) AS cantidad, sum(m.valor) AS valor
                FROM inventario.movimiento_stock AS m
                WHERE m.empresa_id = {0} AND m.fecha_de_operacion >= {1} AND m.fecha_de_operacion < {3}
                GROUP BY m.articulo_id, m.almacen_id, m.ubicacion_id, m.lote_id, m.numero_de_serie_id
            ) AS l
            JOIN inventario.existencias AS e
                ON e.empresa_id = {0}
                AND e.articulo_id = l.articulo_id
                AND e.almacen_id = l.almacen_id
                AND e.ubicacion_id = l.ubicacion_id
                AND e.lote_id IS NOT DISTINCT FROM l.lote_id
                AND e.numero_de_serie_id IS NOT DISTINCT FROM l.numero_de_serie_id
        ) AS x
        GROUP BY x.existencia_id
        """;

    /// <summary>Pone el corte de la empresa <c>{0}</c> en el mes <c>{1}</c>.</summary>
    /// <remarks>
    /// Sin condición, porque el paso ya ha leído el corte con el exclusivo tomado y sabe que va un
    /// mes por delante. Lo cuadrado se queda como estaba: sigue sin pasar del corte, que sube.
    /// </remarks>
    internal const string SqlDelCorte =
        """
        INSERT INTO inventario.cortes_de_la_instantanea AS c (empresa_id, hasta_el_mes)
        VALUES ({0}, {1})
        ON CONFLICT (empresa_id) DO UPDATE SET hasta_el_mes = excluded.hasta_el_mes
        """;

    /// <summary>El último mes con instantánea de la empresa <c>{0}</c>, si tiene: ninguna fila o una.</summary>
    /// <remarks>Por el índice de empresa y mes, al revés: una sola entrada.</remarks>
    internal const string SqlDelUltimoMes =
        """
        SELECT i.mes AS "Value"
        FROM inventario.instantaneas_mensuales AS i
        WHERE i.empresa_id = {0}
        ORDER BY i.mes DESC
        LIMIT 1
        """;

    /// <summary>Tira las instantáneas del mes <c>{1}</c> de la empresa <c>{0}</c>.</summary>
    internal const string SqlDelBorradoDelMes =
        "DELETE FROM inventario.instantaneas_mensuales AS i WHERE i.empresa_id = {0} AND i.mes = {1}";

    /// <summary>
    /// Baja el corte de la empresa <c>{0}</c> al mes <c>{1}</c>, y deja su cuadre sin nada cuadrado.
    /// </summary>
    /// <remarks>
    /// <b>Lo cuadrado vuelve a nulo, y no al mes de antes</b>: volver atrás es el principio de un
    /// recálculo, y lo que el cuadre había comparado ya no es lo que va a haber (ADR-0067 §2).
    /// </remarks>
    internal const string SqlDeLaVueltaAtras =
        """
        UPDATE inventario.cortes_de_la_instantanea AS c
        SET hasta_el_mes = {1}, cuadrado_hasta = NULL
        WHERE c.empresa_id = {0}
        """;

    /// <summary>Quita el corte de la empresa <c>{0}</c>, cuando ya no le queda ninguna instantánea.</summary>
    internal const string SqlDelCorteQuitado =
        "DELETE FROM inventario.cortes_de_la_instantanea AS c WHERE c.empresa_id = {0}";

    /// <summary>Recalcula las instantáneas de una empresa hasta un mes.</summary>
    /// <remarks>
    /// <para>
    /// <b>Va mes a mes, también</b> (ADR-0067 §3): vuelve atrás un mes por transacción hasta que a
    /// la empresa no le queda ninguna instantánea, y avanza desde el primer mes de su libro hasta el
    /// que se le pide, otra vez un mes por transacción. Hasta el 2.17 era una sola sentencia, la
    /// misma del avance y con el mismo plazo, y en una empresa de 10⁵ claves no terminaba.
    /// </para>
    /// <para>
    /// <b>Entre un paso y el siguiente, las confirmaciones de la empresa entran.</b> Una que entra
    /// mientras vuelve atrás deja sus instantáneas hasta el corte de ese momento, y el paso
    /// siguiente las tira con su mes; una que entra mientras avanza las deja hasta el corte del mes
    /// recién hecho, y el siguiente las arrastra. Las dos acaban en el libro, que es de donde sale
    /// todo lo demás.
    /// </para>
    /// <para>
    /// <b>Pone el corte donde se le diga</b>, también por detrás del que había. El que no lo mueve si
    /// ya llega es <see cref="AvanzarUnMesAsync"/>.
    /// </para>
    /// </remarks>
    /// <param name="empresaId">La empresa que se recalcula.</param>
    /// <param name="hastaElMes">El primer día del último mes con instantánea.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>Cuántas instantáneas ha escrito al avanzar.</returns>
    /// <exception cref="ArgumentException">La empresa es el identificador vacío.</exception>
    /// <exception cref="ArgumentOutOfRangeException">El mes no es un primer día.</exception>
    /// <exception cref="InvalidOperationException">Ya hay una transacción abierta.</exception>
    internal async Task<int> RecalcularAsync(Guid empresaId, DateOnly hastaElMes, CancellationToken cancelacion)
    {
        ExigirLaEmpresaYElMes(empresaId, hastaElMes);

        while (await RetrocederUnMesAsync(empresaId, cancelacion).ConfigureAwait(false))
        {
            cancelacion.ThrowIfCancellationRequested();
        }

        int escritas = 0;

        while (await AvanzarUnMesAsync(empresaId, hastaElMes, cancelacion).ConfigureAwait(false) is { } mes)
        {
            escritas += mes.Escritas;
            cancelacion.ThrowIfCancellationRequested();
        }

        return escritas;
    }

    /// <summary>
    /// Avanza el corte de una empresa un mes hacia el que se le diga, si no llega ya, con las
    /// instantáneas de ese mes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Un solo mes, el del corte más uno, en su propia transacción</b> (ADR-0067 §1). Con el
    /// exclusivo de la empresa tomado lee el corte, escribe las instantáneas del mes siguiente,
    /// pone el corte en él y confirma. Quien quiera llegar más lejos lo llama otra vez: entre una
    /// llamada y la siguiente, las confirmaciones de la empresa entran.
    /// </para>
    /// <para>
    /// <b>Sin corte, el primer mes es el del primer movimiento</b>, que es donde empiezan las
    /// instantáneas. Si la empresa no tiene libro, o su primer movimiento cae después del mes que se
    /// pide, el corte se pone directamente en ese mes y no hay ninguna instantánea que escribir:
    /// todo lo que hay es posterior, y la sentencia que anota el libro no crea instantáneas por
    /// encima del corte.
    /// </para>
    /// <para>
    /// <b>Abre su propia transacción, y revienta si ya hay una.</b> El cerrojo tiene que ser lo
    /// primero que toma: todo lo que se lea después se lee con lo que las confirmaciones a las que
    /// ha esperado ya dejaron escrito. Dentro de la transacción de otro, lo leído antes del cerrojo
    /// podría estar viejo, y lo escrito quedaría a merced de un <c>COMMIT</c> que no es suyo.
    /// </para>
    /// <para>
    /// <b>Dos pasos a la vez se ponen en fila</b> en el exclusivo, y el segundo lee el corte que ha
    /// dejado el primero: avanza el mes siguiente, o no hace nada si ya se ha llegado.
    /// </para>
    /// </remarks>
    /// <param name="empresaId">La empresa cuyo corte se avanza.</param>
    /// <param name="hastaElMes">El primer día del mes al que tiene que llegar el corte.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>
    /// El mes en que ha puesto el corte y cuántas instantáneas ha escrito, o <c>null</c> si el corte
    /// ya llegaba y no ha tocado nada.
    /// </returns>
    /// <exception cref="ArgumentException">La empresa es el identificador vacío.</exception>
    /// <exception cref="ArgumentOutOfRangeException">El mes no es un primer día.</exception>
    /// <exception cref="InvalidOperationException">Ya hay una transacción abierta.</exception>
    internal async Task<MesDeLasInstantaneas?> AvanzarUnMesAsync(
        Guid empresaId, DateOnly hastaElMes, CancellationToken cancelacion)
    {
        ExigirLaEmpresaYElMes(empresaId, hastaElMes);

        await using IDbContextTransaction transaccion = await AbrirConElCerrojoAsync(empresaId, cancelacion)
            .ConfigureAwait(false);

        List<DateOnly> corte = await contexto.Database
            .SqlQueryRaw<DateOnly>(SqlDelCorteGuardado, empresaId)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        DateOnly mes;

        if (corte.Count == 0)
        {
            List<DateOnly> primero = await contexto.Database
                .SqlQueryRaw<DateOnly>(SqlDelPrimerMovimiento, empresaId)
                .ToListAsync(cancelacion)
                .ConfigureAwait(false);

            if (primero.Count == 0 || MesDe(primero[0]) > hastaElMes)
            {
                await PonerElCorteAsync(empresaId, hastaElMes, cancelacion).ConfigureAwait(false);
                await transaccion.CommitAsync(cancelacion).ConfigureAwait(false);

                return new MesDeLasInstantaneas(hastaElMes, 0);
            }

            mes = MesDe(primero[0]);
        }
        else if (corte[0] >= hastaElMes)
        {
            // El corte ya llegaba: el COMMIT suelta el exclusivo sin haber escrito nada.
            await transaccion.CommitAsync(cancelacion).ConfigureAwait(false);

            return null;
        }
        else
        {
            mes = corte[0].AddMonths(1);
        }

        int escritas = await contexto.Database
            .ExecuteSqlRawAsync(SqlDelMes, [empresaId, mes, mes.AddMonths(-1), mes.AddMonths(1)], cancelacion)
            .ConfigureAwait(false);

        await PonerElCorteAsync(empresaId, mes, cancelacion).ConfigureAwait(false);
        await transaccion.CommitAsync(cancelacion).ConfigureAwait(false);

        return new MesDeLasInstantaneas(mes, escritas);
    }

    /// <summary>
    /// Lleva el corte de una empresa un mes atrás: tira las instantáneas de su último mes y baja el
    /// corte al anterior.
    /// </summary>
    /// <remarks>
    /// <b>Cuando ya no le queda ninguna, quita el corte</b>, y el avance siguiente empieza por el
    /// primer mes del libro. Sin corte, la sentencia que anota el libro no crea instantáneas, así que
    /// lo que se confirme mientras tanto no deja nada que tirar.
    /// </remarks>
    /// <param name="empresaId">La empresa.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns><c>true</c> si ha tirado un mes, y <c>false</c> si ya no quedaba ninguno.</returns>
    private async Task<bool> RetrocederUnMesAsync(Guid empresaId, CancellationToken cancelacion)
    {
        await using IDbContextTransaction transaccion = await AbrirConElCerrojoAsync(empresaId, cancelacion)
            .ConfigureAwait(false);

        List<DateOnly> ultimo = await contexto.Database
            .SqlQueryRaw<DateOnly>(SqlDelUltimoMes, empresaId)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        if (ultimo.Count == 0)
        {
            await contexto.Database
                .ExecuteSqlRawAsync(SqlDelCorteQuitado, [empresaId], cancelacion)
                .ConfigureAwait(false);
            await transaccion.CommitAsync(cancelacion).ConfigureAwait(false);

            return false;
        }

        await contexto.Database
            .ExecuteSqlRawAsync(SqlDelBorradoDelMes, [empresaId, ultimo[0]], cancelacion)
            .ConfigureAwait(false);
        await contexto.Database
            .ExecuteSqlRawAsync(SqlDeLaVueltaAtras, [empresaId, ultimo[0].AddMonths(-1)], cancelacion)
            .ConfigureAwait(false);
        await transaccion.CommitAsync(cancelacion).ConfigureAwait(false);

        return true;
    }

    private async Task<IDbContextTransaction> AbrirConElCerrojoAsync(Guid empresaId, CancellationToken cancelacion)
    {
        if (contexto.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                "Cada paso de las instantáneas abre su propia transacción y toma el cerrojo lo " +
                "primero: dentro de otra, lo que ya se hubiera leído podría estar viejo.");
        }

        IDbContextTransaction transaccion = await contexto.Database
            .BeginTransactionAsync(cancelacion)
            .ConfigureAwait(false);

        try
        {
            await contexto.Database
                .ExecuteSqlRawAsync(
                    SqlDelCerrojo,
                    [ClaveDelCerrojoDeLasInstantaneas.Espacio, ClaveDelCerrojoDeLasInstantaneas.DeLaEmpresa(empresaId)],
                    cancelacion)
                .ConfigureAwait(false);
        }
        catch
        {
            await transaccion.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return transaccion;
    }

    private Task<int> PonerElCorteAsync(Guid empresaId, DateOnly mes, CancellationToken cancelacion) =>
        contexto.Database.ExecuteSqlRawAsync(SqlDelCorte, [empresaId, mes], cancelacion);

    private static DateOnly MesDe(DateOnly fecha) => new(fecha.Year, fecha.Month, 1);

    private static void ExigirLaEmpresaYElMes(Guid empresaId, DateOnly hastaElMes)
    {
        if (empresaId == Guid.Empty)
        {
            throw new ArgumentException(
                "Una instantánea es siempre de una empresa, y con el identificador vacío las " +
                "sentencias no tirarían ni escribirían nada de nadie.",
                nameof(empresaId));
        }

        if (hastaElMes.Day != 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hastaElMes),
                hastaElMes,
                "El corte de las instantáneas es un mes, y se escribe con su primer día.");
        }
    }
}

/// <summary>Lo que deja un paso del avance: dónde ha puesto el corte y cuánto ha escrito.</summary>
/// <param name="Mes">El primer día del mes en que ha quedado el corte.</param>
/// <param name="Escritas">Cuántas instantáneas de ese mes ha escrito.</param>
internal sealed record MesDeLasInstantaneas(DateOnly Mes, int Escritas);
