namespace Bastion.Inventario.Contracts;

/// <summary>
/// Los permisos que declara el módulo Inventario, uno por <b>tipo × verbo</b>.
/// </summary>
/// <remarks>
/// <para>
/// Mismo criterio que <c>PermisosDeOrganizacion</c>, <c>PermisosDeTerceros</c> y
/// <c>PermisosDeCatalogo</c>. Lo que separa aquí las facultades es que <b>confirmar un ajuste
/// mueve existencias y gasta un número de serie</b>, y ninguna de las dos cosas se deshace: el
/// ítem 2.5 no borra un ajuste confirmado, le opone un contra-documento.
/// </para>
/// <para>
/// <b>Hay DOS constantes porque hay DOS acciones, y eso no es una lista a medias.</b> El catálogo
/// se compara entero contra lo que las acciones exigen, así que un permiso declarado sin acción
/// que lo pida <b>tumba el arranque</b>. Declarar aquí el alta, el listado o la ficha del ajuste
/// —que llegan con sus pantallas— sería repartir casillas que un administrador concede creyendo
/// que abren algo.
/// </para>
/// <para>
/// Son constantes y no un tipo, por lo mismo que en los otros tres módulos: <c>Contracts</c> no
/// referencia nada, así que aquí no se puede usar <c>Permiso</c>. La forma se comprueba al
/// componer el catálogo, y una constante mal escrita <b>tumba el arranque</b>.
/// </para>
/// </remarks>
public static class PermisosDeInventario
{
    /// <summary>Confirmar un ajuste de inventario.</summary>
    /// <remarks>
    /// <b>Es la facultad de mover el libro, no la de rellenar un papel.</b> Abrir un borrador y
    /// ponerle líneas no cambia ni una existencia; confirmarlo escribe los movimientos (R1, R13),
    /// consume el correlativo de su serie (R5) y deja el documento fuera del alcance de cualquier
    /// edición. Por eso cuando el alta llegue irá con permiso propio: hay perfiles que preparan
    /// regularizaciones y no las cierran.
    /// </remarks>
    public const string AjusteConfirmar = "inventario.ajuste.confirmar";

    /// <summary>Anular un ajuste de inventario oponiéndole un contra-documento.</summary>
    /// <remarks>
    /// <b>Anular no es confirmar, y por eso no comparte permiso.</b> Hay perfiles que cierran
    /// regularizaciones a diario y no deshacen ninguna: quien anula gasta otro correlativo de la
    /// serie (R5), escribe otra tanda de filas en el libro (R13) y deja sin efecto un documento
    /// que alguien dio por bueno. Con un solo permiso, conceder lo primero regalaría lo segundo.
    /// </remarks>
    public const string AjusteAnular = "inventario.ajuste.anular";

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
        AjusteConfirmar,
        AjusteAnular,
    ];
}
