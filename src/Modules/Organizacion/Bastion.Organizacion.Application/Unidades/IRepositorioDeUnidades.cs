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

    /// <summary>Una página de conversiones, con el total.</summary>
    Task<PaginaDe<ConversionUM>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion);

    /// <summary>Apunta una conversión nueva.</summary>
    void Agregar(ConversionUM conversion);
}
