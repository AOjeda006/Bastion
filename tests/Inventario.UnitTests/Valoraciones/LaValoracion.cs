using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Valoraciones;

namespace Bastion.Inventario.UnitTests.Valoraciones;

/// <summary>
/// Valora un documento como lo hace el caso de uso, con los saldos que le toquen.
/// </summary>
/// <remarks>
/// <b>El caso de uso los lee bloqueados de la base, y aquí salen de las filas del libro que se le
/// pasen</b>, sumadas por clave. Es la misma suma que hace la tabla, así que un documento que se
/// confirma después de otro se valora contra lo que el otro dejó, sin encender nada.
/// </remarks>
internal static class LaValoracion
{
    /// <summary>Valora el documento como si ninguna de sus claves se hubiera movido nunca.</summary>
    /// <param name="documento">El documento en borrador.</param>
    /// <returns>Una valoración por línea, en su orden.</returns>
    internal static IReadOnlyList<LineaValorada> DesdeCero(Ajuste documento) => Tras([], documento);

    /// <summary>Valora el documento contra lo que dejaron unas filas del libro ya escritas.</summary>
    /// <param name="loQueHabia">Las filas de antes, todas en la divisa del documento.</param>
    /// <param name="documento">El documento en borrador.</param>
    /// <returns>Una valoración por línea, en su orden.</returns>
    internal static IReadOnlyList<LineaValorada> Tras(
        IEnumerable<MovimientoStock> loQueHabia,
        Ajuste documento)
    {
        IReadOnlyList<LineaAValorar> lineas = documento.LineasAValorar();
        MovimientoStock[] anteriores = [.. loQueHabia];

        var saldos = lineas
            .Select(linea => linea.Clave)
            .Distinct()
            .ToDictionary(
                clave => clave,
                clave =>
                {
                    MovimientoStock[] suyas = [.. anteriores.Where(fila =>
                        fila.ArticuloId == clave.ArticuloId && fila.AlmacenId == clave.AlmacenId)];

                    return new SaldoValorado(
                        suyas.Sum(fila => fila.CantidadEnUnidadBase),
                        Importe.De(suyas.Sum(fila => fila.Valor.Cantidad), documento.Divisa),
                        suyas.Length == 0 ? null : suyas.Max(fila => fila.FechaDeOperacion));
                });

        return new ElPrecioMedioPonderado().Valorar(saldos, lineas, documento.Divisa, documento.FechaDeOperacion);
    }
}
