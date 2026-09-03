using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Domain.Almacenes;

namespace Bastion.Organizacion.Application.Almacenes;

/// <summary>Acceso a los almacenes guardados.</summary>
public interface IRepositorioDeAlmacenes : IOrdenaPor
{
    /// <summary>El almacén con ese identificador, o nulo si no hay ninguno.</summary>
    Task<Almacen?> ObtenerAsync(Guid id, CancellationToken cancelacion);

    /// <summary>Indica si esa empresa ya tiene un almacén con ese código.</summary>
    Task<bool> ExisteElCodigoAsync(Guid empresaId, string codigo, CancellationToken cancelacion);

    /// <summary>Una página de almacenes, con el total.</summary>
    Task<PaginaDe<Almacen>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion);

    /// <summary>Apunta un almacén nuevo.</summary>
    void Agregar(Almacen almacen);
}
