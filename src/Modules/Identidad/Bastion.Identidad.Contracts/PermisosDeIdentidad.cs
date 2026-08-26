namespace Bastion.Identidad.Contracts;

/// <summary>
/// Los permisos que declara el módulo Identidad, uno por <b>tipo × verbo</b>.
/// </summary>
/// <remarks>
/// <para>
/// Estos son los permisos que reparten los demás: quien puede conceder pertenencias y asignar
/// roles puede, en dos pasos, darse a sí mismo cualquier permiso que exista. Por eso están
/// separados hasta el detalle —conceder no es retirar, asignar un rol no es crear un rol— y por
/// eso el rol que los reúne es el único que la semilla marca como del sistema.
/// </para>
/// <para>
/// <b>No hay permiso para iniciar o renovar sesión.</b> Esas dos operaciones son anónimas por
/// definición: exigir un permiso para obtener el token con el que se demuestran los permisos es
/// un círculo. Lo que sí tienen es todo lo demás —tope de intentos, rotación y detección de
/// reutilización—.
/// </para>
/// </remarks>
public static class PermisosDeIdentidad
{
    /// <summary>Consultar usuarios.</summary>
    public const string UsuarioVer = "identidad.usuario.ver";

    /// <summary>Dar de alta un usuario. Es el registro: solo por invitación.</summary>
    public const string UsuarioCrear = "identidad.usuario.crear";

    /// <summary>Cambiar el nombre de un usuario.</summary>
    public const string UsuarioModificar = "identidad.usuario.modificar";

    /// <summary>Dar de baja una cuenta (R16).</summary>
    public const string UsuarioBloquear = "identidad.usuario.bloquear";

    /// <summary>Reactivar una cuenta dada de baja.</summary>
    public const string UsuarioDesbloquear = "identidad.usuario.desbloquear";

    /// <summary>Cambiarle la contraseña a OTRO usuario.</summary>
    /// <remarks>
    /// Cambiarse la propia no lleva permiso: para hacerlo hay que presentar la actual, que es la
    /// prueba de identidad. Cambiarle la contraseña a otro sí, porque es tomar su cuenta.
    /// </remarks>
    public const string UsuarioCambiarContrasena = "identidad.usuario.cambiar-contrasena";

    /// <summary>Consultar roles.</summary>
    public const string RolVer = "identidad.rol.ver";

    /// <summary>Crear un rol.</summary>
    public const string RolCrear = "identidad.rol.crear";

    /// <summary>Cambiar el nombre o los permisos de un rol.</summary>
    public const string RolModificar = "identidad.rol.modificar";

    /// <summary>Consultar las pertenencias de un usuario.</summary>
    public const string PertenenciaVer = "identidad.pertenencia.ver";

    /// <summary>Dar de alta a un usuario en una empresa.</summary>
    public const string PertenenciaConceder = "identidad.pertenencia.conceder";

    /// <summary>Dar de baja a un usuario de una empresa.</summary>
    public const string PertenenciaRetirar = "identidad.pertenencia.retirar";

    /// <summary>Asignar un rol a un usuario en una empresa.</summary>
    public const string PertenenciaAsignarRol = "identidad.pertenencia.asignar-rol";

    /// <summary>Retirarle un rol a un usuario en una empresa.</summary>
    /// <remarks>
    /// Separado de asignar por lo mismo que bloquear lo está de desbloquear: hacer y deshacer no
    /// son la misma facultad. Quien reparte roles no tiene por qué poder quitarle el suyo al
    /// administrador, y con un único permiso «gestionar roles» esa distinción no se puede ni
    /// expresar.
    /// </remarks>
    public const string PertenenciaRetirarRol = "identidad.pertenencia.retirar-rol";

    /// <summary>Todos los permisos del módulo, que es lo que el host junta en el catálogo.</summary>
    public static IReadOnlyList<string> Todos { get; } =
    [
        UsuarioVer,
        UsuarioCrear,
        UsuarioModificar,
        UsuarioBloquear,
        UsuarioDesbloquear,
        UsuarioCambiarContrasena,
        RolVer,
        RolCrear,
        RolModificar,
        PertenenciaVer,
        PertenenciaConceder,
        PertenenciaRetirar,
        PertenenciaAsignarRol,
        PertenenciaRetirarRol,
    ];
}
