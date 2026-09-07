using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Domain.Unidades;

namespace Bastion.Organizacion.Application.Unidades;

/// <summary>Acceso a las unidades de medida guardadas.</summary>
public interface IRepositorioDeUnidadesDeMedida : IOrdenaPor
{
    /// <summary>La unidad con ese identificador, o nula si no hay ninguna.</summary>
    Task<UnidadMedida?> ObtenerAsync(Guid id, CancellationToken cancelacion);

    /// <summary>Indica si ya hay una unidad con ese código.</summary>
    /// <param name="codigo">Código ya normalizado.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<bool> ExisteElCodigoAsync(string codigo, CancellationToken cancelacion);

    /// <summary>Indica si existen todas las unidades de la lista, en una sola consulta.</summary>
    Task<bool> ExistenAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancelacion);

    /// <summary>Una página de unidades, con el total.</summary>
    Task<PaginaDe<UnidadMedida>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion);

    /// <summary>Apunta una unidad nueva.</summary>
    void Agregar(UnidadMedida unidad);
}

/// <summary>Acceso a las conversiones entre unidades guardadas.</summary>
public interface IRepositorioDeConversiones : IOrdenaPor
{
    /// <summary>La conversión con ese identificador, o nula si no hay ninguna.</summary>
    Task<ConversionUM?> ObtenerAsync(Guid id, CancellationToken cancelacion);

    /// <summary>Indica si ese par de unidades ya tiene conversión.</summary>
    Task<bool> ExisteAsync(Guid unidadOrigenId, Guid unidadDestinoId, CancellationToken cancelacion);

    /// <summary>La conversión declarada para ese par, o nula si no hay ninguna.</summary>
    /// <remarks>
    /// <para>
    /// <b>Trae las retiradas también</b>, y eso es la decisión 2 del ítem 1.7 traducida a una
    /// consulta. Sus dos clientes la necesitan así: el resolutor, porque una fila retirada «sigue
    /// resolviendo» —es literalmente lo que sigue haciendo—; y la comprobación de la inversa,
    /// porque si retirar una fila la sacara de la comprobación, retirar el sentido incómodo sería
    /// la manera de declarar cualquier número en el otro.
    /// </para>
    /// <para>
    /// El par es la identidad de la fila —hay un índice único sobre él—, así que devuelve una o
    /// ninguna, no una lista.
    /// </para>
    /// </remarks>
    /// <param name="unidadOrigenId">Unidad de la que se parte.</param>
    /// <param name="unidadDestinoId">Unidad a la que se llega.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<ConversionUM?> DelParAsync(
        Guid unidadOrigenId,
        Guid unidadDestinoId,
        CancellationToken cancelacion);

    /// <summary>Una página de conversiones, con el total.</summary>
    Task<PaginaDe<ConversionUM>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion);

    /// <summary>Apunta una conversión nueva.</summary>
    void Agregar(ConversionUM conversion);
}
