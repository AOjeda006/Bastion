using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.Organizacion.Application.Ejercicios;

/// <summary>Los desenlaces fallidos que comparten varios casos de uso de ejercicio.</summary>
internal static class ErroresDeEjercicio
{
    internal static ErrorDeOperacion NoEncontrado(Guid id) => ErrorDeOperacion.NoEncontrado(
        "ejercicio-no-encontrado",
        $"No hay ningún ejercicio con el identificador {id}.");

    /// <summary>El intervalo pedido se pisa con el de otro ejercicio de la misma empresa.</summary>
    /// <remarks>
    /// Lo contesta el caso de uso preguntando antes; quien de verdad lo impide es la restricción
    /// de exclusión de la base, que es la única que cubre dos peticiones a la vez. El mensaje no
    /// interpola fechas a propósito: quien las mandó ya las tiene, y un formato de fecha dentro de
    /// un mensaje depende de la cultura del proceso que lo escribe.
    /// </remarks>
    internal static ErrorDeOperacion Solapado() => ErrorDeOperacion.Conflicto(
        "ejercicio-solapado",
        "El intervalo se pisa con el de otro ejercicio de la misma empresa. Una fecha tiene que " +
        "caer en un solo ejercicio: si cayera en dos, «a qué ejercicio pertenece esta operación» " +
        "dejaría de tener una sola respuesta, y con ella se irían el cierre, la numeración y la " +
        "autoliquidación (R9).");

    internal static ErrorDeOperacion Cerrado(Guid id) => ErrorDeOperacion.Conflicto(
        "ejercicio-cerrado",
        $"El ejercicio {id} está cerrado. Reábralo antes de cambiar sus fechas: moverlas movería " +
        "también las operaciones que caen dentro (R9).");
}
