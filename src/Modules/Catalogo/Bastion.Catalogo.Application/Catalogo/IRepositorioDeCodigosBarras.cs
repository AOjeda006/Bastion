using Bastion.Catalogo.Domain.Catalogo;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>Acceso a los códigos de barras guardados: qué GTIN lleva cada artículo.</summary>
/// <remarks>
/// <para>
/// El puerto lo declara la capa que lo CONSUME y lo implementa Infrastructure
/// (`principios/clean-architecture.md`). Ninguno de sus métodos confirma nada: eso lo decide el
/// caso de uso a través de <see cref="IUnidadTrabajoDeCatalogo"/>.
/// </para>
/// <para>
/// <b>Sin paginar, como los proveedores de un artículo</b>: lo que cabe aquí son los códigos de UN
/// artículo, que son unos pocos —la base, alguna caja y algún palé—, no los de la empresa.
/// </para>
/// </remarks>
public interface IRepositorioDeCodigosBarras
{
    /// <summary>El código de barras con ese identificador, o nulo si no hay ninguno.</summary>
    Task<CodigoBarras?> ObtenerAsync(Guid id, CancellationToken cancelacion);

    /// <summary>Todos los códigos de barras de ese artículo, de la base a las agrupaciones.</summary>
    /// <param name="articuloId">Artículo del que se quieren los códigos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<IReadOnlyList<CodigoBarras>> DeArticuloAsync(Guid articuloId, CancellationToken cancelacion);

    /// <summary>El código de barras que lleva ese GTIN en la empresa, o nulo si no lo lleva ninguno.</summary>
    /// <remarks>
    /// <para>
    /// Hay uno como mucho, porque el índice único <c>(empresa_id, gtin)</c> no deja poner dos. Lo
    /// usan la búsqueda por GTIN y la comprobación previa del alta.
    /// </para>
    /// <para>
    /// <b>En el alta no sustituye al índice</b>: entre esta consulta y la escritura cabe otra
    /// transacción, y la base es la única que puede impedirlo cuando dos altas llegan a la vez. Esto
    /// se adelanta para contestar el mismo <c>409</c> sin llegar al motor (ADR-0051 §5).
    /// </para>
    /// </remarks>
    /// <param name="gtin">El GTIN ya leído, en su forma de catorce.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<CodigoBarras?> DelGtinAsync(Gtin gtin, CancellationToken cancelacion);

    /// <summary>Apunta un código de barras nuevo. No lo graba: eso lo hace la unidad de trabajo.</summary>
    void Agregar(CodigoBarras codigo);

    /// <summary>Apunta que un código de barras se va. No lo graba: eso lo hace la unidad de trabajo.</summary>
    /// <remarks>
    /// Borrado de verdad y no baja lógica (ADR-0051 §8). La empresa puede volver a dar de alta ese
    /// GTIN enseguida, en el mismo artículo o en otro: la regla de los 48 meses está derogada. El
    /// rastro de quién lo quitó y cuándo lo lleva la auditoría (ADR-0012).
    /// </remarks>
    void Eliminar(CodigoBarras codigo);
}
