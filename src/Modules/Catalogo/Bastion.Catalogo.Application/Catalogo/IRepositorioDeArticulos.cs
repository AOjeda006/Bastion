using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Catalogo.Domain.Catalogo;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>Acceso a los artículos guardados.</summary>
/// <remarks>
/// El puerto lo declara la capa que lo CONSUME y lo implementa Infrastructure
/// (`principios/clean-architecture.md`). Ninguno de sus métodos confirma nada: eso lo decide el
/// caso de uso a través de <see cref="IUnidadTrabajoDeCatalogo"/>.
/// </remarks>
public interface IRepositorioDeArticulos : IOrdenaPor
{
    /// <summary>El artículo con ese identificador, o nulo si no hay ninguno.</summary>
    Task<Articulo?> ObtenerAsync(Guid id, CancellationToken cancelacion);

    /// <summary>Indica si esa empresa ya tiene un artículo con ese código.</summary>
    /// <remarks>
    /// Se pregunta ANTES de insertar para poder contestar un <c>409</c> con explicación en vez de
    /// un <c>500</c> traído por el índice único. Y se pregunta por el código <b>ya normalizado</b>,
    /// que es la forma sobre la que está el índice: preguntando por lo que escribió el usuario, un
    /// código en minúsculas pasaría el filtro y chocaría después.
    /// </remarks>
    /// <param name="empresaId">Empresa a la que pertenecería la ficha (R8).</param>
    /// <param name="codigo">Código ya normalizado.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<bool> ExisteElCodigoAsync(Guid empresaId, string codigo, CancellationToken cancelacion);

    /// <summary>Una página de artículos, con el total.</summary>
    /// <remarks>
    /// <b>El filtro por categoría va aparte de la paginación</b> y no dentro de <c>?q=</c>: son dos
    /// preguntas distintas —«los que digan esto» y «los que estén aquí»— y mezclarlas obligaría a
    /// adivinar si un texto es un identificador. Es además el <b>consumidor real</b> que decidió
    /// el modelo del árbol: filtra por la categoría dicha y no por su subárbol, que es un descenso
    /// y no un ascenso.
    /// </remarks>
    /// <param name="paginacion">Qué página, de qué tamaño, con qué orden y con qué filtro.</param>
    /// <param name="categoriaId">Categoría por la que se acota, o nula para no acotar.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<PaginaDe<Articulo>> ListarAsync(
        Paginacion paginacion,
        Guid? categoriaId,
        CancellationToken cancelacion);

    /// <summary>Apunta un artículo nuevo. No lo graba: eso lo hace la unidad de trabajo.</summary>
    void Agregar(Articulo articulo);
}
