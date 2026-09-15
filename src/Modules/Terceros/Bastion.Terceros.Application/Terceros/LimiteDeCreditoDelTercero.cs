using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Application.Validacion;
using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Terceros.Application.Comun;
using Bastion.Terceros.Contracts.Terceros;
using Bastion.Terceros.Domain.Terceros;

namespace Bastion.Terceros.Application.Terceros;

/// <summary>Cuánto se le fía a un tercero, si se le fía.</summary>
public interface IObtenerLimiteCredito
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="terceroId">La ficha.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<LimiteCreditoDto>> EjecutarAsync(Guid terceroId, CancellationToken cancelacion);
}

/// <summary>Fija —o retira— el límite de crédito de un tercero.</summary>
public interface IFijarLimiteCredito
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="terceroId">La ficha.</param>
    /// <param name="version">La versión de la FICHA sobre la que se escribe.</param>
    /// <param name="peticion">El importe con su divisa, o nada para retirarlo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<LimiteCreditoDto>> EjecutarAsync(
        Guid terceroId,
        VersionDeRecurso version,
        LimiteCreditoDeAltaDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IObtenerLimiteCredito"/>
internal sealed class ObtenerLimiteCredito(IRepositorioDeTerceros terceros)
    : IObtenerLimiteCredito
{
    public async Task<Resultado<LimiteCreditoDto>> EjecutarAsync(
        Guid terceroId,
        CancellationToken cancelacion)
    {
        Tercero? tercero = await terceros.ObtenerAsync(terceroId, cancelacion).ConfigureAwait(false);

        // `ObtenerAsync` y no la versión con lo que cuelga: el límite vive en la propia fila de la
        // ficha, así que cargarle las tres colecciones sería pagar tres consultas por nada.
        return tercero is null
            ? Resultado.Fallo<LimiteCreditoDto>(ErroresDeTercero.NoEncontrado(terceroId))
            : Resultado.Correcto(tercero.ALimiteDto());
    }
}

/// <inheritdoc cref="IFijarLimiteCredito"/>
internal sealed class FijarLimiteCredito(
    IRepositorioDeTerceros terceros,
    IUnidadTrabajoDeTerceros unidadTrabajo,
    IVersionesDeTerceros versiones) : IFijarLimiteCredito
{
    public async Task<Resultado<LimiteCreditoDto>> EjecutarAsync(
        Guid terceroId,
        VersionDeRecurso version,
        LimiteCreditoDeAltaDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Tercero? tercero = await terceros.ObtenerAsync(terceroId, cancelacion).ConfigureAwait(false);

        if (tercero is null)
        {
            return Resultado.Fallo<LimiteCreditoDto>(ErroresDeTercero.NoEncontrado(terceroId));
        }

        versiones.Exigir(tercero, version);

        var errores = new ErroresPorCampo();
        Importe? limite = LimitesDeCredito.Leer(peticion, errores);

        if (errores.Hay)
        {
            return Resultado.Fallo<LimiteCreditoDto>(errores.AError());
        }

        tercero.FijarLimiteDeCredito(limite);

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(tercero.ALimiteDto());
    }
}
