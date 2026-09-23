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
    /// <remarks>
    /// Pregunta por la <b>etiqueta</b>, no por las fechas, y por eso no basta: dos ejercicios de
    /// años distintos —2026 de enero a diciembre y 2027 de julio de 2026 a junio de 2027— pasan
    /// por aquí sin rozarse y se solapan seis meses. Quien mira las fechas es
    /// <see cref="HaySolapeAsync"/>.
    /// </remarks>
    Task<bool> ExisteElAnioAsync(Guid empresaId, int anio, CancellationToken cancelacion);

    /// <summary>
    /// Indica si esa empresa ya tiene un ejercicio cuyo intervalo se pisa con el que se le pasa.
    /// </summary>
    /// <remarks>
    /// <b>No sustituye a la restricción de la base</b> —un <c>EXCLUDE USING gist</c> sobre la
    /// empresa y el rango de fechas—, que es la única que puede impedirlo cuando dos peticiones
    /// llegan a la vez y la única que cubre los caminos de escritura que no pasan por aquí. Esto
    /// se adelanta para poder contestar un 409 con el motivo escrito en vez de dejar salir una
    /// violación de integridad convertida en 500. Es el mismo reparto que en los tramos de tarifa
    /// y en los de impuesto del 0.15, y se escribe igual para que se lea igual.
    /// </remarks>
    /// <param name="empresaId">Empresa a la que pertenece (R8).</param>
    /// <param name="inicio">Primer día del intervalo que se quiere ocupar.</param>
    /// <param name="fin">Último día del intervalo, incluido.</param>
    /// <param name="excepto">
    /// Ejercicio que no cuenta, para poder comprobar uno contra los demás. Sin él, modificar un
    /// ejercicio sin moverlo se encontraría solapado consigo mismo.
    /// </param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<bool> HaySolapeAsync(
        Guid empresaId,
        DateOnly inicio,
        DateOnly fin,
        Guid? excepto,
        CancellationToken cancelacion);

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
