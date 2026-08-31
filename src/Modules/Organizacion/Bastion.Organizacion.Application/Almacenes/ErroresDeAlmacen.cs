using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.Organizacion.Application.Almacenes;

/// <summary>Los desenlaces fallidos que comparten varios casos de uso de almacén.</summary>
internal static class ErroresDeAlmacen
{
    internal static ErrorDeOperacion NoEncontrado(Guid id) => ErrorDeOperacion.NoEncontrado(
        "almacen-no-encontrado",
        $"No hay ningún almacén con el identificador {id}.");
}
