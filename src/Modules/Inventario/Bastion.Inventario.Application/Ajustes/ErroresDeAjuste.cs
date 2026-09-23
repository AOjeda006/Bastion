using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.Inventario.Application.Ajustes;

/// <summary>Los desenlaces de negocio del ajuste que no vienen de un puerto.</summary>
internal static class ErroresDeAjuste
{
    internal const string CodigoNoEncontrado = "ajuste-no-encontrado";
    internal const string CodigoSinLineas = "ajuste-sin-lineas";
    internal const string CodigoNoEstaEnBorrador = "ajuste-no-esta-en-borrador";
    internal const string CodigoNoEstaConfirmado = "ajuste-no-esta-confirmado";
    internal const string CodigoMotivoNoValido = "ajuste-motivo-no-valido";
    internal const string CodigoSinEjercicio = "ajuste-sin-ejercicio";
    internal const string CodigoEnEjercicioCerrado = "ajuste-en-ejercicio-cerrado";

    internal static ErrorDeOperacion NoEncontrado(Guid ajusteId) => ErrorDeOperacion.NoEncontrado(
        CodigoNoEncontrado,
        $"No hay ningún ajuste con el identificador {ajusteId}.");

    /// <summary>La vuelta de la R13, dicha antes de que exista el documento.</summary>
    /// <remarks>
    /// Un ajuste sin líneas no llega ni a abrirse. Podría abrirse vacío y rechazarse al confirmar,
    /// y sería peor: dejaría en la base documentos en borrador que nunca podrán confirmarse, y
    /// alguien tendría que decidir qué se hace con ellos.
    /// </remarks>
    /// <param name="ajusteId">El documento, si ya existía.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion SinLineas(Guid ajusteId) => ErrorDeOperacion.Validacion(
        CodigoSinLineas,
        $"El ajuste {ajusteId} no tiene ninguna línea: un documento que no mueve el libro no " +
        "ajusta nada (R13).");

    internal static ErrorDeOperacion NoEstaEnBorrador(Guid ajusteId, string estado) =>
        ErrorDeOperacion.Conflicto(
            CodigoNoEstaEnBorrador,
            $"El ajuste {ajusteId} está en estado «{estado}»: solo se confirma un borrador, y un " +
            "ajuste confirmado no se vuelve a confirmar porque sus filas del libro ya están " +
            "escritas y el libro es de solo añadido (R2, R3).");

    /// <summary>El motivo de la anulación, que es lo único que trae su petición.</summary>
    /// <remarks>
    /// <b>Se comprueba en el caso de uso aunque el dominio también lo compruebe.</b> Ahí es una
    /// invariante y se lanza; aquí es un cuerpo mal escrito, y lo que el borde debe devolver por
    /// eso es un 400 con su código, no un 500 (ADR-0004). No se dice qué tenía de malo más allá
    /// del largo: el valor recibido es del llamante y no se le devuelve dentro de un error.
    /// </remarks>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion MotivoNoValido() => ErrorDeOperacion.Validacion(
        CodigoMotivoNoValido,
        "El motivo de la anulación no puede estar vacío ni pasar de " +
        $"{Domain.Ajustes.Ajuste.LargoDelMotivo} caracteres: es lo único que queda para entender " +
        "la corrección dentro de dos años.");

    /// <summary>La fecha del documento no cae en ningún ejercicio de la empresa (R9).</summary>
    /// <remarks>
    /// <b>Es un desenlace distinto del ejercicio cerrado, y por eso lleva su propio código.</b> Se
    /// arreglan de maneras distintas: éste, abriendo el ejercicio que falta —o corrigiendo la
    /// fecha, si estaba mal escrita—; el otro, reabriendo o poniendo el documento donde le toca.
    /// Un solo código obligaría a leer la prosa para saber cuál de las dos cosas hacer, y la prosa
    /// es lo único del error que no es contrato.
    /// </remarks>
    /// <param name="fecha">La fecha de operación que no cae en ningún ejercicio.</param>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion SinEjercicio(DateOnly fecha) => ErrorDeOperacion.Conflicto(
        CodigoSinEjercicio,
        $"La fecha de operación {fecha:yyyy-MM-dd} no cae dentro de ningún ejercicio de esta " +
        "empresa, así que el documento no podría imputarse a ninguna autoliquidación. Abra el " +
        "ejercicio que falta o corrija la fecha (R9).");

    /// <summary>La fecha del documento cae en un ejercicio ya cerrado (R9).</summary>
    /// <returns>El error.</returns>
    internal static ErrorDeOperacion EnEjercicioCerrado(DateOnly fecha) =>
        ErrorDeOperacion.Conflicto(
            CodigoEnEjercicioCerrado,
            $"El ejercicio al que cae la fecha de operación {fecha:yyyy-MM-dd} está cerrado: ese " +
            "periodo ya es definitivo y no admite documentos nuevos. Reabra el ejercicio, con su " +
            "motivo, o lleve el documento a una fecha del ejercicio abierto (R9).");

    internal static ErrorDeOperacion NoEstaConfirmado(Guid ajusteId, string estado) =>
        ErrorDeOperacion.Conflicto(
            CodigoNoEstaConfirmado,
            $"El ajuste {ajusteId} está en estado «{estado}»: solo se anula lo que está " +
            "confirmado. Un borrador no ha movido nada, así que no hay nada que compensar.");
}
