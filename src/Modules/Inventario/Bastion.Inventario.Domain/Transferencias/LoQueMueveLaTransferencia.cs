using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.Inventario.Domain.Movimientos;

namespace Bastion.Inventario.Domain.Transferencias;

/// <summary>
/// Lo que un paso de la transferencia escribe en la misma transacción: filas del libro y tránsito.
/// </summary>
/// <remarks>
/// <b>Las dos listas van por separado porque solo una es el libro</b> (ADR-0053 §1). Las filas del
/// libro mueven el físico y la valoración, y el tránsito mueve las columnas del destino que cuentan lo
/// que vuela. Quien lo escribe aplica primero todo lo que baja y después todo lo que sube, porque el
/// índice de la serie se comprueba fila a fila (§7).
/// </remarks>
/// <param name="Movimientos">Las filas del libro, una por línea y pata.</param>
/// <param name="Transito">Lo que entra en tránsito o sale de él, una entrada por línea.</param>
public sealed record LoQueMueveLaTransferencia(
    IReadOnlyList<MovimientoStock> Movimientos,
    IReadOnlyList<MovimientoEnTransito> Transito);

/// <summary>
/// Lo que una línea suma al tránsito del destino, o le resta, en unidad base y en valor.
/// </summary>
/// <remarks>
/// <b>La clave es la de la existencia del destino</b>: su almacén, su ubicación, su lote y su serie.
/// La valoración del destino se suma por artículo y almacén, como el libro.
/// </remarks>
/// <param name="ArticuloId">El artículo que vuela.</param>
/// <param name="AlmacenId">El almacén de destino.</param>
/// <param name="UbicacionId">La ubicación de destino.</param>
/// <param name="LoteId">El lote, o <see langword="null"/>.</param>
/// <param name="NumeroDeSerieId">El número de serie, o <see langword="null"/>.</param>
/// <param name="Cantidad">En unidad base, con signo: positiva al enviar y negativa al recibir.</param>
/// <param name="Valor">El valor que viaja, con el signo de la cantidad.</param>
public sealed record MovimientoEnTransito(
    Guid ArticuloId,
    Guid AlmacenId,
    Guid UbicacionId,
    Guid? LoteId,
    Guid? NumeroDeSerieId,
    decimal Cantidad,
    Importe Valor);
