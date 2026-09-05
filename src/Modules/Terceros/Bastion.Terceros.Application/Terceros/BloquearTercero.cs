using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Bloqueos;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Terceros.Domain.Terceros;

namespace Bastion.Terceros.Application.Terceros;

/// <summary>
/// Bloquea un tercero. Es lo que hace el <c>DELETE</c> del recurso.
/// </summary>
/// <remarks>
/// <b>Aquí el motivo es el que la ley nombra</b>, a diferencia del almacén: un tercero puede ser
/// una persona física, y lo que procede cuando ejerce su derecho de supresión es identificar sus
/// datos y reservarlos, no borrarlos (art. 32 de la LOPDGDD). Las facturas que ya se le emitieron
/// tienen que seguir cuadrando (R15), y sus datos fiscales viven copiados en cada documento
/// (§7.7), no colgando de esta ficha.
/// </remarks>
public interface IBloquearTercero
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del tercero.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, VersionDeRecurso version, CancellationToken cancelacion);
}

/// <inheritdoc cref="IBloquearTercero"/>
internal sealed class BloquearTercero(
    IRepositorioDeTerceros terceros,
    IUnidadTrabajoDeTerceros unidadTrabajo,
    IVersionesDeTerceros versiones,
    TimeProvider reloj) : IBloquearTercero
{
    public async Task<Resultado> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        CancellationToken cancelacion)
    {
        Tercero? tercero = await terceros.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (tercero is null)
        {
            return Resultado.Fallo(ErroresDeTercero.NoEncontrado(id));
        }

        versiones.Exigir(tercero, version);

        tercero.Bloquear(MotivoDeBloqueo.SupresionSolicitada, reloj.GetUtcNow());
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}
