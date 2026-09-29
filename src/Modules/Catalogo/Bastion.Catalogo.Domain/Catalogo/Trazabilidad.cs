namespace Bastion.Catalogo.Domain.Catalogo;

/// <summary>
/// Cómo se sigue la pista a las existencias de un artículo: la marca que decide si sus movimientos
/// llevan lote, número de serie o nada.
/// </summary>
/// <remarks>
/// <para>
/// <b>Excluyente, por decisión del usuario en la puerta de la fase 2.</b> Un artículo lleva lote o
/// serie, nunca los dos. El esquema de Inventario guarda las dos cosas por separado, así que el día
/// que un artículo necesite las dos será un cuarto valor aquí y no un cambio de la clave del libro
/// (ADR-0048 §1).
/// </para>
/// <para>
/// <b>No cambia en cuanto hay un movimiento.</b> Un libro con filas sin lote no se puede leer por
/// lote, ni al revés. Quien lo impide es <c>ModificarArticulo</c>, que pregunta a Inventario con la
/// fila del artículo bloqueada (ADR-0048 §4). El dominio no lo sabe, porque los movimientos no son
/// suyos.
/// </para>
/// <para>
/// Se guarda como <b>texto</b>, como <see cref="TipoDeArticulo"/> y por el mismo motivo.
/// </para>
/// </remarks>
public enum Trazabilidad
{
    /// <summary>Sin lote ni serie: el stock se cuenta por artículo y ubicación.</summary>
    Ninguna = 0,

    /// <summary>Cada movimiento lleva su lote, y el stock se cuenta por lote.</summary>
    PorLote = 1,

    /// <summary>
    /// Cada movimiento lleva su número de serie y mueve una sola unidad, y cada serie está como
    /// mucho en un sitio.
    /// </summary>
    PorNumeroSerie = 2,
}
