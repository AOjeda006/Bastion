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
/// <param name="Empresas">Empresas a las que pertenece, con nombre, para el selector.</param>
/// <param name="Permisos">Permisos que tiene en la empresa activa, para la interfaz.</param>
public sealed record SesionDto(
    string TokenDeAcceso,
    DateTimeOffset ExpiraEn,
    Guid UsuarioId,
    string Nombre,
    Guid EmpresaActivaId,
    IReadOnlyList<EmpresaDeSesionDto> Empresas,
    IReadOnlyList<string> Permisos);

/// <summary>Una de las empresas entre las que se puede cambiar, tal como la pinta el selector.</summary>
/// <remarks>
/// <para>
/// <b>Lleva el nombre, y esa es toda la razón de que exista.</b> Hasta el 0.11 la sesión devolvía
/// una lista de identificadores, y con eso no se puede pintar un desplegable: nadie elige entre
/// <c>a3f1…</c> y <c>7c02…</c>. El nombre no puede salir de
/// <c>GET /api/v1/organizacion/empresas</c> porque ese endpoint exige el permiso
/// <c>organizacion.empresa.ver</c>, y pertenecer a varias empresas no implica poder ver la ficha de
/// ninguna: el usuario de almacén que trabaja para dos sociedades tiene que poder cambiar entre
/// ellas sin tener acceso al padrón.
/// </para>
/// <para>
/// Lleva el nombre y <b>nada más</b>. Ni NIF, ni domicilio, ni régimen: es una etiqueta de una
/// lista, no una ficha, y lo que no viaja no se filtra por descuido.
/// </para>
/// <para>
/// <b>Una empresa bloqueada no aparece aquí</b> aunque el usuario siga perteneciendo a ella. No es
/// un caso especial de este contrato: es el filtro de R16 en la consulta que lo puebla. Suprimir
/// al amparo del art. 32 y seguir ofreciéndola en un desplegable serían dos cosas incompatibles.
/// </para>
/// </remarks>
/// <param name="Id">Identificador de la empresa.</param>
/// <param name="RazonSocial">Razón social, o nombre del empresario individual.</param>
public sealed record EmpresaDeSesionDto(Guid Id, string RazonSocial);
