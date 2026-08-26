using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.Organizacion.Application.Ejercicios;

/// <summary>Los desenlaces fallidos que comparten varios casos de uso de ejercicio.</summary>
internal static class ErroresDeEjercicio
{
    internal static ErrorDeOperacion NoEncontrado(Guid id) => ErrorDeOperacion.NoEncontrado(
        "ejercicio-no-encontrado",
        $"No hay ningún ejercicio con el identificador {id}.");

    internal static ErrorDeOperacion Cerrado(Guid id) => ErrorDeOperacion.Conflicto(
        "ejercicio-cerrado",
        $"El ejercicio {id} está cerrado. Reábralo antes de cambiar sus fechas: moverlas movería " +
        "también las operaciones que caen dentro (R9).");
}
