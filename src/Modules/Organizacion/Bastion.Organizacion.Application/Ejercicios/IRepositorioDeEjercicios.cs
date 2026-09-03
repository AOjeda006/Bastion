using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Domain.Ejercicios;

namespace Bastion.Organizacion.Application.Ejercicios;

/// <summary>Acceso a los ejercicios guardados.</summary>
public interface IRepositorioDeEjercicios : IOrdenaPor
{
    /// <summary>El ejercicio con ese identificador, o nulo si no hay ninguno.</summary>
    Task<Ejercicio?> ObtenerAsync(Guid id, CancellationToken cancelacion);

    /// <summary>Indica si esa empresa ya tiene un ejercicio con ese año.</summary>
    Task<bool> ExisteElAnioAsync(Guid empresaId, int anio, CancellationToken cancelacion);

    /// <summary>Indica si existe el ejercicio, sin traérselo entero.</summary>
    Task<bool> ExisteAsync(Guid id, CancellationToken cancelacion);

    /// <summary>Indica si el ejercicio tiene series colgando.</summary>
    /// <remarks>
    /// Vive en este repositorio y no en el de series porque quien pregunta es el caso de uso que
    /// borra un ejercicio: lo que necesita saber es si el ejercicio se puede borrar, no qué series
    /// hay. La consulta la resuelve la misma unidad de persistencia, así que no cruza módulo.
    /// </remarks>
    Task<bool> TieneSeriesAsync(Guid id, CancellationToken cancelacion);

    /// <summary>Una página de ejercicios, con el total.</summary>
    Task<PaginaDe<Ejercicio>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion);

    /// <summary>Apunta un ejercicio nuevo.</summary>
    void Agregar(Ejercicio ejercicio);

    /// <summary>Marca un ejercicio para que desaparezca al confirmar.</summary>
    void Eliminar(Ejercicio ejercicio);
}
