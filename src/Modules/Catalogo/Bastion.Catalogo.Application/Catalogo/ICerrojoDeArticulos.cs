namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>
/// El cerrojo <b>exclusivo</b> sobre la fila de un artículo, que toma quien va a cambiar su marca
/// antes de preguntar si tiene movimientos.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué no basta el testigo de concurrencia (R11)</b>, con el mismo argumento que el cierre
/// del ejercicio: confirmar un ajuste no escribe la fila del artículo, así que no hay versión con
/// la que chocar. Sin este cerrojo, Catálogo pregunta «¿tiene movimientos?», Inventario confirma el
/// primero contra la marca vieja, y Catálogo cambia la marca con la respuesta de antes (ADR-0042).
/// </para>
/// <para>
/// <b><c>FOR NO KEY UPDATE</c> y no <c>FOR UPDATE</c></b> (ADR-0048 §4). Es el cerrojo que toma el
/// propio <c>UPDATE</c> del artículo, y ya choca con el <c>FOR SHARE</c> con el que Inventario lee
/// la marca. <c>FOR UPDATE</c> pararía además las claves ajenas que apuntan al artículo, como una
/// línea de tarifa nueva, y el cambio de marca no tiene nada que decir sobre ellas.
/// </para>
/// </remarks>
public interface ICerrojoDeArticulos
{
    /// <summary>Bloquea la fila del artículo hasta el <c>COMMIT</c>.</summary>
    /// <param name="id">El artículo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>Falso si no hay tal artículo en esta empresa.</returns>
    Task<bool> TomarEnExclusivaAsync(Guid id, CancellationToken cancelacion);
}
