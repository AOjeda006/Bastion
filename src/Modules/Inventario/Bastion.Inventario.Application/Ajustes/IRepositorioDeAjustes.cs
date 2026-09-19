using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Movimientos;

namespace Bastion.Inventario.Application.Ajustes;

/// <summary>Los ajustes y las filas del libro que escriben, que son dos cosas distintas.</summary>
/// <remarks>
/// <b>Un solo repositorio para dos agregados, y a propósito.</b> Confirmar escribe la cabecera del
/// documento y sus filas del libro en la <b>misma transacción</b> —es la mitad de la R12 que este
/// ítem dobla a sabiendas, con su motivo en <c>docs/PLAN.md</c>—, así que separarlos en dos
/// repositorios con dos unidades de trabajo sugeriría que se pueden guardar por separado, que es
/// exactamente lo que no puede pasar.
/// </remarks>
public interface IRepositorioDeAjustes
{
    /// <summary>Trae un ajuste con sus líneas.</summary>
    /// <param name="id">Identificador del ajuste.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>El ajuste, o <see langword="null"/> si no hay ninguno con ese identificador.</returns>
    Task<Ajuste?> ObtenerAsync(Guid id, CancellationToken cancelacion);

    /// <summary>Apunta un ajuste nuevo.</summary>
    /// <param name="ajuste">El documento.</param>
    void Agregar(Ajuste ajuste);

    /// <summary>Apunta las filas del libro que un documento acaba de generar.</summary>
    /// <remarks>
    /// Solo se añaden. No hay ningún método para modificarlas ni para quitarlas, y no es un olvido:
    /// la tabla las rechazaría igualmente en el motor, y una firma que no existe no hace falta
    /// explicarla dos veces.
    /// </remarks>
    /// <param name="movimientos">Las filas.</param>
    void AgregarMovimientos(IReadOnlyCollection<MovimientoStock> movimientos);

    /// <summary>Las filas del libro que escribió un documento (R13, la ida).</summary>
    /// <param name="tipo">Qué clase de documento.</param>
    /// <param name="documentoId">Cuál.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>Sus movimientos, en el orden en que se escribieron.</returns>
    Task<IReadOnlyList<MovimientoStock>> MovimientosDeAsync(
        TipoDeDocumentoOrigen tipo,
        Guid documentoId,
        CancellationToken cancelacion);
}
