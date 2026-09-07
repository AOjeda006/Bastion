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

    /// <summary>Colgar un contacto de una ficha.</summary>
    /// <remarks>
    /// <b>Aparte de <see cref="TerceroModificar"/>, y no por simetría.</b> Los datos de contacto
    /// son de personas distintas del interesado de la ficha, y hay perfiles —comercial, compras—
    /// que necesitan mantener con quién se habla sin poder tocar la razón social ni el domicilio
    /// fiscal, que es lo que sale impreso en una factura.
    /// </remarks>
    public const string ContactoAgregar = "terceros.contacto.agregar";

    /// <summary>Quitar un contacto de una ficha.</summary>
    /// <remarks>
    /// Separado de <see cref="ContactoAgregar"/> por la misma razón que crear y modificar: aquí
    /// sí se borra la fila, y quien puede añadir a quién se llama no tiene por qué poder borrar
    /// el único teléfono que alguien había apuntado.
    /// </remarks>
    public const string ContactoQuitar = "terceros.contacto.quitar";

    /// <summary>Colgar una cuenta bancaria de una ficha.</summary>
    /// <remarks>
    /// <b>El permiso más delicado del módulo.</b> Cambiar el IBAN de un proveedor es exactamente
    /// lo que hace el fraude del falso cambio de cuenta, y quien mantiene el maestro de clientes
    /// no tiene por qué poder decidir a qué cuenta se paga.
    /// </remarks>
    public const string CuentaBancariaAgregar = "terceros.cuenta-bancaria.agregar";

    /// <summary>Elegir cuál de las cuentas de una ficha es la preferente.</summary>
    /// <remarks>
    /// Aparte de <see cref="CuentaBancariaAgregar"/> porque cambia a dónde va el dinero sin añadir
    /// nada: con solo este permiso, ya se decide por cuál de las cuentas ya guardadas se paga.
    /// </remarks>
    public const string CuentaBancariaPreferente = "terceros.cuenta-bancaria.preferente";

    /// <summary>Quitar una cuenta bancaria de una ficha.</summary>
    public const string CuentaBancariaQuitar = "terceros.cuenta-bancaria.quitar";

    /// <summary>Fijar la condición de pago de un papel.</summary>
    /// <remarks>
    /// Conceder plazo es conceder financiación, y no es la misma facultad que corregir un
    /// domicilio. El tope legal de sesenta días lo sostiene el dominio pase lo que pase con este
    /// permiso: no es lo que este permiso protege.
    /// </remarks>
    public const string CondicionPagoFijar = "terceros.condicion-pago.fijar";

    /// <summary>Fijar —o retirar— el límite de crédito de una ficha.</summary>
    /// <remarks>
    /// Decidir cuánto se le fía a alguien es una autoridad de riesgo, no de mantenimiento del
    /// maestro. Es el permiso que un perfil administrativo <b>no</b> lleva.
    /// </remarks>
    public const string LimiteCreditoFijar = "terceros.limite-credito.fijar";

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
        ContactoAgregar,
        ContactoQuitar,
        CuentaBancariaAgregar,
        CuentaBancariaPreferente,
        CuentaBancariaQuitar,
        CondicionPagoFijar,
        LimiteCreditoFijar,
    ];
}
