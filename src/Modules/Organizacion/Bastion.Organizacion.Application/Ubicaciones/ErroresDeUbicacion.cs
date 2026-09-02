using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.Organizacion.Application.Ubicaciones;

/// <summary>Los desenlaces fallidos que comparten varios casos de uso de ubicación.</summary>
internal static class ErroresDeUbicacion
{
    internal static ErrorDeOperacion NoEncontrada(Guid id) => ErrorDeOperacion.NoEncontrado(
        "ubicacion-no-encontrada",
        $"No hay ninguna ubicación con el identificador {id}.");
}
