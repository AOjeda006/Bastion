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
    ];
}
