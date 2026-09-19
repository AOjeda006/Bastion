using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Movimientos;

namespace Bastion.Inventario.Application.Ajustes;

/// <summary>Del dominio a lo que se enseña.</summary>
internal static class Mapeos
{
    internal static AjusteDto ADto(this Ajuste ajuste) => new(
        ajuste.Id,
        ajuste.SerieId,
        ajuste.Numero,
        ajuste.AlmacenId,
        ajuste.FechaDeOperacion,
        ajuste.Motivo,
        ajuste.Estado.ToString(),
        ajuste.Lineas.Count);

    /// <summary>Una fila del libro, con el estado de su almacén resuelto por el puerto.</summary>
    /// <param name="movimiento">La fila.</param>
    /// <param name="almacenSeOfreceParaLoNuevo">Lo que contestó el puerto de almacenes.</param>
    /// <returns>Lo que se enseña.</returns>
    internal static MovimientoDto ADto(
        this MovimientoStock movimiento,
        bool almacenSeOfreceParaLoNuevo) => new(
        movimiento.Id,
        movimiento.FechaDeOperacion,
        movimiento.AlmacenId,
        almacenSeOfreceParaLoNuevo,
        movimiento.UbicacionId,
        movimiento.ArticuloId,
        movimiento.CantidadEnUnidadBase,
        movimiento.CantidadIntroducida,
        movimiento.UnidadIntroducidaId,
        movimiento.FactorAUnidadBase,
        movimiento.CosteUnitario.Cantidad,
        movimiento.CosteUnitario.Divisa,
        movimiento.DocumentoOrigenTipo.ToString(),
        movimiento.DocumentoOrigenId);
}
