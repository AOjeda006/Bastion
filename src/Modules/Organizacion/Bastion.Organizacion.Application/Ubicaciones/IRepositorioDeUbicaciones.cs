using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Domain.Ubicaciones;

namespace Bastion.Organizacion.Application.Ubicaciones;

/// <summary>Acceso a las ubicaciones guardadas.</summary>
public interface IRepositorioDeUbicaciones : IOrdenaPor
{
    /// <summary>La ubicación con ese identificador, o nula si no hay ninguna.</summary>
    Task<Ubicacion?> ObtenerAsync(Guid id, CancellationToken cancelacion);

    /// <summary>Indica si ese almacén ya tiene una ubicación con ese código.</summary>
    /// <param name="almacenId">Almacén en el que se busca.</param>
    /// <param name="codigo">Código ya normalizado.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<bool> ExisteElCodigoAsync(Guid almacenId, string codigo, CancellationToken cancelacion);

    /// <summary>Una página de ubicaciones, con el total.</summary>
    Task<PaginaDe<Ubicacion>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion);

    /// <summary>Apunta una ubicación nueva.</summary>
    void Agregar(Ubicacion ubicacion);
}
