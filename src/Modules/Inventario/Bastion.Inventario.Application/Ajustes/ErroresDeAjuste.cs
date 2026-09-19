using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.Inventario.Application.Ajustes;

/// <summary>Los desenlaces de negocio del ajuste que no vienen de un puerto.</summary>
internal static class ErroresDeAjuste
{
    internal const string CodigoNoEncontrado = "ajuste-no-encontrado";
    internal const string CodigoSinLineas = "ajuste-sin-lineas";
    internal const string CodigoNoEstaEnBorrador = "ajuste-no-esta-en-borrador";
    internal const string CodigoNoEstaConfirmado = "ajuste-no-esta-confirmado";

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

    internal static ErrorDeOperacion NoEstaConfirmado(Guid ajusteId, string estado) =>
        ErrorDeOperacion.Conflicto(
            CodigoNoEstaConfirmado,
            $"El ajuste {ajusteId} está en estado «{estado}»: solo se anula lo que está " +
            "confirmado. Un borrador no ha movido nada, así que no hay nada que compensar.");
}
