using System.ComponentModel.DataAnnotations;

namespace Bastion.Catalogo.Contracts.Catalogo;

/// <summary>Una categoría del árbol de clasificación, tal como sale de la API.</summary>
/// <remarks>
/// <b>Lleva el padre y no los hijos.</b> El árbol se guarda como lista de adyacencia —cada fila
/// sabe de quién cuelga y nada más—, y publicar el subárbol obligaría a recorrerlo entero en cada
/// lectura. Quien quiera pintar el árbol pide la página de categorías y lo compone en el cliente,
/// que es lo que ya hace con cualquier maestro pequeño.
/// </remarks>
/// <param name="Id">Identificador de la categoría.</param>
/// <param name="EmpresaId">Empresa a la que pertenece (R8).</param>
/// <param name="Codigo">Código, en mayúsculas. No cambia.</param>
/// <param name="Nombre">Nombre con el que se muestra.</param>
/// <param name="PadreId">Categoría de la que cuelga, o nula si es una raíz.</param>
public sealed record CategoriaDto(
    Guid Id,
    Guid EmpresaId,
    string Codigo,
    string Nombre,
    Guid? PadreId);

/// <summary>Lo que hace falta para dar de alta una categoría.</summary>
/// <remarks>
/// <b>No lleva empresa</b>: sale del <i>claim</i> y nunca del cuerpo (R8).
/// </remarks>
public sealed record CrearCategoriaDto
{
    /// <summary>Código de la categoría. Se normaliza a mayúsculas.</summary>
    [Required(ErrorMessage = "El código de la categoría es obligatorio.")]
    [StringLength(20, ErrorMessage = "El código no puede pasar de {1} caracteres.")]
    public string Codigo { get; init; } = string.Empty;

    /// <summary>Nombre con el que se muestra.</summary>
    [Required(ErrorMessage = "El nombre de la categoría es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede pasar de {1} caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Categoría de la que cuelga, o nula para crear una raíz.</summary>
    /// <remarks>
    /// El padre tiene que existir <b>y ser de la misma empresa</b>, y lo segundo lo sostiene el
    /// filtro de inquilinato: la consulta que lo busca no ve las categorías de otra empresa, así
    /// que un identificador prestado sale como «no existe».
    /// </remarks>
    public Guid? PadreId { get; init; }
}

/// <summary>
/// Lo que se puede cambiar de una categoría ya dada de alta.
/// </summary>
/// <remarks>
/// <b>Sin código</b>, como en todos los maestros de este sistema: el código es con lo que se la
/// nombra en informes y en importaciones, y cambiarlo rompe silenciosamente lo que apuntaba a él
/// por ese nombre. El padre <b>sí</b> cambia, y es justo la operación que puede cerrar un ciclo:
/// mover una rama debajo de su propia descendencia. Por eso la comprobación del árbol corre
/// también aquí y no solo en el alta.
/// </remarks>
public sealed record ModificarCategoriaDto
{
    /// <summary>Nombre con el que se muestra.</summary>
    [Required(ErrorMessage = "El nombre de la categoría es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede pasar de {1} caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Categoría de la que cuelga, o nula para dejarla como raíz.</summary>
    public Guid? PadreId { get; init; }
}
