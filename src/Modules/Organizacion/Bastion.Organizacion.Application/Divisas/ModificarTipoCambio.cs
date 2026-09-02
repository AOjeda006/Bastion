using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Contracts.Divisas;
using Bastion.Organizacion.Domain.Divisas;

namespace Bastion.Organizacion.Application.Divisas;

/// <summary>
/// Rectifica la tasa de una cotización ya registrada.
/// </summary>
/// <remarks>
/// Ni el par ni la fecha: los tres juntos son la identidad de la fila —hay un índice único sobre
/// ellos—, así que cambiarlos no sería corregir esta cotización sino inventar otra.
/// </remarks>
public interface IModificarTipoCambio
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la cotización.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="peticion">La tasa nueva.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<TipoCambioDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarTipoCambioDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IModificarTipoCambio"/>
internal sealed class ModificarTipoCambio(
    IRepositorioDeTiposDeCambio cambios,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones) : IModificarTipoCambio
{
    public async Task<Resultado<TipoCambioDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarTipoCambioDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        TipoCambio? cambio = await cambios.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (cambio is null)
        {
            return Resultado.Fallo<TipoCambioDto>(ErroresDeDivisa.CambioNoEncontrado(id));
        }

        versiones.Exigir(cambio, version);

        cambio.Modificar(peticion.Tasa);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(cambio.ADto());
    }
}
