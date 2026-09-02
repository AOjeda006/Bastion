using System.ComponentModel.DataAnnotations;

namespace Bastion.Organizacion.Contracts.Ubicaciones;

/// <summary>Una ubicación dentro de un almacén, tal como sale de la API.</summary>
/// <param name="Id">Identificador de la ubicación.</param>
/// <param name="EmpresaId">Empresa a la que pertenece (R8).</param>
/// <param name="AlmacenId">Almacén en el que está.</param>
/// <param name="Codigo">Código de la ubicación, en mayúsculas y único dentro del almacén.</param>
/// <param name="Pasillo">Pasillo, o nulo si el almacén no se organiza así.</param>
/// <param name="Estante">Estante, o nulo.</param>
/// <param name="Hueco">Hueco, o nulo.</param>
/// <param name="Descripcion">Descripción libre, o nula.</param>
/// <remarks>
/// <b>No lleva estado ni fecha de bloqueo, y su ausencia es la regla</b> (R16), igual que en el
/// almacén: lo bloqueado no sale por un camino ordinario, así que un campo de estado solo podría
/// decir «activa».
/// </remarks>
public sealed record UbicacionDto(
    Guid Id,
    Guid EmpresaId,
    Guid AlmacenId,
    string Codigo,
    string? Pasillo,
    string? Estante,
    string? Hueco,
    string? Descripcion);

/// <summary>Lo que hace falta para dar de alta una ubicación.</summary>
public sealed record CrearUbicacionDto
{
    // No hay campo `EmpresaId`, y su AUSENCIA es la regla (R8): sale del claim del token. El
    // almacén sí viaja, porque dentro de una empresa hay varios y hay que decir en cuál.
    /// <summary>Almacén en el que está la ubicación.</summary>
    [Required(ErrorMessage = "El almacén es obligatorio.")]
    public Guid AlmacenId { get; init; }

    /// <summary>Código de la ubicación. Se normaliza a mayúsculas.</summary>
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(20, ErrorMessage = "El código no puede pasar de {1} caracteres.")]
    public string Codigo { get; init; } = string.Empty;

    /// <summary>Pasillo. Opcional: no todos los almacenes se organizan por coordenadas.</summary>
    [StringLength(20, ErrorMessage = "El pasillo no puede pasar de {1} caracteres.")]
    public string? Pasillo { get; init; }

    /// <summary>Estante.</summary>
    [StringLength(20, ErrorMessage = "El estante no puede pasar de {1} caracteres.")]
    public string? Estante { get; init; }

    /// <summary>Hueco.</summary>
    [StringLength(20, ErrorMessage = "El hueco no puede pasar de {1} caracteres.")]
    public string? Hueco { get; init; }

    /// <summary>Descripción libre.</summary>
    [StringLength(120, ErrorMessage = "La descripción no puede pasar de {1} caracteres.")]
    public string? Descripcion { get; init; }
}

/// <summary>
/// Lo que se puede cambiar de una ubicación.
/// </summary>
/// <remarks>
/// Ni el código ni el almacén. El código va impreso en la etiqueta que está pegada a la
/// estantería, y mover una ubicación de almacén no es cambiarle un campo: es darla de baja allí y
/// crearla aquí, con las existencias que eso arrastra.
/// </remarks>
public sealed record ModificarUbicacionDto
{
    /// <summary>Pasillo, o nulo.</summary>
    [StringLength(20, ErrorMessage = "El pasillo no puede pasar de {1} caracteres.")]
    public string? Pasillo { get; init; }

    /// <summary>Estante, o nulo.</summary>
    [StringLength(20, ErrorMessage = "El estante no puede pasar de {1} caracteres.")]
    public string? Estante { get; init; }

    /// <summary>Hueco, o nulo.</summary>
    [StringLength(20, ErrorMessage = "El hueco no puede pasar de {1} caracteres.")]
    public string? Hueco { get; init; }

    /// <summary>Descripción libre, o nula.</summary>
    [StringLength(120, ErrorMessage = "La descripción no puede pasar de {1} caracteres.")]
    public string? Descripcion { get; init; }
}
