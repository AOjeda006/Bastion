using Bastion.BuildingBlocks.Domain.Eventos;

namespace Bastion.Inventario.Contracts.Transferencias;

/// <summary>
/// Una transferencia salió del origen: sus líneas ya son filas del libro en el origen, y tránsito en
/// el destino.
/// </summary>
/// <remarks>
/// Va por documento y sin las líneas dentro, como el del ajuste: el detalle está en el libro y en el
/// documento, y quien lo necesite lo pide por el identificador.
/// </remarks>
/// <param name="TransferenciaId">El documento que se envió.</param>
/// <param name="EmpresaId">Empresa a la que pertenece (R8).</param>
/// <param name="AlmacenOrigenId">De dónde salió.</param>
/// <param name="AlmacenDestinoId">A dónde va.</param>
/// <param name="FechaDeEnvio">Día al que se imputa la salida.</param>
/// <param name="Lineas">Cuántas filas del libro escribió.</param>
public sealed record TransferenciaEnviada(
    Guid TransferenciaId,
    Guid EmpresaId,
    Guid AlmacenOrigenId,
    Guid AlmacenDestinoId,
    DateOnly FechaDeEnvio,
    int Lineas) : EventoDeIntegracion
{
    /// <summary>Nombre con el que viaja por la bandeja de salida.</summary>
    public const string Nombre = "inventario.transferencia-enviada";
}
