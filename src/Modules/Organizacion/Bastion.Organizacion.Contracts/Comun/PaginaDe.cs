namespace Bastion.Organizacion.Contracts.Comun;

/// <summary>
/// Una página de una colección, con lo que hace falta para pedir la siguiente.
/// </summary>
/// <remarks>
/// Devolver una lista pelada obliga al cliente a adivinar si hay más, y a la primera empresa con
/// diez mil artículos el listado se convierte en una descarga completa (§9, «colecciones
/// paginadas, con el total cuando es barato calcularlo»).
/// </remarks>
/// <typeparam name="T">Lo que hay en la página.</typeparam>
/// <param name="Elementos">Los de esta página, en el orden pedido.</param>
/// <param name="Pagina">Número de página, empezando en 1.</param>
/// <param name="Tamanio">Cuántos elementos caben por página.</param>
/// <param name="Total">Cuántos hay en total, no en esta página.</param>
public sealed record PaginaDe<T>(IReadOnlyList<T> Elementos, int Pagina, int Tamanio, long Total);
