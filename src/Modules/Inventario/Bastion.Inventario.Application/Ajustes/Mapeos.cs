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
        ajuste.Lineas.Count,
        ajuste.AnulaAId,
        ajuste.Divisa);

    /// <summary>La ficha, con todas sus líneas en su orden (ADR-0065 §8).</summary>
    /// <param name="ajuste">El documento, con sus líneas.</param>
    /// <returns>Lo que se enseña.</returns>
    internal static FichaDeAjusteDto AFicha(this Ajuste ajuste) => new(
        ajuste.Id,
        ajuste.SerieId,
        ajuste.Numero,
        ajuste.AlmacenId,
        ajuste.FechaDeOperacion,
        ajuste.Motivo,
        ajuste.Estado.ToString(),
        ajuste.Divisa,
        ajuste.AnulaAId,
        ajuste.RecuentoId,
        [.. ajuste.Lineas.OrderBy(linea => linea.Numero).Select(ALineaDeLaFicha)]);

    /// <summary>Una fila del listado, sin líneas.</summary>
    /// <param name="ajuste">El documento.</param>
    /// <returns>Lo que se enseña.</returns>
    internal static AjusteResumenDto AResumen(this Ajuste ajuste) => new(
        ajuste.Id,
        ajuste.Numero,
        ajuste.AlmacenId,
        ajuste.FechaDeOperacion,
        ajuste.Estado.ToString(),
        ajuste.Motivo);

    // LA CANTIDAD EN UNIDAD BASE SE REDONDEA COMO LA DEL LIBRO, con el mismo método: la ficha tiene que
    // decir lo que el libro sumó, y un producto sin redondear diría una cifra que no está en ninguna
    // fila.
    private static LineaDeFichaDeAjusteDto ALineaDeLaFicha(LineaDeAjuste linea) => new(
        linea.Numero,
        linea.UbicacionId,
        linea.ArticuloId,
        linea.CantidadIntroducida,
        linea.UnidadIntroducidaId,
        linea.FactorAUnidadBase,
        MovimientoStock.EnUnidadBase(linea.CantidadIntroducida, linea.FactorAUnidadBase),
        linea.CosteUnitario,
        linea.CodigoDeLote,
        linea.NumeroDeSerie,
        linea.Valor);

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
        movimiento.LoteId,
        movimiento.NumeroDeSerieId,
        movimiento.CantidadEnUnidadBase,
        movimiento.CantidadIntroducida,
        movimiento.UnidadIntroducidaId,
        movimiento.FactorAUnidadBase,
        movimiento.CosteUnitario?.Cantidad,
        movimiento.Valor.Cantidad,
        movimiento.PrecioMedio.Cantidad,
        movimiento.Divisa,
        movimiento.DocumentoOrigenTipo.ToString(),
        movimiento.DocumentoOrigenId);
}
