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
/// </remarks>
public interface INumeradorDeSerie
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
    /// </remarks>
    /// <param name="serieId">La serie de la que se numera, elegida al abrir el documento.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>
    /// El número, o el fallo de una serie que no numera —que no existe, que está cerrada o que no
    /// es de esta empresa—.
    /// </returns>
    Task<Resultado<long>> TomarNumeroAsync(Guid serieId, CancellationToken cancelacion);
}
