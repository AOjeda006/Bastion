using Bastion.BuildingBlocks.Domain.Fechas;

namespace Bastion.Inventario.Domain.Transferencias;

/// <summary>
/// Las zonas de las dos puntas de una transferencia, como están al comprobar (ADR-0079): la del
/// origen dice el día del envío, y la del destino, el de la recepción.
/// </summary>
/// <remarks>
/// <para>
/// <b>Se leen al comprobar, y no se guardan.</b> Una fecha de negocio no lleva zona (R14): el
/// documento guarda dos días, cada uno el de su almacén, y la zona de cada almacén se pide al puerto
/// de las zonas cuando hay que comparar. Si una cambió por medio, se compara con la de ahora, y lo
/// que ya está fechado no se mueve.
/// </para>
/// <para>
/// <b>Comparar las dos fechas es comparar instantes</b>, porque cada una es de una zona: la península
/// va una hora por delante de Canarias, y lo que sale de Madrid a las 00:30 del 26 llega a Canarias el
/// 25. Un día va antes que otro si acaba antes de que el otro empiece.
/// </para>
/// </remarks>
public sealed record ZonasDeLaTransferencia
{
    /// <summary>Las dos zonas, cada una la de su almacén.</summary>
    /// <param name="delOrigen">La del almacén de donde sale.</param>
    /// <param name="delDestino">La del almacén a donde llega.</param>
    public ZonasDeLaTransferencia(ZonaHoraria delOrigen, ZonaHoraria delDestino)
    {
        ArgumentNullException.ThrowIfNull(delOrigen);
        ArgumentNullException.ThrowIfNull(delDestino);

        DelOrigen = delOrigen;
        DelDestino = delDestino;
    }

    /// <summary>La zona del almacén de origen: la de la fecha de envío.</summary>
    public ZonaHoraria DelOrigen { get; }

    /// <summary>La zona del almacén de destino: la de la fecha de recepción.</summary>
    public ZonaHoraria DelDestino { get; }

    /// <summary>Si una recepción de un día no va antes que un envío de otro.</summary>
    /// <param name="recepcion">El día de la recepción, en la zona del destino.</param>
    /// <param name="envio">El del envío, en la del origen.</param>
    /// <returns><c>true</c> si el día de la recepción no acaba antes de que empiece el del envío.</returns>
    public bool LaRecepcionNoVaAntesDelEnvio(DateOnly recepcion, DateOnly envio) =>
        DiaDeNegocio.NoAcabaAntesDe(recepcion, DelDestino, envio, DelOrigen);

    /// <summary>El primer día que admite la recepción de un envío: el anterior ya va antes.</summary>
    /// <remarks>
    /// Es el día del destino en el que empieza el del envío, la misma cuenta que hace
    /// <see cref="LaRecepcionNoVaAntesDelEnvio"/>. Hacia Canarias, desde la península o Ceuta, es el
    /// anterior al del envío; en los demás pares, el mismo. Lo publica la ficha para el formulario.
    /// </remarks>
    /// <param name="envio">El día del envío, en la zona del origen.</param>
    /// <returns>Un día de la zona del destino.</returns>
    public DateOnly LaPrimeraRecepcionDe(DateOnly envio) =>
        DiaDeNegocio.De(DiaDeNegocio.Inicio(envio, DelOrigen), DelDestino);
}
