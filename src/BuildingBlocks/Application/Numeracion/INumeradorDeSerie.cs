using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.BuildingBlocks.Application.Numeracion;

/// <summary>
/// Quien entrega el siguiente número de una serie, dentro de la transacción de quien confirma
/// (R5).
/// </summary>
/// <remarks>
/// <para>
/// <b>No hay un método «consultar el siguiente número».</b> Tomarlo y verlo son la misma llamada a
/// propósito: un número mirado antes de confirmar es un número que otro documento puede llevarse
/// entre la mirada y la escritura, y quien lo hubiera enseñado ya no podría cumplirlo. El número
/// aparece cuando el documento lo tiene, y entonces ya es suyo.
/// </para>
/// <para>
/// <b>Cada módulo hereda su propio puerto de este</b>, como con la unidad de trabajo: el mecanismo
/// escribe a través del <c>DbContext</c> del módulo que confirma, porque es ahí donde está abierta
/// la transacción de la petición, y un puerto compartido entregaría el de otro módulo —o sea, un
/// número tomado en una transacción distinta de la del documento que lo lleva—.
/// </para>
/// <para>
/// <b>Y lo hereda con sus propios documentos</b>, que es para lo que existe el parámetro de tipo:
/// cada módulo nombra los suyos con su enumerado, y es su numerador quien los traduce al tipo de
/// serie que Organización guarda. El bloque común no ve el vocabulario fiscal ni tiene por qué.
/// </para>
/// </remarks>
/// <typeparam name="TDocumento">Las clases de documento que numera el módulo.</typeparam>
public interface INumeradorDeSerie<in TDocumento>
    where TDocumento : struct, Enum
{
    /// <summary>
    /// Sube el contador de la serie y devuelve el número que le ha tocado a este documento.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La empresa no es un parámetro</b>, y la ausencia es la garantía: sale del mismo sitio del
    /// que la toma el filtro global de inquilinato (R8), no de quien llama. Pedirla dejaría que un
    /// llamante numerase contra la serie de otra sociedad sin más que escribir su identificador.
    /// </para>
    /// <para>
    /// <b>Exige una transacción ya abierta</b> y <b>lanza</b> si no la hay: el número y el documento
    /// que lo lleva se confirman juntos o no se confirma ninguno. Ver la implementación.
    /// </para>
    /// <para>
    /// <b>La serie tiene que ser de este documento y de este ejercicio</b>, y las dos cosas las
    /// comprueba la sentencia, no quien llama (R5). El documento dice de qué tipo tiene que ser la
    /// serie; la fecha, en qué ejercicio tiene que colgar.
    /// </para>
    /// <para>
    /// <b>Qué fecha se pasa lo decide quien llama, y esa es la puerta de las excepciones.</b> Un
    /// documento normal pasa la suya. El inverso de un ajuste pasa <b>la de su original</b>, porque
    /// numera en la serie del original aunque lleve la fecha de hoy: si tuviera que numerar en la
    /// del ejercicio de hoy, anular dependería de que existiera esa serie, y la R2 promete que
    /// anular se puede siempre. Eso vale para los ajustes y no es regla para todos: una factura
    /// rectificativa exige serie propia, y quien la numere elegirá esa serie y pasará su fecha.
    /// </para>
    /// </remarks>
    /// <param name="serieId">La serie de la que se numera, elegida al abrir el documento.</param>
    /// <param name="documento">Qué clase de documento pide el número.</param>
    /// <param name="fechaQueDecideElEjercicio">
    /// La fecha que tiene que caer dentro del ejercicio de la serie: la del documento, salvo la
    /// excepción que quien llama escriba y justifique.
    /// </param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>
    /// El número; o el fallo de una serie que no numera —que no existe, que está cerrada o que no
    /// es de esta empresa—, de una serie de otro documento, o de una fecha fuera de su ejercicio.
    /// </returns>
    Task<Resultado<long>> TomarNumeroAsync(
        Guid serieId,
        TDocumento documento,
        DateOnly fechaQueDecideElEjercicio,
        CancellationToken cancelacion);
}
