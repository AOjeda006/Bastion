namespace Bastion.Inventario.Contracts.Movimientos;

/// <summary>
/// Lo que otro módulo puede preguntar a Inventario sobre los movimientos de un artículo, sin ver el
/// libro.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo publica Inventario y lo consume Catálogo</b>, que es la mitad de vuelta del segundo cruce
/// mutuo del proyecto (ADR-0048 §7): Inventario ya preguntaba a Catálogo por sus artículos. Los dos
/// <c>Contracts</c> no se ven entre sí, y por aquí solo cruzan un <c>Guid</c> y un <c>bool</c>.
/// </para>
/// <para>
/// <b>Quien pregunta para decidir tiene que haber bloqueado antes</b> lo que decide (ADR-0042). La
/// respuesta es la del instante de la lectura, y una confirmación en vuelo no se ve hasta su
/// <c>COMMIT</c>. Cambiar la marca de un artículo bloquea su fila antes de preguntar, y la
/// confirmación la lee con un cerrojo compartido, así que una de las dos espera a la otra.
/// </para>
/// </remarks>
public interface IMovimientosDeArticulos
{
    /// <summary>
    /// Si el libro tiene alguna fila de ese artículo en la empresa del ámbito.
    /// </summary>
    /// <remarks>
    /// <b>Un borrador no es un movimiento.</b> Un ajuste sin confirmar no ha escrito nada en el
    /// libro, y su marca se comprueba otra vez al confirmarlo.
    /// </remarks>
    /// <param name="articuloId">El artículo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>Verdadero si hay al menos una fila en el libro.</returns>
    Task<bool> TieneMovimientosAsync(Guid articuloId, CancellationToken cancelacion);
}
