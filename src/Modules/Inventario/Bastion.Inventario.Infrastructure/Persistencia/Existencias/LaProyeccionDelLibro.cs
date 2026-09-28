using Bastion.Inventario.Domain.Movimientos;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Inventario.Infrastructure.Persistencia.Existencias;

/// <summary>
/// Las sentencias que mueven la proyección cuando se anota el libro: la fila viva de cada
/// existencia y sus instantáneas mensuales, a la vez (ADR-0044), sin bajar de cero (ADR-0046 §4).
/// </summary>
/// <remarks>
/// <para>
/// <b>La fila viva y las instantáneas se mueven en una sola sentencia</b>, por la decisión (a) de
/// la puerta del 2.7: juntas o no se mueven. Antes va otra, que no mueve nada: crea a cero las filas
/// vivas que falten y bloquea todas las del documento, en orden de clave.
/// </para>
/// <para>
/// <b>Son dos desde el ítem 2.8, y lo decide el <c>CHECK</c> del stock.</b> PostgreSQL comprueba las
/// restricciones de un <c>INSERT … ON CONFLICT DO UPDATE</c> sobre la fila <b>propuesta</b>, antes
/// de mirar si choca. La sentencia única proponía <c>fisico = Δ</c>, así que toda salida chocaba
/// contra <c>fisico &gt;= 0</c> aunque la fila existiera y el saldo quedara en positivo: de 5, sacar
/// 2 se rechazaba. Con la fila ya creada, la suma es un <c>UPDATE</c>, y la restricción mira la fila
/// ya sumada, que es la que tiene que mirar. Es la misma forma que la valoración (ADR-0046 §2): una
/// sentencia bloquea y crea, y la siguiente suma sobre lo bloqueado.
/// </para>
/// <para>
/// <b>La aplicación nunca lee lo que suma.</b> Las dos escrituras suman sobre la fila que hay en el
/// motor, con la fila bloqueada. Leer el saldo, sumarle y escribirlo dejaría una ventana entre la
/// lectura y la escritura, y dos confirmaciones simultáneas del mismo artículo se llevarían el mismo
/// saldo de partida: una de las dos cantidades se perdería, sin error.
/// </para>
/// <para>
/// <b>Las claves se bloquean ordenadas</b>, y eso es lo que impide el interbloqueo. Dos
/// confirmaciones que tocan los mismos dos artículos los bloquean en el mismo orden, así que la
/// segunda espera a la primera en vez de quedarse cada una con la fila que la otra necesita. La
/// sentencia que suma ya no necesita orden: solo toca filas que esta transacción tiene bloqueadas.
/// Las instantáneas tampoco: cada una cuelga de una sola existencia, y quien la toca ya tiene
/// bloqueada la fila viva de esa existencia.
/// </para>
/// <para>
/// <b>Y las cantidades van agrupadas</b>, por clave en la fila viva y por clave y mes en las
/// instantáneas. Un documento con dos líneas del mismo artículo y la misma ubicación daría dos
/// filas para el mismo <c>ON CONFLICT</c>, y PostgreSQL rechaza actualizar dos veces la misma fila
/// en una sentencia.
/// </para>
/// <para>
/// <b>El lote va nulo, escrito.</b> El libro todavía no tiene columna de lote: la trae el 2.9, y con
/// ella esta sentencia, el recálculo y el cuadre cambian a la vez.
/// </para>
/// </remarks>
internal static class LaProyeccionDelLibro
{
    /// <summary>
    /// Crea a cero la fila viva de cada clave nueva y bloquea la de todas, en orden de clave.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El <c>DO UPDATE</c> que no cambia nada es el cerrojo</b>: bloquea la fila como un
    /// <c>FOR UPDATE</c>, y además bloquea la clave que todavía no existe. Dos primeras entradas
    /// simultáneas de un artículo no encontrarían nada que bloquear con un <c>FOR UPDATE</c>; aquí la
    /// segunda choca con la fila sin confirmar de la primera y espera a que confirme.
    /// </para>
    /// <para>
    /// <b>Propone cero, que cumple el <c>CHECK</c></b> tanto si la fila entra como si choca. Un
    /// identificador por clave: las filas del libro de la misma clave caen en el mismo grupo.
    /// </para>
    /// </remarks>
    internal const string SqlQueCreaYBloqueaLasVivas =
        """
        INSERT INTO inventario.existencias AS e
            (id, empresa_id, articulo_id, almacen_id, ubicacion_id, lote_id, fisico, reservado)
        SELECT c.id, {0}, c.articulo_id, c.almacen_id, c.ubicacion_id, NULL::uuid, 0, 0
        FROM unnest({1}::uuid[], {2}::uuid[], {3}::uuid[], {4}::uuid[])
            AS c (id, articulo_id, almacen_id, ubicacion_id)
        GROUP BY c.id, c.articulo_id, c.almacen_id, c.ubicacion_id
        ORDER BY c.articulo_id, c.almacen_id, c.ubicacion_id
        ON CONFLICT (empresa_id, articulo_id, almacen_id, ubicacion_id, lote_id)
        DO UPDATE SET fisico = e.fisico
        """;

