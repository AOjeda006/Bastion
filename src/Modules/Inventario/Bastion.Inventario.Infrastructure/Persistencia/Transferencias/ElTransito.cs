using Bastion.Inventario.Domain.Transferencias;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Inventario.Infrastructure.Persistencia.Existencias;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Inventario.Infrastructure.Persistencia.Transferencias;

/// <summary>
/// Las sentencias de lo que vuela: suman el tránsito en la existencia y en la valoración del
/// destino (ADR-0053 §1).
/// </summary>
/// <remarks>
/// <para>
/// <b>No escriben ninguna fila del libro</b>, y por eso no tocan ni el físico, ni las instantáneas,
/// ni la última fecha de la valoración: el tránsito es la proyección de los documentos, no del libro.
/// </para>
/// <para>
/// <b>Suman sobre filas bloqueadas, como las del libro.</b> La existencia la crea y la bloquea la
/// misma sentencia que usa el libro, y la valoración la bloqueó el caso de uso antes de valorar. Las
/// dos cuentan las filas que tocan, y una que falte es un defecto que revienta.
/// </para>
/// <para>
/// <b>El signo lo trae cada fila</b>: positivo al enviar, negativo al recibir y en el inverso de una
/// enviada. Las guardas de la tabla miran la fila ya sumada, y ninguna se traduce: restar más de lo
/// que vuela es saltarse el documento, y el testigo de su fila impide que dos lo resten a la vez.
/// </para>
/// </remarks>
internal static class ElTransito
{
    /// <summary>Suma lo que vuela en la existencia de cada clave del destino.</summary>
    /// <remarks>
    /// Agrupado por clave, porque dos líneas al mismo hueco darían dos filas para el mismo
    /// <c>UPDATE</c>, y PostgreSQL solo toca una vez cada fila por sentencia.
    /// </remarks>
    internal const string SqlQueMueveLaExistencia =
        """
        UPDATE inventario.existencias AS e
        SET en_transito = e.en_transito + d.cantidad
        FROM (
            SELECT t.articulo_id, t.almacen_id, t.ubicacion_id, t.lote_id, t.numero_de_serie_id,
                   sum(t.cantidad) AS cantidad
            FROM unnest({1}::uuid[], {2}::uuid[], {3}::uuid[], {4}::uuid[], {5}::uuid[], {6}::numeric[])
                AS t (articulo_id, almacen_id, ubicacion_id, lote_id, numero_de_serie_id, cantidad)
            GROUP BY t.articulo_id, t.almacen_id, t.ubicacion_id, t.lote_id, t.numero_de_serie_id) AS d
        WHERE e.empresa_id = {0}
            AND e.articulo_id = d.articulo_id
            AND e.almacen_id = d.almacen_id
            AND e.ubicacion_id = d.ubicacion_id
            AND e.lote_id IS NOT DISTINCT FROM d.lote_id
            AND e.numero_de_serie_id IS NOT DISTINCT FROM d.numero_de_serie_id
        """;

    /// <summary>Suma lo que vuela, en cantidad y en valor, en la valoración de cada clave del destino.</summary>
    /// <remarks>
    /// <b>La divisa pasa a ser la del documento</b>, y eso solo cambia algo en una clave vacía: sin
    /// cantidad y sin nada en vuelo. Con cualquiera de las dos en otra divisa, el caso de uso ya
    /// contestó, y la condición lo vuelve a exigir: una clave que no la cumpla no se toca, y el
    /// recuento lo denuncia.
    /// </remarks>
    internal const string SqlQueMueveLaValoracion =
        """
        UPDATE inventario.valoraciones AS v
        SET en_transito = v.en_transito + d.cantidad,
            valor_en_transito = v.valor_en_transito + d.valor,
            divisa = {1}
        FROM (
            SELECT t.articulo_id, t.almacen_id, sum(t.cantidad) AS cantidad, sum(t.valor) AS valor
            FROM unnest({2}::uuid[], {3}::uuid[], {4}::numeric[], {5}::numeric[])
                AS t (articulo_id, almacen_id, cantidad, valor)
            GROUP BY t.articulo_id, t.almacen_id) AS d
        WHERE v.empresa_id = {0}
            AND v.articulo_id = d.articulo_id
            AND v.almacen_id = d.almacen_id
            AND (v.divisa = {1} OR (v.cantidad = 0 AND v.en_transito = 0))
        """;

