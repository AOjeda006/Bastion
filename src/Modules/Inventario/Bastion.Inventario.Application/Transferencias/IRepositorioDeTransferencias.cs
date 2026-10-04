using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Transferencias;
using Bastion.Inventario.Domain.Valoraciones;

namespace Bastion.Inventario.Application.Transferencias;

/// <summary>
/// Lo que los casos de uso de la transferencia necesitan de la persistencia: el documento, los
/// cerrojos de la valoración y de los lotes, y escribir lo que el documento mueve.
/// </summary>
/// <remarks>
/// <para>
/// <b>Las mismas tres costuras que el ajuste</b> —bloquear la valoración, resolver los lotes y las
/// series, y anotar—, y la tercera cambia de forma: un ajuste solo escribe filas del libro, y una
/// transferencia escribe además lo que vuela (ADR-0053 §1).
/// </para>
/// <para>
/// <b>Sin lectura de movimientos</b>: la transferencia no tiene superficie de lectura en el 2.11
/// (ADR-0053 §9), y la vuelta de la R13 desde el libro la da ya el tipo de documento de cada fila.
/// </para>
/// </remarks>
public interface IRepositorioDeTransferencias
{
    /// <summary>La transferencia con sus líneas, o <see langword="null"/> si no es de la empresa.</summary>
    /// <param name="id">Identificador de la transferencia.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>La transferencia, o <see langword="null"/>.</returns>
    Task<Transferencia?> ObtenerAsync(Guid id, CancellationToken cancelacion);

    /// <summary>Añade una transferencia nueva, que se escribe al confirmar la unidad de trabajo.</summary>
    /// <param name="transferencia">La transferencia: un borrador, o el inverso que anula a otra.</param>
    void Agregar(Transferencia transferencia);

    /// <summary>Bloquea la valoración de cada clave, creándola vacía si no existe, y la lee bloqueada.</summary>
    /// <param name="claves">Las claves que el documento toca: las del origen, las del destino o las dos.</param>
    /// <param name="divisa">La del documento, para las claves que nazcan.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>El saldo de cada clave, ya bloqueado hasta el <c>COMMIT</c>.</returns>
    Task<IReadOnlyDictionary<ClaveDeValoracion, SaldoValorado>> BloquearLasValoracionesAsync(
        IReadOnlyCollection<ClaveDeValoracion> claves,
        string divisa,
        CancellationToken cancelacion);

    /// <summary>Resuelve cada lote y cada número de serie a su fila, creando los que falten.</summary>
    /// <param name="lotes">Los lotes que nombra el documento.</param>
    /// <param name="series">Los números de serie que nombra el documento.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>Cada código con su fila.</returns>
    Task<LotesYSeriesResueltos> ResolverLotesYSeriesAsync(
        IReadOnlyList<CodigoDeUnArticulo> lotes,
        IReadOnlyList<CodigoDeUnArticulo> series,
        CancellationToken cancelacion);

    /// <summary>
    /// Escribe lo que mueve la transferencia: guarda el documento, mueve las existencias y la
    /// valoración, primero lo que baja y después lo que sube, y deja las filas del libro para la
    /// unidad de trabajo.
    /// </summary>
    /// <param name="movido">Las filas del libro y lo que vuela, tal como los da el documento.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>Una tarea que acaba cuando las sentencias han corrido.</returns>
    Task MoverAsync(LoQueMueveLaTransferencia movido, CancellationToken cancelacion);
}
