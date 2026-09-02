using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.Organizacion.Application.Impuestos;

/// <summary>Los desenlaces fallidos que comparten varios casos de uso de impuesto.</summary>
internal static class ErroresDeImpuesto
{
    internal static ErrorDeOperacion NoEncontrado(Guid id) => ErrorDeOperacion.NoEncontrado(
        "impuesto-no-encontrado",
        $"No hay ningún tramo de impuesto con el identificador {id}.");

    internal static ErrorDeOperacion Solapado(string codigo) => ErrorDeOperacion.Conflicto(
        "impuesto-con-tramos-solapados",
        $"Ya hay un tramo de {codigo} vigente en esas fechas. Un impuesto no se edita: se cierra " +
        "el tramo anterior y se abre el nuevo al día siguiente.");
}
