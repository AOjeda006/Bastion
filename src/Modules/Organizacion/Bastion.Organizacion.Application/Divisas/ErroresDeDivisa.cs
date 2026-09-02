using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.Organizacion.Application.Divisas;

/// <summary>Los desenlaces fallidos que comparten los casos de uso de divisa y cotización.</summary>
internal static class ErroresDeDivisa
{
    internal static ErrorDeOperacion NoEncontrada(Guid id) => ErrorDeOperacion.NoEncontrado(
        "divisa-no-encontrada",
        $"No hay ninguna divisa con el identificador {id}.");

    internal static ErrorDeOperacion CambioNoEncontrado(Guid id) => ErrorDeOperacion.NoEncontrado(
        "tipo-cambio-no-encontrado",
        $"No hay ninguna cotización con el identificador {id}.");
}
