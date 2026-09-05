using System.ComponentModel.DataAnnotations;
using Bastion.BuildingBlocks.Contracts.Direcciones;
using Bastion.Organizacion.Contracts.Comun;

namespace Bastion.Organizacion.Contracts.Almacenes;

/// <summary>Un almacén, tal como sale de la API.</summary>
/// <param name="Id">Identificador del almacén.</param>
/// <param name="EmpresaId">Empresa a la que pertenece (R8).</param>
/// <param name="Codigo">Código del almacén, en mayúsculas.</param>
/// <param name="Nombre">Nombre con el que se le conoce.</param>
/// <param name="Direccion">Dónde está, o nulo si es virtual o de tránsito.</param>
/// <param name="Tipo">Tipo de almacén, como texto.</param>
/// <remarks>
/// <b>No lleva estado ni fecha de bloqueo, y su ausencia es la regla</b> (R16, desde el 0.10). El
/// filtro de repositorio deja fuera lo bloqueado, así que todo lo que sale por un camino ordinario
/// está activo por construcción: un campo `Estado` solo podría decir «activo», y un campo que solo
/// puede decir una cosa no informa, confunde. Lo bloqueado se ve abriendo un ámbito declarado, y
/// quien lo abra tiene delante la entidad entera.
/// </remarks>
public sealed record AlmacenDto(
    Guid Id,
    Guid EmpresaId,
    string Codigo,
    string Nombre,
    DireccionDto? Direccion,
    string Tipo);

/// <summary>Lo que hace falta para dar de alta un almacén.</summary>
public sealed record CrearAlmacenDto
{
    // No hay campo `EmpresaId`, y su AUSENCIA es la regla (R8). La empresa sale del <i>claim</i>
    // del token y no hay ningún camino que la lea de la petición, así que no puede existir un caso
    // de uso que se olvide de comprobarla: el dato no llega por ahí. Con el campo puesto, el
    // permiso «crear en mi empresa» sería en realidad «crear en cualquiera», y la comprobación que
    // lo evitara habría que acordarse de escribirla en cada caso de uso, para siempre.
    /// <summary>Código del almacén. Se normaliza a mayúsculas.</summary>
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(20, ErrorMessage = "El código no puede pasar de {1} caracteres.")]
    public string Codigo { get; init; } = string.Empty;

    /// <summary>Nombre con el que se le conoce.</summary>
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede pasar de {1} caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    /// <summary>
    /// Dónde está. Opcional en el contrato porque un almacén virtual o de tránsito no está en
    /// ningún sitio; para uno físico la exige el dominio, que es quien sabe la regla.
    /// </summary>
    public DireccionDto? Direccion { get; init; }

    /// <summary>Tipo de almacén, como texto.</summary>
    [Required(ErrorMessage = "El tipo de almacén es obligatorio.")]
    public string Tipo { get; init; } = string.Empty;
}

/// <summary>
/// Lo que se puede cambiar de un almacén.
/// </summary>
/// <remarks>
/// Sin código: aparece en albaranes y en etiquetas que ya están impresas y fuera, y cambiarlo
/// rompería la correspondencia con ese papel.
/// </remarks>
public sealed record ModificarAlmacenDto
{
    /// <summary>Nombre con el que se le conoce.</summary>
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede pasar de {1} caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Dónde está, o nulo si es virtual o de tránsito.</summary>
    public DireccionDto? Direccion { get; init; }

    /// <summary>Tipo de almacén, como texto.</summary>
    [Required(ErrorMessage = "El tipo de almacén es obligatorio.")]
    public string Tipo { get; init; } = string.Empty;
}
