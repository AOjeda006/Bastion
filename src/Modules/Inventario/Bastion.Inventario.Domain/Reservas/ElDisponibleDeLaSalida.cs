using Bastion.Inventario.Domain.Valoraciones;

namespace Bastion.Inventario.Domain.Reservas;

/// <summary>Una clave de la que una salida saca más de lo disponible (ADR-0059 §9).</summary>
/// <param name="Clave">El artículo y el almacén.</param>
/// <param name="Sale">Lo que sacan las líneas de la clave, en la unidad base.</param>
/// <param name="Disponible">El físico menos lo reservado, leídos con la valoración bloqueada.</param>
public sealed record SalidaPorEncimaDelDisponible(ClaveDeValoracion Clave, decimal Sale, decimal Disponible);

/// <summary>
/// Lo que una salida que no consume una reserva le pide al disponible de las claves de las que sale
/// (ADR-0059 §9): la transferencia no se lleva lo que otro tiene apartado.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pasa del disponible la clave que saca más que el físico menos lo reservado, y no más que el
/// físico.</b> Lo que pasa del físico lo contesta la guarda del hueco, el <c>422</c>
/// <c>stock-insuficiente</c>, que es más precisa: dice que no hay, y no que está apartado. Así lo que
/// ya se rechazaba se sigue rechazando con el mismo código.
/// </para>
/// <para>
/// <b>El físico es la cantidad del saldo</b>, que no cuenta lo que vuela: el tránsito de la clave no
/// se puede sacar (ADR-0059 §3). <b>Solo se miran las líneas que bajan</b>, y se suman por clave: una
/// entrada de la misma clave no da disponible a la salida.
/// </para>
/// </remarks>
public static class ElDisponibleDeLaSalida
{
    /// <summary>La primera clave, en el orden de las líneas, que pasa de su disponible.</summary>
    /// <param name="lineas">Las líneas de la salida.</param>
    /// <param name="saldos">El saldo de cada clave de las líneas, bloqueado.</param>
    /// <param name="reservado">Lo reservado de cada clave de las líneas, leído después del cerrojo.</param>
    /// <returns>La clave que pasa, con sus cifras, o <see langword="null"/> si ninguna pasa.</returns>
    public static SalidaPorEncimaDelDisponible? LoQuePasa(
        IReadOnlyList<LineaAValorar> lineas,
        IReadOnlyDictionary<ClaveDeValoracion, SaldoValorado> saldos,
        IReadOnlyDictionary<ClaveDeValoracion, decimal> reservado)
    {
        ArgumentNullException.ThrowIfNull(lineas);
        ArgumentNullException.ThrowIfNull(saldos);
        ArgumentNullException.ThrowIfNull(reservado);

        foreach (IGrouping<ClaveDeValoracion, LineaAValorar> clave in lineas
            .Where(linea => linea.Cantidad < 0m)
            .GroupBy(linea => linea.Clave))
        {
            decimal sale = -clave.Sum(linea => linea.Cantidad);
            decimal fisico = saldos[clave.Key].Cantidad;
            decimal disponible = fisico - reservado[clave.Key];

            if (sale > disponible && sale <= fisico)
            {
                return new SalidaPorEncimaDelDisponible(clave.Key, sale, disponible);
            }
        }

        return null;
    }
}
