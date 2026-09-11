using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Catalogo.Domain.Catalogo;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>
/// Un peldaño del ascenso por el árbol: quién es y de quién cuelga.
/// </summary>
/// <remarks>
/// <b>No es la categoría entera, y ahí está el motivo de que exista este tipo.</b> El recorrido de
/// <c>ElArbolSigueSiendoUnArbol</c> hace una consulta por nivel, y traer el agregado completo en
/// cada una lo metería además en el rastreador de EF Core: hasta once entidades rastreadas por
/// alta que nadie va a modificar, y que en la modificación competirían con la que sí se está
/// cambiando. Se leen las tres columnas que el ascenso usa y ninguna más.
/// </remarks>
/// <param name="Id">Identificador de la categoría.</param>
/// <param name="PadreId">De quién cuelga, o nulo si es una raíz.</param>
/// <param name="Codigo">Su código, para poder contar la cadena en el mensaje de error.</param>
public sealed record EslabonDeCategoria(Guid Id, Guid? PadreId, string Codigo);

/// <summary>Acceso a las categorías guardadas.</summary>
/// <remarks>
/// El puerto lo declara la capa que lo CONSUME y lo implementa Infrastructure
/// (`principios/clean-architecture.md`). Ninguno de sus métodos confirma nada: eso lo decide el
/// caso de uso a través de <see cref="IUnidadTrabajoDeCatalogo"/>.
/// </remarks>
public interface IRepositorioDeCategorias : IOrdenaPor
{
    /// <summary>La categoría con ese identificador, o nula si no hay ninguna.</summary>
    Task<Categoria?> ObtenerAsync(Guid id, CancellationToken cancelacion);

    /// <summary>
    /// El peldaño de esa categoría, o nulo si no existe <b>en esta empresa</b>.
    /// </summary>
    /// <remarks>
    /// El «en esta empresa» no lo pone esta consulta: lo pone el filtro de inquilinato del
    /// contexto (R8). Repetirlo aquí haría creer que la decisión vive en esta línea, de donde se
    /// puede caer olvidándola en la siguiente.
    /// </remarks>
    /// <param name="id">Identificador de la categoría.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<EslabonDeCategoria?> EslabonAsync(Guid id, CancellationToken cancelacion);

    /// <summary>
    /// La ascendencia de esa categoría: ella misma la primera, y después sus padres hasta la raíz.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>UNA consulta, sea cual sea la profundidad</b>, y esa es toda la razón de que este método
    /// exista al lado de <see cref="EslabonAsync"/> en vez de reutilizarlo en un bucle. El árbol de
    /// categorías de la empresa se trae <b>plano</b> —dos columnas, sin rastrear— y el ascenso se
    /// recorre en memoria. El número de viajes a la base <b>no crece con la profundidad</b>.
    /// </para>
    /// <para>
    /// <b>Por qué no se reutiliza el ascenso de <c>ElArbolSigueSiendoUnArbol</c></b>, que ya existe,
    /// está acotado y está probado: aquel gasta <b>una consulta por nivel</b> y corre al colgar una
    /// categoría, que pasa poco y una vez. Esto corre en <b>cada resolución de precio</b>, y un
    /// documento de cuarenta líneas serían cuarenta ascensos por hasta once niveles — cuatrocientas
    /// cuarenta consultas, todas rapidísimas por separado, ninguna lenta, ninguna señalando nada.
    /// Es un N+1 por construcción. La forma del bucle no se hereda porque lo que decide no es el
    /// algoritmo: es <b>dónde corre</b>.
    /// </para>
    /// <para>
    /// <b>Y por qué el árbol plano y no un <c>WITH RECURSIVE</c> en un viaje</b>, que era la otra
    /// salida y en una base de datos es la respuesta de libro. Porque en este proyecto un
    /// <c>WITH RECURSIVE</c> es SQL crudo, y el SQL crudo <b>no pasa por el traductor de consultas,
    /// así que el filtro de empresa no se le aplica</b> (0.6). Las dos únicas excepciones vivas
    /// están autorizadas por un argumento que aquí no vale y que ellas mismas dejan escrito: no
    /// leen ninguna fila. Una consulta recursiva sobre <c>categorias</c> lee filas, y son filas de
    /// una tabla por empresa — exactamente el agujero que la prohibición nombra. La alternativa
    /// pasa por EF Core con el filtro puesto y cuesta una consulta de dos columnas sobre las
    /// categorías de <b>una</b> empresa, que son cientos y no millones: un árbol de clasificación
    /// con once niveles de tope no es una tabla de movimientos.
    /// </para>
    /// <para>
    /// <b>El recorrido va acotado</b>, y por lo mismo que el de <c>ElArbolSigueSiendoUnArbol</c>:
    /// sobre datos que ya tuvieran un ciclo —una restauración a medias, un <c>UPDATE</c> a mano— un
    /// ascenso sin cota no da error, <b>gira</b>. Con el árbol en memoria gira en el proceso y no
    /// en el servidor de base de datos, que es la única ventaja que tiene girar.
    /// </para>
    /// </remarks>
    /// <param name="categoriaId">Categoría desde la que se sube.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>
    /// La cadena desde la categoría dada hasta la raíz, o vacía si esa categoría no existe en esta
    /// empresa. La posición en la lista <b>es el nivel</b>: <c>0</c> es ella misma, <c>1</c> su
    /// madre.
    /// </returns>
    Task<IReadOnlyList<Guid>> AscendenciaAsync(Guid categoriaId, CancellationToken cancelacion);

    /// <summary>Indica si esa empresa ya tiene una categoría con ese código.</summary>
    /// <param name="empresaId">Empresa a la que pertenecería (R8).</param>
    /// <param name="codigo">Código ya normalizado.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<bool> ExisteElCodigoAsync(Guid empresaId, string codigo, CancellationToken cancelacion);

    /// <summary>Una página de categorías, con el total.</summary>
    Task<PaginaDe<Categoria>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion);

    /// <summary>Apunta una categoría nueva. No la graba: eso lo hace la unidad de trabajo.</summary>
    void Agregar(Categoria categoria);
}
