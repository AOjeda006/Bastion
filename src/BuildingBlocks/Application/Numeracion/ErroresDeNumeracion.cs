using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.BuildingBlocks.Application.Numeracion;

/// <summary>
/// Lo que puede salir mal al pedirle un número a una serie, con su código, que es contrato
/// publicado.
/// </summary>
public static class ErroresDeNumeracion
{
    /// <summary>Código estable del <c>409</c> de una serie que no entrega números.</summary>
    public const string CodigoDeSerieNoNumera = "serie-no-numera";

    /// <summary>
    /// La sentencia no encontró ninguna fila que subir: la serie no existe, está cerrada o no es de
    /// esta empresa.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Un solo error para los tres casos, y es a propósito.</b> Distinguirlos convertiría la
    /// confirmación en un detector de series ajenas: quien probara identificadores al azar sabría
    /// cuáles existen en otras sociedades por la diferencia entre «no existe» y «está cerrada». Es
    /// el mismo criterio con el que <c>CrearTercero</c> no dice si el identificador que estorba
    /// estaba activo o bloqueado.
    /// </para>
    /// <para>
    /// <b><c>409</c> y no <c>404</c>:</b> quien confirma no está pidiendo la serie, está pidiendo
    /// que le den un número, y lo que falla es el estado de algo —la serie— que la operación
    /// necesita. Un <c>404</c> además diría que el documento no existe, y el documento existe.
    /// </para>
    /// </remarks>
    /// <param name="serieId">La serie que se pidió, para que el mensaje la nombre.</param>
    public static ErrorDeOperacion SerieNoNumera(Guid serieId) => ErrorDeOperacion.Conflicto(
        CodigoDeSerieNoNumera,
        $"La serie {serieId} no puede entregar números: o no existe, o está cerrada, o no es de " +
        "esta empresa. Elija una serie activa y vuelva a confirmar.");

    /// <summary>Código estable del <c>409</c> de una serie que numera otra clase de documento.</summary>
    public const string CodigoDeSerieDeOtroDocumento = "serie-de-otro-documento";

    /// <summary>
    /// La serie es de esta empresa y está activa, pero numera otra clase de documento: una serie de
    /// facturas no le da número a un ajuste de inventario.
    /// </summary>
    /// <remarks>
    /// <b>Este sí se distingue de <see cref="SerieNoNumera"/>, y no abre el oráculo</b> que aquel
    /// cierra: solo se contesta cuando la serie ya ha resultado ser <b>de esta empresa</b>. Una
    /// serie ajena de otro tipo sigue contestando «no numera», igual que una que no existe.
    /// </remarks>
    /// <param name="serieId">La serie que se pidió.</param>
    public static ErrorDeOperacion SerieDeOtroDocumento(Guid serieId) => ErrorDeOperacion.Conflicto(
        CodigoDeSerieDeOtroDocumento,
        $"La serie {serieId} numera otra clase de documento. Elija una serie de este tipo de " +
        "documento y vuelva a confirmar.");

    /// <summary>
    /// Código estable del <c>409</c> de una fecha que no cae en el ejercicio del que cuelga la
    /// serie.
    /// </summary>
    public const string CodigoDeFechaFueraDelEjercicioDeLaSerie = "fecha-fuera-del-ejercicio-de-la-serie";

    /// <summary>
    /// La serie es de esta empresa, está activa y es de este documento, pero cuelga de un ejercicio
    /// que no comprende la fecha: la numeración es por serie <b>y ejercicio</b> (R5), y un número
    /// de la serie del año pasado en un documento de este año mezclaría los dos.
    /// </summary>
    /// <remarks>
    /// <b>No es el error del periodo</b> (R9), que dice si la fecha tiene un ejercicio abierto. Aquí
    /// la fecha puede tener el suyo, abierto y en regla, y aun así no ser el de la serie. Se arregla
    /// de otra manera —eligiendo la serie del ejercicio de la fecha—, y por eso lleva otro código.
    /// </remarks>
    /// <param name="serieId">La serie que se pidió.</param>
    /// <param name="fecha">La fecha que tenía que caer dentro de su ejercicio.</param>
    public static ErrorDeOperacion FechaFueraDelEjercicioDeLaSerie(Guid serieId, DateOnly fecha) =>
        ErrorDeOperacion.Conflicto(
            CodigoDeFechaFueraDelEjercicioDeLaSerie,
            $"La serie {serieId} cuelga de un ejercicio que no comprende el {fecha:yyyy-MM-dd}. " +
            "Elija una serie del ejercicio de esa fecha y vuelva a confirmar.");
}
