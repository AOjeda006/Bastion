using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.Inventario.Application.Saldos;

/// <summary>Los desenlaces de la consulta del saldo a una fecha.</summary>
/// <remarks>
/// <b>Con el prefijo <c>saldo-</c></b>, como cada documento lleva el suyo: la misma fecha futura se
/// explica distinto en una lectura que en un documento que mueve el libro.
/// </remarks>
internal static class ErroresDeSaldo
{
    internal const string CodigoConFechaFutura = "saldo-con-fecha-futura";

    /// <summary>La fecha pedida es posterior a hoy (ADR-0063 §3).</summary>
    /// <remarks>
    /// <b>De clase validación, y no conflicto</b>: es un parámetro de una lectura y no choca con ningún
    /// estado. El saldo del futuro no está en el libro (ADR-0044 §4), así que no hay nada que contestar
    /// hasta ese día. Lleva el campo, porque es lo único que hay que corregir.
    /// </remarks>
    /// <param name="fecha">La fecha pedida.</param>
    /// <param name="hoy">El día UTC de hoy, que es el último que tiene saldo.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion ConFechaFutura(DateOnly fecha, DateOnly hoy) => ErrorDeOperacion.Validacion(
        CodigoConFechaFutura,
        $"La fecha {fecha:yyyy-MM-dd} es posterior a hoy, {hoy:yyyy-MM-dd}: el saldo es la suma del " +
        "libro, y el libro solo recoge lo que ya ha pasado. Pida una fecha de hoy o anterior.",
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [LosParametrosDelSaldo.Fecha] = [$"El último día con saldo es hoy, {hoy:yyyy-MM-dd}."],
        });
}
