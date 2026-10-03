using Bastion.BuildingBlocks.Domain.Eventos;

namespace Bastion.Inventario.Contracts.Transferencias;

/// <summary>Una transferencia llegó entera al destino, y ya no le queda nada en vuelo.</summary>
/// <param name="TransferenciaId">El documento que se recibió.</param>
/// <param name="EmpresaId">Empresa a la que pertenece (R8).</param>
/// <param name="AlmacenDestinoId">A dónde llegó.</param>
/// <param name="FechaDeRecepcion">Día al que se imputa la entrada.</param>
/// <param name="Lineas">Cuántas filas del libro escribió.</param>
public sealed record TransferenciaRecibida(
    Guid TransferenciaId,
    Guid EmpresaId,
    Guid AlmacenDestinoId,
    DateOnly FechaDeRecepcion,
    int Lineas) : EventoDeIntegracion
{
    /// <summary>Nombre con el que viaja por la bandeja de salida.</summary>
    public const string Nombre = "inventario.transferencia-recibida";
}
