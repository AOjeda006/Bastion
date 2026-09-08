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
