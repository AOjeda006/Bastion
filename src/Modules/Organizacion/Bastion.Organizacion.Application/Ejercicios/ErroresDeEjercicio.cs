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

    /// <summary>Se pidió cerrar un ejercicio que ya estaba cerrado.</summary>
    /// <remarks>
    /// Es un 409 y no un 200 silencioso desde el ítem 2.6. Cerrar dejó de ser idempotente cuando
    /// pasó a tener precondiciones y consecuencias, y el caso que este error destapa es el que
    /// importa: alguien se adelantó, o el ejercicio se reabrió y se volvió a cerrar por en medio.
    /// </remarks>
    internal static ErrorDeOperacion YaCerrado(Guid id) => ErrorDeOperacion.Conflicto(
        "ejercicio-ya-cerrado",
        $"El ejercicio {id} ya estaba cerrado, así que esta petición no lo ha cerrado. Si esperaba " +
        "cerrarlo usted, alguien se ha adelantado: vuelva a leerlo antes de decidir.");

    /// <summary>Se pidió reabrir un ejercicio que ya estaba abierto.</summary>
    /// <remarks>
    /// Por lo mismo que <see cref="YaCerrado"/>, y con más motivo: una reapertura deja evento
    /// auditado con su motivo, y una que no reabre nada no puede dejarlo.
    /// </remarks>
    internal static ErrorDeOperacion YaAbierto(Guid id) => ErrorDeOperacion.Conflicto(
        "ejercicio-ya-abierto",
        $"El ejercicio {id} ya estaba abierto, así que esta petición no lo ha reabierto.");

    /// <summary>Queda algún documento en borrador con fecha dentro del ejercicio que se cierra.</summary>
    /// <remarks>
    /// <b>Nombra los módulos</b>, que es para lo que el puerto publica <c>Modulo</c>. «Quedan
    /// borradores dentro» no se puede arreglar sin saber dónde están, y con seis módulos inscritos
    /// un error mudo obliga a recorrerlos uno por uno. No dice cuántos ni cuáles: eso es un listado
    /// y cada módulo ya publica el suyo, filtrable por fecha y por estado.
    /// </remarks>
    internal static ErrorDeOperacion ConBorradores(IEnumerable<string> modulos) =>
        ErrorDeOperacion.Conflicto(
            "ejercicio-con-borradores",
            "Quedan documentos en borrador con fecha dentro del ejercicio, en: " +
            string.Join(", ", modulos) + ". Cerrar congela el periodo, y un borrador todavía " +
            "puede cambiar de importe: confírmelos o bórrelos antes de cerrar (R9).");

    internal static ErrorDeOperacion Cerrado(Guid id) => ErrorDeOperacion.Conflicto(
        "ejercicio-cerrado",
        $"El ejercicio {id} está cerrado. Reábralo antes de cambiar sus fechas: moverlas movería " +
        "también las operaciones que caen dentro (R9).");
}
