using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Domain.Ejercicios;

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

    /// <summary>El intervalo nuevo dejaría fuera documentos que hoy caen dentro.</summary>
    /// <remarks>
    /// <b>Es el agujero silencioso que este ítem tapa.</b> Hasta ahora, encoger un ejercicio
    /// abierto con movimientos dentro estaba permitido, y los que quedaban fuera pasaban a
    /// <c>SinEjercicio</c> sin que nadie los tocara: ni cambiaban de fecha, ni de importe, ni
    /// dejaban rastro de que habían cambiado de periodo. Se enteraba quien cuadrara el año, meses
    /// después.
    /// </remarks>
    internal static ErrorDeOperacion DejariaDocumentosFuera(IEnumerable<string> modulos) =>
        ErrorDeOperacion.Conflicto(
            "ejercicio-dejaria-documentos-fuera",
            "Las fechas nuevas dejarían fuera del ejercicio documentos que hoy caen dentro, en: " +
            string.Join(", ", modulos) + ". Esos documentos se quedarían sin ejercicio sin que " +
            "nadie los tocara. Mueva primero los documentos o deje el intervalo donde está (R9).");

    /// <summary>Se pidió borrar un ejercicio con documentos dentro.</summary>
    /// <remarks>
    /// No lo impide ninguna clave ajena, y no puede impedirlo: los documentos viven en otros
    /// esquemas y entre esquemas no se cruza (regla 4). Lo único que hay entre borrar el
    /// ejercicio y dejar huérfanos los movimientos de medio año es esta pregunta.
    /// </remarks>
    internal static ErrorDeOperacion ConDocumentos(IEnumerable<string> modulos) =>
        ErrorDeOperacion.Conflicto(
            "ejercicio-con-documentos",
            "El ejercicio tiene documentos con fecha dentro, en: " + string.Join(", ", modulos) +
            ". Borrarlo dejaría esos documentos sin ejercicio al que pertenecer, y a qué ejercicio " +
            "pertenece una operación tiene que tener una sola respuesta (R9).");

    /// <summary>La reapertura vino sin motivo, o con uno más largo de la cuenta.</summary>
    /// <remarks>
    /// Se comprueba en el caso de uso y no solo en el borde: un <c>[Required]</c> en el DTO para
    /// una cadena no distingue «vacía» de «tres espacios», y lo que hace falta aquí es que quede
    /// escrito algo que se pueda leer.
    /// </remarks>
    internal static ErrorDeOperacion MotivoNoValido() => ErrorDeOperacion.Validacion(
        "ejercicio-motivo-no-valido",
        "El motivo de la reapertura no puede estar vacío ni pasar de " +
        $"{Ejercicio.LargoDelMotivo} caracteres: es lo único que queda para entender, dentro de " +
        "dos años, por qué se volvió a abrir un periodo que estaba cerrado.");

    internal static ErrorDeOperacion Cerrado(Guid id) => ErrorDeOperacion.Conflicto(
        "ejercicio-cerrado",
        $"El ejercicio {id} está cerrado. Reábralo antes de cambiar sus fechas: moverlas movería " +
        "también las operaciones que caen dentro (R9).");
}
