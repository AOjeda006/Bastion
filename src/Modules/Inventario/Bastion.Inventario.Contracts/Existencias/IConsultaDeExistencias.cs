namespace Bastion.Inventario.Contracts.Existencias;

/// <summary>
/// Lo que otro módulo puede preguntar a Inventario sobre lo que hay de un artículo en un almacén, y
/// lo que se puede comprometer de ello, sin ver el libro ni las reservas.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es la proyección de la precisión 1 del ADR-0059</b>: Ventas, en la fase 4, no lee las tablas
/// de Inventario, porque ninguna consulta cruza esquemas (§5, regla 4). Pregunta aquí, y por aquí
/// solo cruzan identificadores y tres cifras.
/// </para>
/// <para>
/// <b>Para decidir, no basta</b>: la respuesta es la del instante de la lectura, sin cerrojo. Quien
/// aparta mercancía no compara contra esto: reserva, y la reserva mira el disponible con la
/// valoración de la clave bloqueada (ADR-0059 §5). Esto es lo que enseña una pantalla de pedido.
/// </para>
/// </remarks>
public interface IConsultaDeExistencias
{
    /// <summary>
    /// El físico, lo reservado y el disponible de cada artículo en un almacén, en una sola
    /// sentencia, así que las tres cifras salen de la misma foto (ADR-0059 §3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Va por lotes</b>, como la unidad base en Catálogo: una pantalla de pedido pregunta por
    /// todas sus líneas a la vez.
    /// </para>
    /// <para>
    /// <b>Todo artículo pedido vuelve</b>, y uno sin existencias contesta tres ceros: desde fuera de
    /// Inventario, no haber tenido nunca y no tener ahora son lo mismo. Un artículo o un almacén de
    /// otra empresa contestan igual, porque la empresa es la del ámbito.
    /// </para>
    /// <para>
    /// <b>El disponible puede salir negativo</b>, y no se recorta: un −5 dice que faltan cinco para
    /// servir lo apartado.
    /// </para>
    /// </remarks>
    /// <param name="almacenId">El almacén.</param>
    /// <param name="articulos">Los artículos por los que se pregunta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>Las tres cifras de cada artículo pedido, en su unidad base.</returns>
    Task<IReadOnlyDictionary<Guid, DisponibleDeUnArticulo>> DisponibleDeAsync(
        Guid almacenId, IReadOnlyCollection<Guid> articulos, CancellationToken cancelacion);
}

/// <summary>Lo que hay de un artículo en un almacén, y lo que se puede comprometer de ello.</summary>
/// <param name="Fisico">La suma de su libro en ese almacén, sin lo que viene en tránsito.</param>
/// <param name="Reservado">Lo pendiente de sus reservas activas y vigentes.</param>
/// <param name="Disponible">El físico menos lo reservado. Puede ser negativo.</param>
public sealed record DisponibleDeUnArticulo(decimal Fisico, decimal Reservado, decimal Disponible);