    /// <summary>
    /// <b>La sentencia que decide.</b> Suma cada clave en su fila viva y cada mes en su instantánea,
    /// desde el mes del movimiento hasta el corte de la empresa, creando las instantáneas que falten.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La primera escritura va en un <c>WITH</c></b> porque la segunda necesita el identificador
    /// de cada fila viva. <c>RETURNING</c> lo devuelve.
    /// </para>
    /// <para>
    /// <b>Es un <c>UPDATE</c>, y ahí está la guarda del stock.</b> La restricción
    /// <c>ck_existencias_fisico_no_negativo</c> se evalúa sobre la fila ya sumada, con la fila ya
    /// bloqueada por la sentencia anterior, y si queda por debajo de cero la sentencia entera se
    /// rechaza: ni la fila viva ni las instantáneas se mueven. La fila tiene que existir, y existe
    /// porque la crea la sentencia anterior; sin ella, esta no tocaría nada y las filas del libro
    /// entrarían sin su suma. Por eso las dos solo se ejecutan juntas, desde <see cref="MoverAsync"/>.
    /// </para>
    /// <para>
    /// <b>Sin corte no hay instantáneas</b>: el límite superior de <c>generate_series</c> sale nulo
    /// y la serie, vacía. Y un movimiento de un mes posterior al corte tampoco escribe ninguna, por
    /// lo mismo. Las dos cosas son correctas: esos meses no se han materializado todavía, y quien
    /// los materialice leerá el libro.
    /// </para>
    /// <para>
    /// <b>Sin punto y coma final</b>, como las demás cadenas crudas del proyecto.
    /// </para>
    /// </remarks>
    internal const string SqlQueMueveLaProyeccion =
        """
        WITH movimientos AS (
            SELECT m.articulo_id, m.almacen_id, m.ubicacion_id, m.mes, m.cantidad
            FROM unnest({1}::uuid[], {2}::uuid[], {3}::uuid[], {4}::date[], {5}::numeric[])
                AS m (articulo_id, almacen_id, ubicacion_id, mes, cantidad)
        ),
        vivas AS (
            UPDATE inventario.existencias AS e
            SET fisico = e.fisico + d.cantidad
            FROM (
                SELECT m.articulo_id, m.almacen_id, m.ubicacion_id, sum(m.cantidad) AS cantidad
                FROM movimientos AS m
                GROUP BY m.articulo_id, m.almacen_id, m.ubicacion_id) AS d
            WHERE e.empresa_id = {0}
                AND e.articulo_id = d.articulo_id
                AND e.almacen_id = d.almacen_id
                AND e.ubicacion_id = d.ubicacion_id
                AND e.lote_id IS NULL
            RETURNING e.id, e.articulo_id, e.almacen_id, e.ubicacion_id
        )
        INSERT INTO inventario.instantaneas_mensuales AS i (existencia_id, mes, empresa_id, fisico)
        SELECT v.id, g.mes::date, {0}, sum(m.cantidad)
        FROM movimientos AS m
        JOIN vivas AS v
            ON v.articulo_id = m.articulo_id
            AND v.almacen_id = m.almacen_id
            AND v.ubicacion_id = m.ubicacion_id
        CROSS JOIN LATERAL generate_series(
            m.mes::timestamp,
            (SELECT c.hasta_el_mes FROM inventario.cortes_de_la_instantanea AS c
             WHERE c.empresa_id = {0})::timestamp,
            interval '1 month') AS g (mes)
        GROUP BY v.id, g.mes
        ORDER BY v.id, g.mes
        ON CONFLICT (existencia_id, mes) DO UPDATE SET fisico = i.fisico + excluded.fisico
        """;

    /// <summary>Las dos guardas de la sentencia, que quien anota comprueba antes de escribir nada.</summary>
    /// <remarks>
    /// Están aparte porque el repositorio guarda el documento antes de mover la proyección, y ese
    /// guardado, sin transacción, se confirmaría solo: tiene que saberlo antes, no después.
    /// </remarks>
    /// <param name="contexto">El contexto de Inventario.</param>
    /// <param name="empresaId">La empresa del inquilino.</param>
    /// <param name="movimientos">Las filas del libro que se van a anotar.</param>
    /// <exception cref="InvalidOperationException">
    /// No hay transacción abierta, o alguna fila es de otra empresa.
    /// </exception>
    internal static void ExigirLoQueLaSentenciaNecesita(
        InventarioDbContext contexto,
        Guid empresaId,
        IReadOnlyCollection<MovimientoStock> movimientos)
    {
        if (contexto.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "No hay transacción abierta en el contexto de Inventario, así que la proyección se " +
                "confirmaría por su cuenta y el libro podría no llegar a escribirse. El dueño de la " +
                "transacción es el filtro de idempotencia: la acción que anota el libro tiene que " +
                "declarar la Idempotency-Key obligatoria.");
        }

