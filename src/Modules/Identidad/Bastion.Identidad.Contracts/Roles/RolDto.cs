using System.ComponentModel.DataAnnotations;

namespace Bastion.Identidad.Contracts.Roles;

/// <summary>Un rol, tal como sale de la API.</summary>
/// <param name="Id">Identificador del rol.</param>
/// <param name="Codigo">Código estable, en minúsculas y con guiones.</param>
/// <param name="Nombre">Nombre para la interfaz.</param>
/// <param name="EsDelSistema">Si lo creó la semilla y no se puede suprimir.</param>
/// <param name="Permisos">Permisos que concede, ordenados.</param>
public sealed record RolDto(
    Guid Id,
    string Codigo,
    string Nombre,
    bool EsDelSistema,
    IReadOnlyList<string> Permisos);

/// <summary>Lo que hace falta para crear un rol.</summary>
public sealed record CrearRolDto
{
    /// <summary>Código estable. Se normaliza a minúsculas.</summary>
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(40, ErrorMessage = "El código no puede pasar de {1} caracteres.")]
    public string Codigo { get; init; } = string.Empty;

    /// <summary>Nombre para la interfaz.</summary>
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede pasar de {1} caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Permisos que concede. Se validan contra el catálogo.</summary>
    public IReadOnlyList<string> Permisos { get; init; } = [];
}

/// <summary>
/// Lo que se puede cambiar de un rol: el nombre y la lista ENTERA de permisos.
/// </summary>
/// <remarks>
/// La lista va entera y no como altas y bajas sueltas: quien edita permisos ve una lista de
/// casillas, y si el borde tuviera que calcular la diferencia, un descuido dejaría concedido un
/// permiso que el formulario ya no mostraba. El código no se cambia: es contrato con la semilla.
/// </remarks>
public sealed record ModificarRolDto
{
    /// <summary>Nombre para la interfaz.</summary>
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede pasar de {1} caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Los permisos que el rol debe conceder, exactamente estos.</summary>
    public IReadOnlyList<string> Permisos { get; init; } = [];
}
