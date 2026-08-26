using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Domain.Series;

namespace Bastion.Organizacion.Application.Series;

/// <summary>Acceso a las series de numeración guardadas.</summary>
public interface IRepositorioDeSeries
{
    /// <summary>La serie con ese identificador, o nulo si no hay ninguna.</summary>
    Task<Serie?> ObtenerAsync(Guid id, CancellationToken cancelacion);

    /// <summary>Indica si ese ejercicio de esa empresa ya tiene una serie con ese código.</summary>
    Task<bool> ExisteElCodigoAsync(Guid empresaId, Guid ejercicioId, string codigo, CancellationToken cancelacion);

    /// <summary>Una página de series, con el total.</summary>
    Task<PaginaDe<Serie>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion);

    /// <summary>Apunta una serie nueva.</summary>
    void Agregar(Serie serie);

    /// <summary>Marca una serie para que desaparezca al confirmar.</summary>
    void Eliminar(Serie serie);
}
