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
/// <b>Hay una constante por acción, y eso no es una lista a medias</b>: las dos del ajuste, desde
/// el 2.11 las tres de la transferencia, y desde el 2.12 las del recuento, que entran con la acción
/// que las pide. El catálogo se compara entero contra lo que las acciones exigen, así que un permiso
/// declarado sin acción que lo pida <b>tumba el arranque</b>. Declarar aquí el alta, el listado o la
/// ficha del ajuste o de la transferencia —que llegan con sus pantallas— sería repartir casillas
/// que un administrador concede creyendo que abren algo.
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

    /// <summary>Enviar una transferencia entre almacenes.</summary>
    /// <remarks>
    /// <b>Uno por acción, porque quien envía y quien recibe suelen ser personas distintas, en
    /// almacenes distintos</b> (ADR-0053 §9). Enviar saca la mercancía del origen y gasta el
    /// correlativo de su serie (R5).
    /// </remarks>
    public const string TransferenciaEnviar = "inventario.transferencia.enviar";

    /// <summary>Recibir entera una transferencia enviada.</summary>
    /// <remarks>
    /// <b>Es lo que hace el almacén de destino</b>: mete lo que estaba en vuelo, con el valor que
    /// salió del origen. No numera nada, pero escribe el libro (R13).
    /// </remarks>
    public const string TransferenciaRecibir = "inventario.transferencia.recibir";

    /// <summary>Anular una transferencia enviada o recibida oponiéndole un inverso.</summary>
    /// <remarks>
    /// <b>Por lo mismo que en el ajuste</b>: anular gasta otro correlativo, escribe otra tanda de
    /// filas del libro y deja sin efecto lo que otros dieron por bueno.
    /// </remarks>
    public const string TransferenciaAnular = "inventario.transferencia.anular";

    /// <summary>Ver los recuentos: el listado, la ficha y sus líneas.</summary>
    /// <remarks>
    /// <b>Es el primer permiso de lectura del módulo</b>, porque el recuento es su primera pantalla
    /// (ADR-0055 §12). Ver lo que dice el libro de un almacén entero no es contar: hay perfiles que
    /// revisan un recuento y no lo tocan.
    /// </remarks>
    public const string RecuentoVer = "inventario.recuento.ver";

    /// <summary>Abrir el recuento de un almacén.</summary>
    /// <remarks>
    /// <b>Abrir no es confirmar</b>: el alta no mueve nada ni gasta ningún número, pero deja el
    /// almacén con un recuento en curso, y solo puede haber uno (ADR-0055 §1.7).
    /// </remarks>
    public const string RecuentoAbrir = "inventario.recuento.abrir";

    /// <summary>Contar las líneas de un recuento en curso.</summary>
    /// <remarks>
    /// <b>Es lo que hace quien está en el almacén</b>, y no mueve el libro: lo contado se queda en el
    /// recuento hasta que alguien con el permiso de confirmar lo convierte en un ajuste.
    /// </remarks>
    public const string RecuentoContar = "inventario.recuento.contar";

    /// <summary>Añadir a un recuento en curso una clave que la precarga no traía.</summary>
    /// <remarks>
    /// <b>No va con contar, porque cambia qué se cuenta y no cuánto</b> (ADR-0056): una clave nueva
    /// entra en el ajuste con su coste, y hay perfiles que cuentan lo que se les pone delante y no
    /// deciden qué entra en el recuento. Con un solo permiso, conceder lo primero regalaría lo
    /// segundo.
    /// </remarks>
    public const string RecuentoAgregarLinea = "inventario.recuento.agregar-linea";

    /// <summary>Quitar de un recuento en curso una línea que no se va a contar.</summary>
    /// <remarks>
    /// <b>Por lo mismo que añadir, y aparte de él</b> (ADR-0056): quitar una línea deja su clave como
    /// está (ADR-0055 §5), que es decidir que el recuento no dice nada de ella.
    /// </remarks>
    public const string RecuentoQuitarLinea = "inventario.recuento.quitar-linea";

    /// <summary>Confirmar un recuento en curso: numerarlo y mover su diferencia con un ajuste.</summary>
    /// <remarks>
    /// <b>Es el que mueve el libro</b>: gasta dos correlativos y deja el físico de cada clave contada
    /// en lo contado. Quien cuenta no tiene por qué poder dar lo contado por bueno.
    /// </remarks>
    public const string RecuentoConfirmar = "inventario.recuento.confirmar";

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
        TransferenciaEnviar,
        TransferenciaRecibir,
        TransferenciaAnular,
        RecuentoVer,
        RecuentoAbrir,
        RecuentoContar,
        RecuentoAgregarLinea,
        RecuentoQuitarLinea,
        RecuentoConfirmar,
    ];
}
