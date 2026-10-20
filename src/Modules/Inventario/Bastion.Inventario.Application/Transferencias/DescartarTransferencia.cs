using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Domain.Transferencias;

namespace Bastion.Inventario.Application.Transferencias;

/// <summary>Tira una transferencia en borrador (ADR-0068 §5).</summary>
public interface IDescartarTransferencia
{
    /// <summary>Ejecuta el descarte.</summary>
    /// <param name="transferenciaId">La transferencia.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>Correcto, o el motivo por el que no se descarta.</returns>
    Task<Resultado> EjecutarAsync(Guid transferenciaId, VersionDeRecurso version, CancellationToken cancelacion);
}

/// <inheritdoc cref="IDescartarTransferencia"/>
/// <remarks>
/// <para>
/// <b>Como el descarte del ajuste, punto por punto</b>: no mueve nada, no numera y no cuenta nada.
/// Pone la marca de <see cref="Transferencia.DescartadoEn"/>, y el filtro con nombre del contexto deja
/// de traer el documento. Tirarlo dos veces es un <c>404</c>, y por eso el borde no le pide
/// <c>Idempotency-Key</c>.
/// </para>
/// <para>
/// <b>Sin cerrojo</b>. El envío bloquea la valoración del origen, no la fila del documento, y lo que
/// lo serializa contra esto es el testigo de la transferencia (<c>xmin</c>): la versión leída va en
/// el <c>WHERE</c> del <c>UPDATE</c>, así que un envío que se cuele entre la lectura y el guardado
/// deja este guardado sin fila, y el borde responde <c>412</c>.
/// </para>
/// </remarks>
/// <param name="transferencias">El documento.</param>
/// <param name="versiones">La versión del documento, que se compara con la del <c>If-Match</c>.</param>
/// <param name="unidadTrabajo">El guardado.</param>
/// <param name="reloj">De dónde sale «ahora».</param>
internal sealed class DescartarTransferencia(
    IRepositorioDeTransferencias transferencias,
    IVersionesDeInventario versiones,
    IUnidadTrabajoDeInventario unidadTrabajo,
    TimeProvider reloj) : IDescartarTransferencia
{
    /// <inheritdoc/>
    public async Task<Resultado> EjecutarAsync(
        Guid transferenciaId,
        VersionDeRecurso version,
        CancellationToken cancelacion)
    {
        Transferencia? transferencia = await transferencias
            .ObtenerAsync(transferenciaId, cancelacion)
            .ConfigureAwait(false);

        if (transferencia is null)
        {
            return Resultado.Fallo(ErroresDeTransferencia.NoEncontrada(transferenciaId));
        }

        // LA VERSIÓN ANTES QUE EL ESTADO (ADR-0057 §3): quien descarta con la versión de antes de que
        // otro la enviara recibe el 412, que le dice que lo que vio ya no es lo que hay, y con la de
        // ahora, el 409 de una transferencia que ya no está en borrador.
        VersionDeRecurso actual = versiones.De(transferencia);

        if (actual != version)
        {
            return Resultado.Fallo(ErroresDeConcurrencia.Obsoleta(actual));
        }

        // El estado se mira aquí aunque el dominio lo vuelva a mirar: el dominio lanza, y lo que el
        // borde necesita para una enviada es un 409 con su motivo, no un 500 (ADR-0004).
        if (transferencia.Estado != EstadoDeTransferencia.Borrador)
        {
            return Resultado.Fallo(
                ErroresDeTransferencia.NoEstaEnBorrador(transferenciaId, transferencia.Estado));
        }

        transferencia.Descartar(reloj.GetUtcNow());

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}
