using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Domain.Ajustes;

namespace Bastion.Inventario.Application.Ajustes;

/// <summary>Tira un ajuste en borrador (ADR-0065 §5).</summary>
public interface IDescartarAjuste
{
    /// <summary>Ejecuta el descarte.</summary>
    /// <param name="ajusteId">El ajuste.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>Correcto, o el motivo por el que no se descarta.</returns>
    Task<Resultado> EjecutarAsync(Guid ajusteId, VersionDeRecurso version, CancellationToken cancelacion);
}

/// <inheritdoc cref="IDescartarAjuste"/>
/// <remarks>
/// <para>
/// <b>Descartar no mueve nada, no numera y no cuenta nada</b>: pone la marca de
/// <see cref="Ajuste.DescartadoEn"/>, y el filtro con nombre del contexto deja de traer el
/// documento. Así que tirarlo dos veces es un <c>404</c>, que es lo que es, y por eso el borde no le
/// pide <c>Idempotency-Key</c>.
/// </para>
/// <para>
/// <b>Sin cerrojo, y no hace falta</b>, a diferencia del recuento. El recuento bloquea su fila porque
/// su confirmación la bloquea; la del ajuste no, y lo que la serializa contra esto es el testigo del
/// ajuste (<c>xmin</c>): la versión leída va en el <c>WHERE</c> del <c>UPDATE</c>, así que una
/// confirmación que se cuele entre la lectura y el guardado deja este guardado sin fila y el borde
/// responde <c>412</c>.
/// </para>
/// </remarks>
/// <param name="ajustes">El documento.</param>
/// <param name="versiones">La versión del documento, que se compara con la del <c>If-Match</c>.</param>
/// <param name="unidadTrabajo">El guardado.</param>
/// <param name="reloj">De dónde sale «ahora».</param>
internal sealed class DescartarAjuste(
    IRepositorioDeAjustes ajustes,
    IVersionesDeInventario versiones,
    IUnidadTrabajoDeInventario unidadTrabajo,
    TimeProvider reloj) : IDescartarAjuste
{
    /// <inheritdoc/>
    public async Task<Resultado> EjecutarAsync(
        Guid ajusteId,
        VersionDeRecurso version,
        CancellationToken cancelacion)
    {
        Ajuste? ajuste = await ajustes.ObtenerAsync(ajusteId, cancelacion).ConfigureAwait(false);

        if (ajuste is null)
        {
            return Resultado.Fallo(ErroresDeAjuste.NoEncontrado(ajusteId));
        }

        // LA VERSIÓN ANTES QUE EL ESTADO (ADR-0057 §3): quien descarta con la versión de antes de que
        // otro lo confirmara recibe el 412, que le dice que lo que vio ya no es lo que hay, y con la
        // de ahora, el 409 de un ajuste que ya no está en borrador.
        VersionDeRecurso actual = versiones.De(ajuste);

        if (actual != version)
        {
            return Resultado.Fallo(ErroresDeConcurrencia.Obsoleta(actual));
        }

        // El estado se mira aquí aunque el dominio lo vuelva a mirar: el dominio lanza, y lo que el
        // borde necesita para un confirmado es un 409 con su motivo, no un 500 (ADR-0004).
        if (ajuste.Estado != EstadoDeAjuste.Borrador)
        {
            return Resultado.Fallo(ErroresDeAjuste.NoEstaEnBorrador(ajusteId, ajuste.Estado.ToString()));
        }

        ajuste.Descartar(reloj.GetUtcNow());

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}
