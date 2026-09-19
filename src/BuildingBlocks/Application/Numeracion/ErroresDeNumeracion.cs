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
}
