using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.Organizacion.Application.Series;

/// <summary>Los desenlaces fallidos que comparten varios casos de uso de serie.</summary>
internal static class ErroresDeSerie
{
    internal static ErrorDeOperacion NoEncontrada(Guid id) => ErrorDeOperacion.NoEncontrado(
        "serie-no-encontrada",
        $"No hay ninguna serie con el identificador {id}.");

    internal static ErrorDeOperacion Cerrada(Guid id) => ErrorDeOperacion.Conflicto(
        "serie-cerrada",
        $"La serie {id} está cerrada y no admite cambios.");

    /// <summary>
    /// La serie ya ha numerado, así que no se puede quitar de en medio.
    /// </summary>
    /// <remarks>
    /// Borrarla dejaría en el libro registro números emitidos que no apuntan a ninguna serie, y
    /// dejaría libre el hueco para que otra volviera a emitir los mismos: dos facturas distintas
    /// con el mismo número, que es justo lo que R11 y Veri*factu no permiten.
    /// </remarks>
    internal static ErrorDeOperacion YaHaNumerado(long contador) => ErrorDeOperacion.Conflicto(
        "serie-ya-numerada",
        $"La serie ya ha numerado {contador} documento(s) y no se puede suprimir. Ciérrela si no " +
        "quiere que siga numerando.");
}
