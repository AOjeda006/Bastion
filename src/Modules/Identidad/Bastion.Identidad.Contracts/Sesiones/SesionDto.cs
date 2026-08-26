using System.ComponentModel.DataAnnotations;

namespace Bastion.Identidad.Contracts.Sesiones;

/// <summary>Lo que hace falta para iniciar sesión.</summary>
/// <remarks>
/// Puede traer la empresa con la que se quiere empezar. Si no la trae, se activa la primera a la
/// que el usuario pertenece; si la trae y no pertenece, el intento falla igual que cualquier otro
/// —sin decir cuál de las dos cosas ha fallado—.
/// </remarks>
public sealed record IniciarSesionDto
{
    /// <summary>Correo con el que se identifica.</summary>
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [StringLength(254, ErrorMessage = "El correo no puede pasar de {1} caracteres.")]
    public string Correo { get; init; } = string.Empty;

    /// <summary>Su contraseña.</summary>
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(128, ErrorMessage = "La contraseña no puede pasar de {1} caracteres.")]
    public string Contrasena { get; init; } = string.Empty;

    /// <summary>Con qué empresa quiere empezar, si tiene varias.</summary>
    public Guid? EmpresaId { get; init; }
}

/// <summary>Con qué empresa se sigue operando.</summary>
public sealed record CambiarEmpresaDto
{
    /// <summary>Empresa que pasa a ser la activa.</summary>
    [Required(ErrorMessage = "La empresa es obligatoria.")]
    public Guid EmpresaId { get; init; }
}

/// <summary>
/// Una sesión abierta: el token de acceso y con qué se está operando.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aquí no está el token de refresco, y esa ausencia es la mitad del diseño.</b> El de refresco
/// viaja en una cookie <c>HttpOnly</c>, <c>Secure</c> y <c>SameSite=Lax</c>, donde el JavaScript
/// del navegador no lo puede leer. Si volviera en este cuerpo, el frontal tendría que guardarlo
/// en algún sitio, y cualquier sitio al que llegue el JavaScript llega también un XSS. El de
/// acceso sí vuelve aquí porque el frontal lo guarda <b>en memoria</b> y dura quince minutos
/// (§11).
/// </para>
/// <para>
/// <b>Los permisos vienen en la respuesta, y no son la autorización.</b> Están para que la
/// interfaz sepa qué botones enseñar: «la interfaz oculta, el servidor decide» (§11). Quien
/// autoriza es la política del servidor, que lee los permisos del token, no esta lista.
/// </para>
/// </remarks>
/// <param name="TokenDeAcceso">JWT corto, que el frontal guarda en memoria.</param>
/// <param name="ExpiraEn">Cuándo caduca el token de acceso.</param>
/// <param name="UsuarioId">Quién ha iniciado sesión.</param>
/// <param name="Nombre">Su nombre, para la interfaz.</param>
/// <param name="EmpresaActivaId">Empresa con la que se está operando (R8).</param>
/// <param name="Empresas">Empresas a las que pertenece, para el selector.</param>
/// <param name="Permisos">Permisos que tiene en la empresa activa, para la interfaz.</param>
public sealed record SesionDto(
    string TokenDeAcceso,
    DateTimeOffset ExpiraEn,
    Guid UsuarioId,
    string Nombre,
    Guid EmpresaActivaId,
    IReadOnlyList<Guid> Empresas,
    IReadOnlyList<string> Permisos);
