namespace Bastion.Terceros.Domain.Terceros;

/// <summary>
/// Cuánto se sabe de que un identificador fiscal sea de verdad el que dice ser.
/// </summary>
/// <remarks>
/// <para>
/// <b>Existe porque hay dos clases de identificador y solo una se puede comprobar.</b> Un NIF, un
/// NIE o un CIF llevan carácter de control, y el control se calcula: o cuadra o no cuadra, y eso
/// se sabe sin preguntarle a nadie. Un identificador extranjero no lleva nada parecido —cada país
/// tiene su forma, y algunas no tienen dígito de control ninguno—, así que lo único honesto que
/// se puede decir de él es que <b>no se ha comprobado</b>.
/// </para>
/// <para>
/// <b>Lo que este campo impide es la mentira por omisión.</b> Sin él, los dos identificadores se
/// guardan en la misma columna y quedan indistinguibles: el día que alguien cruce el maestro de
/// terceros con un fichero de la AEAT, tratará como verificados unos valores que nadie verificó.
/// Lo que no se puede validar se marca como no validado; no se da por bueno.
/// </para>
/// <para>
/// <b>Dos valores y no tres.</b> Falta el que dirá «comprobado contra un registro externo» —el
/// VIES para el IVA intracomunitario—, y falta a propósito: no existe todavía el camino que lo
/// produciría, y un valor que nadie produce es una rama que nadie recorre y que nadie prueba
/// (ADR-0020). Cuando llegue esa consulta traerá el suyo.
/// </para>
/// </remarks>
public enum EstadoDeVerificacion
{
    /// <summary>
    /// Nadie ha comprobado que este identificador exista ni que esté bien formado.
    /// </summary>
    /// <remarks>
    /// Es el valor cero a propósito: si alguna vez llegara un
    /// <c>default(EstadoDeVerificacion)</c> por un camino que no pasó por la fábrica, la respuesta
    /// que se encuentra es la que no afirma nada.
    /// </remarks>
    NoVerificado = 0,

    /// <summary>
    /// El carácter de control cuadra: es un NIF, un NIE o un CIF bien formado.
    /// </summary>
    /// <remarks>
    /// <b>No dice que el identificador esté dado de alta en la AEAT</b>, y la diferencia importa:
    /// el algoritmo comprueba la forma, no la existencia. Un CIF inventado con su dígito bien
    /// calculado llega hasta aquí, y está bien que llegue — lo que este valor promete es
    /// exactamente lo que se ha comprobado, ni una palabra más.
    /// </remarks>
    VerificadoPorAlgoritmo = 1,
}
