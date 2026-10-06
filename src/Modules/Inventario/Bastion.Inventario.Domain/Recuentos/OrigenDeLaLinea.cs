namespace Bastion.Inventario.Domain.Recuentos;

/// <summary>De dónde sale una línea del recuento.</summary>
/// <remarks>
/// <b>Decide si la línea puede llevar coste</b> (ADR-0055 §6). La precargada es una clave que el
/// sistema ya tenía, y si sube entra al precio medio de su clave. La añadida puede ser una clave que
/// el sistema no conoce, y su coste es lo único que dice cuánto vale lo que se encontró.
/// </remarks>
public enum OrigenDeLaLinea
{
    /// <summary>La puso el alta: una clave del almacén con físico mayor que cero.</summary>
    Precargada = 1,

    /// <summary>La añadió quien cuenta.</summary>
    Anadida = 2,
}
