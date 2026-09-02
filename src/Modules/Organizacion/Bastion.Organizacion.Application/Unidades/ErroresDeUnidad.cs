using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.Organizacion.Application.Unidades;

/// <summary>Los desenlaces fallidos que comparten los casos de uso de unidad y conversión.</summary>
internal static class ErroresDeUnidad
{
    internal static ErrorDeOperacion NoEncontrada(Guid id) => ErrorDeOperacion.NoEncontrado(
        "unidad-medida-no-encontrada",
        $"No hay ninguna unidad de medida con el identificador {id}.");

    internal static ErrorDeOperacion ConversionNoEncontrada(Guid id) => ErrorDeOperacion.NoEncontrado(
        "conversion-um-no-encontrada",
        $"No hay ninguna conversión con el identificador {id}.");
}
