using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Domain.Transferencias;

namespace Bastion.Inventario.Application.Transferencias;

/// <summary>Del dominio a lo que se enseña.</summary>
internal static class Mapeos
{
    internal static TransferenciaDto ADto(this Transferencia transferencia) => new(
        transferencia.Id,
        transferencia.SerieId,
        transferencia.Numero,
        transferencia.AlmacenOrigenId,
        transferencia.AlmacenDestinoId,
        transferencia.FechaDeEnvio,
        transferencia.FechaDeRecepcion,
        transferencia.Estado.ToString(),
        transferencia.Lineas.Count,
        transferencia.AnulaAId,
        transferencia.Motivo,
        transferencia.Divisa);
}
