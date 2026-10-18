namespace Bastion.Organizacion.Contracts.Unidades;

/// <summary>
/// Lo que otros módulos pueden preguntar sobre las conversiones entre unidades de medida.
/// </summary>
/// <remarks>
/// <para>
/// Es la lectura entre módulos del §4, como <see cref="IConsultaDeUnidadesDeMedida"/>: interfaz del
/// <c>Contracts</c> del dueño, resuelta en proceso. La conversión es de Organización (§7.1), y quien
/// la necesita es Inventario, que pasa a unidad base lo que escribió la persona (R3, ADR-0065 §1).
/// </para>
/// <para>
/// <b>Contesta el par exacto, sin invertirlo ni encadenarlo</b> (ADR-0023). Si existe la de la caja a
/// la unidad y se pregunta por la de la unidad a la caja, no hay respuesta: la inversa redondeada a
/// seis decimales sería una conversión que nadie ha escrito.
/// </para>
/// </remarks>
public interface IConsultaDeConversiones
{
    /// <summary>La conversión de cada par, todos los pares en una sola consulta.</summary>
    /// <remarks>
    /// <para>
    /// <b>Un par sin conversión no está en el diccionario.</b> No hay un factor nulo que alguien
    /// pueda leer como cero.
    /// </para>
    /// <para>
    /// <b>La retirada se contesta, con su marca.</b> Decide quien pregunta: el alta del ajuste la
    /// rechaza, y quien lea lo viejo la necesita para explicarlo.
    /// </para>
    /// </remarks>
    /// <param name="pares">Los pares que se preguntan. Un par repetido se contesta una vez.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>La conversión de cada par que la tiene.</returns>
    Task<IReadOnlyDictionary<ParDeUnidades, ConversionResuelta>> DeLosParesAsync(
        IReadOnlyCollection<ParDeUnidades> pares,
        CancellationToken cancelacion);
}

/// <summary>Una pregunta de conversión: de qué unidad a qué unidad.</summary>
/// <param name="OrigenId">La unidad de la que se parte.</param>
/// <param name="DestinoId">La unidad a la que se llega.</param>
public readonly record struct ParDeUnidades(Guid OrigenId, Guid DestinoId);

/// <summary>Lo que se contesta de una conversión.</summary>
/// <param name="Factor">Unidades de destino que hay en una de origen.</param>
/// <param name="EstaRetirada">Si ya no se ofrece para lo nuevo (ADR-0023).</param>
public sealed record ConversionResuelta(decimal Factor, bool EstaRetirada);