        if (movimientos.Any(movimiento => movimiento.EmpresaId != empresaId))
        {
            throw new InvalidOperationException(
                "Se va a anotar una fila del libro de otra empresa: sumaría en las existencias de " +
                "otra sociedad (R8).");
        }
    }

    /// <summary>Mueve la proyección con las filas del libro que se van a anotar: primero bloquea, luego suma.</summary>
    /// <remarks>
    /// <para>
    /// <b>Revienta si no hay transacción</b>, por lo mismo que el numerador: EF Core abre una
    /// implícita por orden, así que sin esta comprobación la proyección se confirmaría sola y las
    /// filas del libro podrían no llegar nunca. Una proyección que dice más que su libro es
    /// exactamente la discrepancia que la R3 prohíbe.
    /// </para>
    /// <para>
    /// <b>Y la empresa la pone quien llama, del inquilino</b>, no la sentencia de las filas. El filtro
    /// global no alcanza al SQL crudo, así que esto comprueba que cada fila es de esa empresa antes
    /// de mandar nada: una fila de otra sumaría en la existencia de otra sociedad.
    /// </para>
    /// </remarks>
    /// <param name="contexto">El contexto de Inventario, con la transacción abierta.</param>
    /// <param name="empresaId">La empresa del inquilino, que es la de todas las filas.</param>
    /// <param name="movimientos">Las filas del libro que se van a anotar.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>Una tarea que acaba cuando la sentencia ha corrido.</returns>
    /// <exception cref="InvalidOperationException">
    /// No hay transacción abierta, o alguna fila es de otra empresa.
    /// </exception>
    /// <exception cref="Npgsql.PostgresException">
    /// Con <c>ck_existencias_fisico_no_negativo</c>, si alguna clave quedaría por debajo de cero. Sube
    /// sin traducir (ADR-0004), y el borde la contesta como <c>422</c> <c>stock-insuficiente</c>.
    /// </exception>
    internal static async Task MoverAsync(
        InventarioDbContext contexto,
        Guid empresaId,
        IReadOnlyCollection<MovimientoStock> movimientos,
        CancellationToken cancelacion)
    {
        ExigirLoQueLaSentenciaNecesita(contexto, empresaId, movimientos);

        if (movimientos.Count == 0)
        {
            return;
        }

        // Un identificador por CLAVE y no por fila: las filas de la misma clave tienen que caer en
        // el mismo grupo, y el identificador es parte del grupo. Solo lo usa la sentencia que crea,
        // si la clave es nueva; si ya tenía fila viva, el `ON CONFLICT` se queda con el suyo.
        Dictionary<(Guid Articulo, Guid Almacen, Guid Ubicacion), Guid> identificadores = [];

        var ids = new Guid[movimientos.Count];
        var articulos = new Guid[movimientos.Count];
        var almacenes = new Guid[movimientos.Count];
        var ubicaciones = new Guid[movimientos.Count];
        var meses = new DateOnly[movimientos.Count];
        decimal[] cantidades = new decimal[movimientos.Count];

        int posicion = 0;

        foreach (MovimientoStock movimiento in movimientos)
        {
            (Guid Articulo, Guid Almacen, Guid Ubicacion) clave =
                (movimiento.ArticuloId, movimiento.AlmacenId, movimiento.UbicacionId);

            if (!identificadores.TryGetValue(clave, out Guid id))
            {
                id = Guid.CreateVersion7();
                identificadores[clave] = id;
            }

            ids[posicion] = id;
            articulos[posicion] = movimiento.ArticuloId;
            almacenes[posicion] = movimiento.AlmacenId;
            ubicaciones[posicion] = movimiento.UbicacionId;
            meses[posicion] = new DateOnly(
                movimiento.FechaDeOperacion.Year, movimiento.FechaDeOperacion.Month, 1);
            cantidades[posicion] = movimiento.CantidadEnUnidadBase;
            posicion++;
        }

        await contexto.Database
            .ExecuteSqlRawAsync(
                SqlQueCreaYBloqueaLasVivas,
                [empresaId, ids, articulos, almacenes, ubicaciones],
                cancelacion)
            .ConfigureAwait(false);

        await contexto.Database
            .ExecuteSqlRawAsync(
                SqlQueMueveLaProyeccion,
                [empresaId, articulos, almacenes, ubicaciones, meses, cantidades],
                cancelacion)
            .ConfigureAwait(false);
    }
}
