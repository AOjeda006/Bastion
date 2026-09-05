namespace Bastion.Terceros.Contracts;

/// <summary>
/// Los permisos que declara el módulo Terceros, uno por <b>tipo × verbo</b>.
/// </summary>
/// <remarks>
/// <para>
/// Mismo criterio que <c>PermisosDeOrganizacion</c>, y aquí muerde más: la ficha de un tercero
/// puede ser la de una persona física, con su nombre, su NIF y su domicilio. Quien puede consultar
/// el maestro de clientes no tiene por qué poder darlos de baja, y quien puede darlos de baja no
/// tiene por qué poder devolverlos a la operativa.
/// </para>
/// <para>
/// Son constantes y no un tipo, por lo mismo que en Organización: <c>Contracts</c> no referencia
/// nada, ni siquiera los bloques comunes de dominio, así que aquí no se puede usar <c>Permiso</c>.
/// La forma se comprueba al componer el catálogo, y una constante mal escrita <b>tumba el
/// arranque</b>.
/// </para>
/// </remarks>
public static class PermisosDeTerceros
{
    /// <summary>Consultar terceros.</summary>
    public const string TerceroVer = "terceros.tercero.ver";

    /// <summary>Dar de alta terceros.</summary>
    public const string TerceroCrear = "terceros.tercero.crear";

    /// <summary>Cambiar los datos de un tercero.</summary>
    public const string TerceroModificar = "terceros.tercero.modificar";

    /// <summary>Dar de baja a un tercero (R16, art. 32 de la LOPDGDD).</summary>
    public const string TerceroBloquear = "terceros.tercero.bloquear";

    /// <summary>Deshacer la baja de un tercero.</summary>
    /// <remarks>
    /// Separado de <see cref="TerceroBloquear"/> a propósito, y con más motivo que en ningún otro
    /// recurso: levantar el bloqueo de una ficha que se reservó porque alguien ejerció su derecho
    /// de supresión es devolver al tratamiento unos datos que la ley había sacado de él. Es la
    /// operación que hay que poder auditar.
    /// </remarks>
    public const string TerceroDesbloquear = "terceros.tercero.desbloquear";

    /// <summary>
    /// Todos los permisos del módulo, para que el <i>composition root</i> componga el catálogo.
    /// </summary>
    /// <remarks>
    /// A mano y no por reflexión sobre las constantes: la lista escrita es la que se compara
    /// entera contra lo que las acciones exigen, y una reflexión que se cuele por su cuenta
    /// convertiría «declarar un permiso» en algo que pasa solo.
    /// </remarks>
    public static IReadOnlyList<string> Todos { get; } =
    [
        TerceroVer,
        TerceroCrear,
        TerceroModificar,
        TerceroBloquear,
        TerceroDesbloquear,
    ];
}
