using Bastion.Inventario.Domain.Movimientos;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Inventario.Infrastructure.Persistencia.Existencias;

/// <summary>
/// La sentencia que mueve la proyección cuando se anota el libro: la fila viva de cada existencia
/// y sus instantáneas mensuales, a la vez (ADR-0044).
/// </summary>
/// <remarks>
/// <para>
/// <b>Una sola sentencia y no dos</b>, por la decisión (a) de la puerta del 2.7: la fila viva y las
/// instantáneas se mueven juntas o no se mueven. Con dos, una confirmación que se cortara entre
/// ellas no dejaría nada a medias —la transacción es la misma—, pero cualquiera que las leyera
/// entre medias sí vería una cosa movida y la otra no.
/// </para>
/// <para>
/// <b>La aplicación nunca lee lo que suma.</b> Las dos escrituras son <c>INSERT … ON CONFLICT DO
/// UPDATE</c> que suman sobre la fila que hay en el motor, con la fila bloqueada. Leer el saldo,
/// sumarle y escribirlo dejaría una ventana entre la lectura y la escritura, y dos confirmaciones
/// simultáneas del mismo artículo se llevarían el mismo saldo de partida: una de las dos cantidades
/// se perdería, sin error.
/// </para>
/// <para>
/// <b>Las claves van ordenadas</b>, y eso es lo que impide el interbloqueo. Dos confirmaciones que
/// tocan los mismos dos artículos los bloquean en el mismo orden, así que la segunda espera a la
/// primera en vez de quedarse cada una con la fila que la otra necesita. Las instantáneas no
/// necesitan orden propio: cada una cuelga de una sola existencia, y quien la toca ya tiene
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
    /// <b>La sentencia que decide.</b> Suma cada clave en su fila viva y cada mes en su instantánea,
    /// desde el mes del movimiento hasta el corte de la empresa, creando las que falten.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La primera escritura va en un <c>WITH</c></b> porque la segunda necesita el identificador
    /// de cada fila viva, que para las claves nuevas es el que acaba de entrar y para las viejas el
    /// que ya tenían. <c>RETURNING</c> devuelve los dos.
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
            SELECT m.id, m.articulo_id, m.almacen_id, m.ubicacion_id, m.mes, m.cantidad
            FROM unnest({1}::uuid[], {2}::uuid[], {3}::uuid[], {4}::uuid[], {5}::date[], {6}::numeric[])
                AS m (id, articulo_id, almacen_id, ubicacion_id, mes, cantidad)
        ),
        vivas AS (
            INSERT INTO inventario.existencias AS e
                (id, empresa_id, articulo_id, almacen_id, ubicacion_id, lote_id, fisico, reservado)
            SELECT m.id, {0}, m.articulo_id, m.almacen_id, m.ubicacion_id, NULL::uuid, sum(m.cantidad), 0
            FROM movimientos AS m
            GROUP BY m.id, m.articulo_id, m.almacen_id, m.ubicacion_id
            ORDER BY m.articulo_id, m.almacen_id, m.ubicacion_id
            ON CONFLICT (empresa_id, articulo_id, almacen_id, ubicacion_id, lote_id)
            DO UPDATE SET fisico = e.fisico + excluded.fisico
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

    /// <summary>Mueve la proyección con las filas del libro que se van a anotar.</summary>
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
    internal static async Task MoverAsync(
        InventarioDbContext contexto,
        Guid empresaId,
        IReadOnlyCollection<MovimientoStock> movimientos,
        CancellationToken cancelacion)
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

        if (movimientos.Count == 0)
        {
            return;
        }

        // Un identificador por CLAVE y no por fila: las filas de la misma clave tienen que caer en
        // el mismo grupo, y el identificador es parte del grupo. Solo se usa si la clave es nueva;
        // si ya tenía fila viva, el `ON CONFLICT` se queda con el suyo.
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
                SqlQueMueveLaProyeccion,
                [empresaId, ids, articulos, almacenes, ubicaciones, meses, cantidades],
                cancelacion)
            .ConfigureAwait(false);
    }
}
