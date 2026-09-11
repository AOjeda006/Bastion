namespace Bastion.Catalogo.Contracts;

/// <summary>
/// Los permisos que declara el módulo Catálogo, uno por <b>tipo × verbo</b>.
/// </summary>
/// <remarks>
/// <para>
/// Mismo criterio que <c>PermisosDeOrganizacion</c> y <c>PermisosDeTerceros</c>. Aquí lo que
/// separa consultar de mantener es que <b>quien vende consulta el catálogo entero todo el día</b>
/// y no tiene por qué poder cambiar la unidad en la que se cuenta un artículo ni el impuesto con
/// el que se factura.
/// </para>
/// <para>
/// <b>Sin permiso de borrado, y no por olvido:</b> este módulo no publica ningún <c>DELETE</c> de
/// artículo ni de categoría. Un permiso que no protege ninguna acción es una casilla que alguien
/// concede en un perfil creyendo que hace algo, y el catálogo de permisos se compara entero contra
/// lo que las acciones exigen — así que declararlo pondría rojo el arranque.
/// </para>
/// <para>
/// Son constantes y no un tipo, por lo mismo que en los otros dos módulos: <c>Contracts</c> no
/// referencia nada, así que aquí no se puede usar <c>Permiso</c>. La forma se comprueba al
/// componer el catálogo, y una constante mal escrita <b>tumba el arranque</b>.
/// </para>
/// </remarks>
public static class PermisosDeCatalogo
{
    /// <summary>Consultar artículos.</summary>
    public const string ArticuloVer = "catalogo.articulo.ver";

    /// <summary>Dar de alta artículos.</summary>
    public const string ArticuloCrear = "catalogo.articulo.crear";

    /// <summary>Cambiar los datos de un artículo.</summary>
    /// <remarks>
    /// Cubre también cambiar el impuesto por defecto y la categoría, que es todo lo que de un
    /// artículo se puede cambiar. Ni el código ni la unidad base están entre ellos: no los protege
    /// un permiso, los protege que el agregado no tiene por dónde cambiarlos.
    /// </remarks>
    public const string ArticuloModificar = "catalogo.articulo.modificar";

    /// <summary>Consultar categorías.</summary>
    public const string CategoriaVer = "catalogo.categoria.ver";

    /// <summary>Dar de alta categorías.</summary>
    public const string CategoriaCrear = "catalogo.categoria.crear";

    /// <summary>Cambiar el nombre de una categoría o moverla de sitio en el árbol.</summary>
    /// <remarks>
    /// <b>Es el permiso que decide sobre la forma del árbol</b>, no sobre un nombre. Mover una
    /// rama recoloca todo lo que cuelga de ella, y en el ítem 1.9 eso cambiará qué tarifa se le
    /// aplica a cada artículo de esa rama por la precedencia de la categoría más cercana. Quien
    /// mantiene descripciones no tiene por qué poder reordenar la clasificación.
    /// </remarks>
    public const string CategoriaModificar = "catalogo.categoria.modificar";

    /// <summary>Consultar tarifas y resolver el precio de un artículo.</summary>
    /// <remarks>
    /// <b>Un permiso para las dos cosas, y hay que decir por qué.</b> Resolver un precio devuelve
    /// menos que leer la tarifa entera —un número, no la tabla—, así que un permiso propio parecería
    /// más fino. Sería falso: quien puede resolver puede preguntar artículo por artículo y
    /// reconstruir la tabla en una tarde, así que separarlos daría una sensación de control que el
    /// mecanismo no sostiene. Un permiso que no protege lo que parece proteger es peor que no
    /// tenerlo, porque alguien lo concede creyendo que sí.
    /// </remarks>
    public const string TarifaVer = "catalogo.tarifa.ver";

    /// <summary>Dar de alta tramos de tarifa.</summary>
    public const string TarifaCrear = "catalogo.tarifa.crear";

    /// <summary>Cambiar el nombre de un tramo de tarifa.</summary>
    /// <remarks>
    /// <b>Es el más flojo de los cuatro de tarifas, y por eso está solo.</b> El agregado no deja
    /// cambiar ni el código, ni la divisa, ni la vigencia, así que lo único que abre este permiso
    /// es corregir un nombre: no mueve un solo número. Quien mantiene el rotulado del catálogo no
    /// tiene por qué poder decidir precios, y hasta el ítem 1.9 esta constante cubría también
    /// cerrar tramos y mantener líneas, que sí los deciden.
    /// </remarks>
    public const string TarifaModificar = "catalogo.tarifa.modificar";

    /// <summary>Cerrar un tramo de tarifa, que es lo que hace entrar al siguiente.</summary>
    /// <remarks>
    /// <b>Aparte de <see cref="TarifaModificar"/>, como <c>ImpuestoCerrar</c> lo está del suyo.</b>
    /// Cerrar no edita nada de lo que hay escrito: cambia hasta cuándo rige, y por tanto qué
    /// precios se aplican a partir de mañana sin tocar un solo precio. Es la operación con la que
    /// una lista entera deja de valer, y quien corrige nombres no tiene por qué poder ejecutarla.
    /// </remarks>
    public const string TarifaCerrar = "catalogo.tarifa.cerrar";

    /// <summary>Poner una línea de precio en un tramo de tarifa.</summary>
    /// <remarks>
    /// <b>Es el permiso que decide precios</b>, y el que de verdad mueve dinero en este módulo:
    /// una línea nueva pone el precio de un artículo o de una rama entera del árbol. Separado de
    /// <see cref="TarifaCrear"/> por lo mismo que en todas partes —abrir una lista vacía no es
    /// llenarla— y separado de <see cref="LineaTarifaModificar"/> porque hay perfiles que
    /// mantienen la tabla que ya existe sin poder añadirle destinos ni tramos.
    /// </remarks>
    public const string LineaTarifaAgregar = "catalogo.linea-tarifa.agregar";

    /// <summary>Cambiar el precio o el descuento de una línea de tarifa.</summary>
    /// <remarks>
    /// Lo único que se puede cambiar de una línea es su precio o su descuento: ni el destino ni la
    /// cantidad desde la que se aplica están en el cuerpo, y no los protege este permiso, los
    /// protege que el agregado no tiene por dónde cambiarlos. Que la cantidad sea inmutable es,
    /// además, lo que hace imposible abrir un hueco en una tabla de precios ya escrita.
    /// </remarks>
    public const string LineaTarifaModificar = "catalogo.linea-tarifa.modificar";

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
        ArticuloVer,
        ArticuloCrear,
        ArticuloModificar,
        CategoriaVer,
        CategoriaCrear,
        CategoriaModificar,
        TarifaVer,
        TarifaCrear,
        TarifaModificar,
        TarifaCerrar,
        LineaTarifaAgregar,
        LineaTarifaModificar,
    ];
}