    /// <summary>Mueve lo que vuela: primero la existencia y después la valoración.</summary>
    /// <param name="contexto">El contexto de Inventario, con la transacción abierta.</param>
    /// <param name="empresaId">La empresa del inquilino.</param>
    /// <param name="transito">Lo que vuela, con su signo.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>Una tarea que acaba cuando las sentencias han corrido.</returns>
    /// <exception cref="InvalidOperationException">
    /// No hay transacción, lo que vuela mezcla divisas, o alguna clave no se ha tocado.
    /// </exception>
    internal static async Task MoverAsync(
        InventarioDbContext contexto,
        Guid empresaId,
        IReadOnlyCollection<MovimientoEnTransito> transito,
        CancellationToken cancelacion)
    {
        LaProyeccionDelLibro.ExigirLoQueLaSentenciaNecesita(contexto, empresaId, []);

        if (transito.Count == 0)
        {
            return;
        }

        string[] divisas = [.. transito.Select(vuela => vuela.Valor.Divisa).Distinct()];

        if (divisas.Length != 1)
        {
            throw new InvalidOperationException(
                $"Lo que vuela viene en {string.Join(", ", divisas)}: un documento habla una sola " +
                "divisa, la de su cabecera (ADR-0046 §7).");
        }

        Guid[] articulos = [.. transito.Select(vuela => vuela.ArticuloId)];
        Guid[] almacenes = [.. transito.Select(vuela => vuela.AlmacenId)];
        Guid[] ubicaciones = [.. transito.Select(vuela => vuela.UbicacionId)];
        Guid?[] lotes = [.. transito.Select(vuela => vuela.LoteId)];
        Guid?[] numerosDeSerie = [.. transito.Select(vuela => vuela.NumeroDeSerieId)];
        decimal[] cantidades = [.. transito.Select(vuela => vuela.Cantidad)];
        decimal[] valores = [.. transito.Select(vuela => vuela.Valor.Cantidad)];

        await LaProyeccionDelLibro
            .CrearYBloquearLasVivasAsync(
                contexto, empresaId, articulos, almacenes, ubicaciones, lotes, numerosDeSerie, cancelacion)
            .ConfigureAwait(false);

        int existencias = await contexto.Database
            .ExecuteSqlRawAsync(
                SqlQueMueveLaExistencia,
                [empresaId, articulos, almacenes, ubicaciones, lotes, numerosDeSerie, cantidades],
                cancelacion)
            .ConfigureAwait(false);

        int claves = transito
            .Select(vuela => (vuela.ArticuloId, vuela.AlmacenId, vuela.UbicacionId, vuela.LoteId, vuela.NumeroDeSerieId))
            .Distinct()
            .Count();

        if (existencias != claves)
        {
            throw new InvalidOperationException(
                $"Lo que vuela toca {claves} existencias y la sentencia ha movido {existencias}: la " +
                "que acaba de crear la sentencia anterior tendría que estar.");
        }

        int valoraciones = await contexto.Database
            .ExecuteSqlRawAsync(
                SqlQueMueveLaValoracion,
                [empresaId, divisas[0], articulos, almacenes, cantidades, valores],
                cancelacion)
            .ConfigureAwait(false);

        int clavesDeValoracion = transito
            .Select(vuela => new ClaveDeValoracion(vuela.ArticuloId, vuela.AlmacenId))
            .Distinct()
            .Count();

        if (valoraciones != clavesDeValoracion)
        {
            throw new InvalidOperationException(
                $"Lo que vuela toca {clavesDeValoracion} valoraciones y la sentencia ha movido " +
                $"{valoraciones}: alguna no se bloqueó antes o está en otra divisa con existencias o " +
                "con algo en vuelo (ADR-0053 §1).");
        }
    }
}
