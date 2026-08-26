using System.ComponentModel.DataAnnotations;

namespace Bastion.Identidad.Contracts.Usuarios;

/// <summary>Un usuario, tal como sale de la API.</summary>
/// <remarks>
/// <b>No lleva el resumen de la contraseña, ni un campo donde pudiera colarse.</b> Un DTO que
/// arrastrara el hash lo publicaría en cada listado, en cada caché de navegador y en cada registro
/// de una petición. Lo que sí lleva son los datos con los que se administra la cuenta.
/// </remarks>
/// <param name="Id">Identificador del usuario.</param>
/// <param name="Correo">Correo con el que inicia sesión, normalizado.</param>
/// <param name="Nombre">Nombre para la interfaz.</param>
/// <param name="Estado">Estado de la cuenta, como texto.</param>
/// <param name="BloqueadoEn">Cuándo se dio de baja, o nulo si está activa.</param>
/// <param name="CreadoEn">Cuándo se creó.</param>
/// <param name="UltimoAccesoEn">Último inicio de sesión correcto, o nulo si no ha habido.</param>
public sealed record UsuarioDto(
    Guid Id,
    string Correo,
    string Nombre,
    string Estado,
    DateTimeOffset? BloqueadoEn,
    DateTimeOffset CreadoEn,
    DateTimeOffset? UltimoAccesoEn);

/// <summary>Lo que hace falta para dar de alta un usuario.</summary>
/// <remarks>
/// <b>El alta la pide alguien que ya está dentro</b>, con el permiso
/// <c>identidad.usuario.crear</c> en la empresa de su <i>claim</i>: esto no es un auto-registro
/// abierto. La empresa en la que queda dado de alta sale del <i>claim</i>, no de aquí (R8).
/// </remarks>
public sealed record CrearUsuarioDto
{
    /// <summary>Correo con el que iniciará sesión.</summary>
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [StringLength(254, ErrorMessage = "El correo no puede pasar de {1} caracteres.")]
    public string Correo { get; init; } = string.Empty;

    /// <summary>Nombre para la interfaz.</summary>
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede pasar de {1} caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Contraseña inicial.</summary>
    /// <remarks>
    /// La longitud mínima está aquí y no solo en el dominio porque es una regla de FORMA: se puede
    /// contestar sin consultar nada, así que contestarla en el borde ahorra un viaje y devuelve un
    /// error por campo. El máximo existe porque el algoritmo de resumen recorre toda la entrada:
    /// sin tope, una contraseña de un megabyte es una denegación de servicio de una línea.
    /// </remarks>
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(128, MinimumLength = 12,
        ErrorMessage = "La contraseña tiene que medir entre {2} y {1} caracteres.")]
    public string Contrasena { get; init; } = string.Empty;
}

/// <summary>Lo que se puede cambiar de un usuario.</summary>
/// <remarks>
/// Sin el correo: es con lo que inicia sesión y con lo que se le identifica en el rastro de
/// auditoría, así que cambiarlo es dar de alta a otra persona en la misma fila.
/// </remarks>
public sealed record ModificarUsuarioDto
{
    /// <summary>Nombre para la interfaz.</summary>
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede pasar de {1} caracteres.")]
    public string Nombre { get; init; } = string.Empty;
}

/// <summary>Cambio de la propia contraseña.</summary>
/// <remarks>
/// Pide la actual, y esa es toda la autorización que hace falta: quien la sabe es el dueño de la
/// cuenta. Por eso esta operación no lleva permiso y cambiarle la contraseña a otro sí.
/// </remarks>
public sealed record CambiarContrasenaDto
{
    /// <summary>La contraseña de ahora.</summary>
    [Required(ErrorMessage = "La contraseña actual es obligatoria.")]
    [StringLength(128, ErrorMessage = "La contraseña no puede pasar de {1} caracteres.")]
    public string Actual { get; init; } = string.Empty;

    /// <summary>La contraseña nueva.</summary>
    [Required(ErrorMessage = "La contraseña nueva es obligatoria.")]
    [StringLength(128, MinimumLength = 12,
        ErrorMessage = "La contraseña tiene que medir entre {2} y {1} caracteres.")]
    public string Nueva { get; init; } = string.Empty;
}

/// <summary>Cambio de la contraseña de OTRO usuario, por quien tiene el permiso.</summary>
public sealed record RestablecerContrasenaDto
{
    /// <summary>La contraseña nueva.</summary>
    [Required(ErrorMessage = "La contraseña nueva es obligatoria.")]
    [StringLength(128, MinimumLength = 12,
        ErrorMessage = "La contraseña tiene que medir entre {2} y {1} caracteres.")]
    public string Nueva { get; init; } = string.Empty;
}
