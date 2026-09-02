using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Domain.Impuestos;

namespace Bastion.Organizacion.Application.Impuestos;

/// <summary>Acceso a los tramos de tipo impositivo guardados.</summary>
public interface IRepositorioDeImpuestos
{
    /// <summary>El tramo con ese identificador, o nulo si no hay ninguno.</summary>
    Task<Impuesto?> ObtenerAsync(Guid id, CancellationToken cancelacion);

    /// <summary>
    /// Indica si ya hay un tramo de ese código cuya vigencia se pisa con la que se le pasa.
    /// </summary>
    /// <remarks>
    /// <b>La restricción de verdad está en la base</b> —un <c>EXCLUDE USING gist</c> sobre el
    /// código y el rango de fechas—, y esta consulta no la sustituye: la base es la única que
    /// puede impedirlo cuando dos peticiones llegan a la vez. Esto se adelanta para poder
    /// contestar un 409 con el motivo escrito en vez de dejar que salga una violación de
    /// integridad convertida en 500, que es lo mismo que hace `EliminarEjercicio` con sus series.
    /// </remarks>
    /// <param name="codigo">Código ya normalizado.</param>
    /// <param name="desde">Primer día del tramo que se quiere abrir.</param>
    /// <param name="hasta">Último día, o nulo si queda abierto.</param>
    /// <param name="excepto">Tramo que no cuenta, para poder comprobar uno contra los demás.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<bool> HaySolapeAsync(
        string codigo,
        DateOnly desde,
        DateOnly? hasta,
        Guid? excepto,
        CancellationToken cancelacion);

    /// <summary>Una página de tramos, con el total.</summary>
    Task<PaginaDe<Impuesto>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion);

    /// <summary>Apunta un tramo nuevo.</summary>
    void Agregar(Impuesto impuesto);
}
