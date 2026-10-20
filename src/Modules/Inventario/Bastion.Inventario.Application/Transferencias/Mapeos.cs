using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Domain.Movimientos;
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

    /// <summary>La ficha, con todas sus líneas en su orden (ADR-0068 §8).</summary>
    /// <param name="transferencia">El documento, con sus líneas.</param>
    /// <returns>Lo que se enseña.</returns>
    internal static FichaDeTransferenciaDto AFicha(this Transferencia transferencia) => new(
        transferencia.Id,
        transferencia.SerieId,
        transferencia.Numero,
        transferencia.AlmacenOrigenId,
        transferencia.AlmacenDestinoId,
        transferencia.FechaDeEnvio,
        transferencia.FechaDeRecepcion,
        transferencia.Estado.ToString(),
        transferencia.Divisa,
        transferencia.AnulaAId,
        transferencia.Motivo,
        [.. transferencia.Lineas.OrderBy(linea => linea.Numero).Select(ALineaDeLaFicha)]);

    /// <summary>Una fila del listado, sin líneas.</summary>
    /// <param name="transferencia">El documento.</param>
    /// <returns>Lo que se enseña.</returns>
    internal static TransferenciaResumenDto AResumen(this Transferencia transferencia) => new(
        transferencia.Id,
        transferencia.Numero,
        transferencia.AlmacenOrigenId,
        transferencia.AlmacenDestinoId,
        transferencia.FechaDeEnvio,
        transferencia.FechaDeRecepcion,
        transferencia.Estado.ToString(),
        transferencia.AnulaAId);

    // LA CANTIDAD EN UNIDAD BASE SE REDONDEA COMO LA DEL LIBRO, con el mismo método que la del ajuste:
    // la ficha tiene que decir lo que el libro sumó en cada punta.
    private static LineaDeFichaDeTransferenciaDto ALineaDeLaFicha(LineaDeTransferencia linea) => new(
        linea.Numero,
        linea.UbicacionOrigenId,
        linea.UbicacionDestinoId,
        linea.ArticuloId,
        linea.CantidadIntroducida,
        linea.UnidadIntroducidaId,
        linea.FactorAUnidadBase,
        MovimientoStock.EnUnidadBase(linea.CantidadIntroducida, linea.FactorAUnidadBase),
        linea.CodigoDeLote,
        linea.NumeroDeSerie,
        linea.Valor);
}
