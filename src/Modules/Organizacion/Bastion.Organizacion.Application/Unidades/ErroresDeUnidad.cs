using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.Organizacion.Application.Unidades;

/// <summary>Los desenlaces fallidos que comparten los casos de uso de unidad y conversión.</summary>
internal static class ErroresDeUnidad
{
    internal static ErrorDeOperacion NoEncontrada(Guid id) => ErrorDeOperacion.NoEncontrado(
        "unidad-medida-no-encontrada",
        $"No hay ninguna unidad de medida con el identificador {id}.");

    internal static ErrorDeOperacion ConversionNoEncontrada(Guid id) => ErrorDeOperacion.NoEncontrado(
        "conversion-um-no-encontrada",
        $"No hay ninguna conversión con el identificador {id}.");

    /// <summary>
    /// El par que se pide resolver no está declarado (ADR-0023, decisión 3).
    /// </summary>
    /// <remarks>
    /// <b>Es un error con nombre y no un cero, un nulo ni el producto de la cadena.</b> Las tres
    /// alternativas son las tres peores maneras de equivocarse en un inventario: un cero convierte
    /// las existencias en nada, un nulo se propaga sin ruido, y encadenar multiplica el error de
    /// redondeo y hace que el número dependa de qué camino elija el buscador — un dato de negocio
    /// no puede depender de un detalle de implementación que nadie ve. Lo que hay que hacer con
    /// este error es dar de alta la conversión que falta, con su factor pensado, y el mensaje lo
    /// dice.
    /// </remarks>
    /// <param name="unidadOrigenId">Unidad de la que se parte.</param>
    /// <param name="unidadDestinoId">Unidad a la que se llega.</param>
    internal static ErrorDeOperacion ConversionNoDeclarada(
        Guid unidadOrigenId,
        Guid unidadDestinoId) => ErrorDeOperacion.NoEncontrado(
        "conversion-um-no-declarada",
        $"No hay ninguna conversión declarada de {unidadOrigenId} a {unidadDestinoId}. No se " +
        "compone encadenando otras: tener kg→g y g→mg no da kg→mg, porque cada salto arrastra su " +
        "redondeo y con varios caminos posibles el número dependería de cuál se eligiera. Dé de " +
        "alta esa conversión con el factor que corresponda.");
}
