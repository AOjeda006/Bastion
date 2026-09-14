using Bastion.Catalogo.Domain.Catalogo;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>Acceso a los suministros guardados: qué tercero provee qué artículo.</summary>
/// <remarks>
/// <para>
/// El puerto lo declara la capa que lo CONSUME y lo implementa Infrastructure
/// (`principios/clean-architecture.md`). Ninguno de sus métodos confirma nada: eso lo decide el
/// caso de uso a través de <see cref="IUnidadTrabajoDeCatalogo"/>.
/// </para>
/// <para>
/// <b>Sin <c>IOrdenaPor</c> y sin <c>PaginaDe</c>, y es una decisión con motivo.</b> Lo que sale de
/// aquí hay que filtrarlo por el bloqueo del tercero al que apunta cada fila (art. 32 de la
/// LOPDGDD, R16), y ese filtro no cabe en el <c>WHERE</c>: el bloqueo vive en
/// <c>terceros.terceros</c> y ninguna consulta cruza esquemas (§5, regla 4). Solo se puede
/// preguntar por el puerto, y eso deja dos órdenes posibles:
/// </para>
/// <para>
/// <b>Paginar y después filtrar</b> devuelve páginas de tamaño variable y un total que miente —«20
/// proveedores» y quince filas—, y el total que miente es justo el dato que el art. 32 reserva:
/// restando se cuenta cuántos hay bloqueados. <b>Filtrar y después paginar</b> exige conocer el
/// conjunto entero antes de cortar la página, así que la página deja de ser una página.
/// </para>
/// <para>
/// Se elige lo segundo llevado al final: <b>el conjunto entero, sin paginar</b>. Se puede porque el
/// conjunto está acotado por el propio modelo —el índice único <c>(empresa, artículo, tercero)</c>
/// impide repetir, y lo que cabe aquí son los proveedores de UN artículo, no los de la empresa—.
/// Si algún día un artículo tuviera cientos de proveedores, la respuesta no sería paginar esto: es
/// que el listado que hace falta sería el de vuelta —los artículos de un proveedor—, y ese sí nace
/// paginado.
/// </para>
/// </remarks>
public interface IRepositorioDeProveedoresDeArticulo
{
    /// <summary>El suministro con ese identificador, o nulo si no hay ninguno.</summary>
    Task<ArticuloProveedor?> ObtenerAsync(Guid id, CancellationToken cancelacion);

    /// <summary>
    /// Todos los suministros de ese artículo, sin filtrar por el estado del tercero.
    /// </summary>
    /// <remarks>
    /// <b>Devuelve lo que hay guardado, y el filtro del art. 32 lo pone quien llama</b> —
    /// <c>ProveedoresDelArticulo</c>, que es quien tiene el puerto—. Repartirlo así es a propósito:
    /// el borrado de un suministro y el listado necesitan cosas distintas de esta consulta, y un
    /// repositorio que filtrara por su cuenta escondería filas a quien va a borrarlas.
    /// </remarks>
    /// <param name="articuloId">Artículo del que se quieren los proveedores.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<IReadOnlyList<ArticuloProveedor>> DeArticuloAsync(
        Guid articuloId, CancellationToken cancelacion);

    /// <summary>Indica si ese tercero ya consta como proveedor de ese artículo.</summary>
    /// <remarks>
    /// <b>No sustituye al índice único</b>, igual que la comprobación de solape no sustituye a la
    /// restricción de exclusión: la base es la única que puede impedirlo cuando dos peticiones
    /// llegan a la vez. Esto se adelanta para poder contestar un 409 con el motivo escrito en vez
    /// de dejar salir una violación de integridad convertida en 500.
    /// </remarks>
    /// <param name="articuloId">Artículo que se suministra.</param>
    /// <param name="terceroId">Quien lo suministraría.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<bool> YaLoSuministraAsync(
        Guid articuloId, Guid terceroId, CancellationToken cancelacion);

    /// <summary>Apunta un suministro nuevo. No lo graba: eso lo hace la unidad de trabajo.</summary>
    void Agregar(ArticuloProveedor suministro);

    /// <summary>Apunta que un suministro se va. No lo graba: eso lo hace la unidad de trabajo.</summary>
    /// <remarks>
    /// Borrado de verdad y no baja lógica, y aquí sí toca: lo que se borra es un <b>hecho entre
    /// dos</b> —«este tercero suministra esto»— y no una ficha. Un proveedor que deja de
    /// suministrar un artículo no deja rastro que haya que conservar; el rastro de quién lo quitó y
    /// cuándo lo lleva la auditoría, que es donde vive (ADR-0012).
    /// </remarks>
    void Eliminar(ArticuloProveedor suministro);
}
