namespace Bastion.Inventario.Infrastructure.Persistencia.Reservas;

/// <summary>
/// Las dos sentencias que suman lo reservado al leer: la de las claves que un caso de uso tiene
/// bloqueadas, y la del disponible que se publica en <c>Contracts</c> (ADR-0059 §3).
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo reservado de una clave</b> es la suma de lo pendiente de sus reservas guardadas
/// <c>Activa</c> cuya caducidad no ha llegado: la cantidad menos lo consumido. Las dos sentencias lo
/// leen con el mismo fragmento, así que no pueden contarlo de dos maneras.
/// </para>
/// <para>
/// <b>«Ahora» viaja como parámetro</b>, el del <c>TimeProvider</c>, y nunca es el <c>now()</c> del
/// motor: así el reloj congelado de los tests vale también aquí dentro.
/// </para>
/// <para>
/// <b>La empresa la compara la sentencia</b>, porque el SQL crudo no pasa por el filtro global. Y
/// <b>sin punto y coma final</b>: <c>SqlQueryRaw</c> la mete dentro de otra sentencia (ADR-0043).
/// </para>
/// </remarks>
internal static class LoReservado
{
    /// <summary>
    /// Lo reservado de la clave <c>c</c> de la sentencia que lo incluye, como <c>lo.reservado</c>:
    /// nulo si no tiene reservas. <c>{0}</c> es la empresa y <c>{1}</c>, ahora.
    /// </summary>
    /// <remarks>
    /// Lo lee el índice parcial <c>ix_reservas_activas_por_clave</c>, y lo consumido de cada reserva,
    /// el índice único de sus consumos, que empieza por ella.
    /// </remarks>
    private const string DeLaClave =
        """
        LEFT JOIN LATERAL (
            SELECT sum(r.cantidad - COALESCE(consumida.cantidad, 0)) AS reservado
            FROM inventario.reservas AS r
            LEFT JOIN LATERAL (
                SELECT sum(x.cantidad) AS cantidad
                FROM inventario.consumos_de_reserva AS x
                WHERE x.reserva_id = r.id) AS consumida ON true
            WHERE r.empresa_id = {0}
                AND r.articulo_id = c.articulo_id
                AND r.almacen_id = c.almacen_id
                AND r.estado = 'Activa'
                AND (r.caduca_el IS NULL OR r.caduca_el > {1})) AS lo ON true
        """;

    /// <summary>
    /// Lo reservado de cada clave pedida, una fila por clave distinta, con cero si no tiene
    /// reservas. <c>{2}</c> y <c>{3}</c> son los artículos y los almacenes, por parejas.
    /// </summary>
    internal const string SqlDeLasClaves =
        $$"""
        SELECT c.articulo_id, c.almacen_id, COALESCE(lo.reservado, 0) AS reservado
        FROM (SELECT DISTINCT k.articulo_id, k.almacen_id
            FROM unnest({2}::uuid[], {3}::uuid[]) AS k (articulo_id, almacen_id)) AS c
        {{DeLaClave}}
        """;

    /// <summary>
    /// El físico, lo reservado y el disponible de cada artículo pedido en un almacén, en una sola
    /// sentencia y con ceros donde no hay fila. <c>{2}</c> es el almacén y <c>{3}</c>, los artículos.
    /// </summary>
    /// <remarks>
    /// <b>El físico es la cantidad de la valoración</b>, la suma del libro de la clave, que no
    /// cuenta lo que viene en tránsito: eso vive en <c>en_transito</c> (ADR-0053 §1).
    /// </remarks>
    internal const string SqlDelDisponible =
        $$"""
        SELECT c.articulo_id,
            COALESCE(v.cantidad, 0) AS fisico,
            COALESCE(lo.reservado, 0) AS reservado,
            COALESCE(v.cantidad, 0) - COALESCE(lo.reservado, 0) AS disponible
        FROM (SELECT DISTINCT a.articulo_id, {2}::uuid AS almacen_id
            FROM unnest({3}::uuid[]) AS a (articulo_id)) AS c
        LEFT JOIN inventario.valoraciones AS v
            ON v.empresa_id = {0}
            AND v.articulo_id = c.articulo_id
            AND v.almacen_id = c.almacen_id
        {{DeLaClave}}
        """;
}

/// <summary>Una fila de lo reservado de una clave, tal como llega.</summary>
/// <remarks>Las columnas se leen por el nombre que les da la convención del contexto.</remarks>
internal sealed class FilaDeLoReservado
{
    public Guid ArticuloId { get; init; }

    public Guid AlmacenId { get; init; }

    public decimal Reservado { get; init; }
}

/// <summary>Una fila del disponible de un artículo, tal como llega.</summary>
/// <remarks>Las columnas se leen por el nombre que les da la convención del contexto.</remarks>
internal sealed class FilaDelDisponible
{
    public Guid ArticuloId { get; init; }

    public decimal Fisico { get; init; }

    public decimal Reservado { get; init; }

    public decimal Disponible { get; init; }
}
