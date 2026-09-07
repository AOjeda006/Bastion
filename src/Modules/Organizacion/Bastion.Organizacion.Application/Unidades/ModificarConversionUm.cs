using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Contracts.Unidades;
using Bastion.Organizacion.Domain.Unidades;

namespace Bastion.Organizacion.Application.Unidades;

/// <summary>
/// Corrige el factor de una conversión.
/// </summary>
/// <remarks>
/// El par de unidades no: es la identidad de la fila —hay un índice único sobre él—, así que
/// cambiarlo no sería corregir esta conversión sino inventar otra.
/// </remarks>
public interface IModificarConversionUm
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la conversión.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="peticion">El factor nuevo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ConversionUmDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarConversionUmDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IModificarConversionUm"/>
internal sealed class ModificarConversionUm(
    IRepositorioDeConversiones conversiones,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones) : IModificarConversionUm
{
    public async Task<Resultado<ConversionUmDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarConversionUmDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        ConversionUM? conversion = await conversiones.ObtenerAsync(id, cancelacion)
            .ConfigureAwait(false);

        if (conversion is null)
        {
            return Resultado.Fallo<ConversionUmDto>(ErroresDeUnidad.ConversionNoEncontrada(id));
        }

        versiones.Exigir(conversion, version);

        // La misma comprobación que en el alta, y NO es un extra. Si se da de alta A→B, luego B→A,
        // y después alguien modifica la primera, el par vuelve a poder contradecirse: una regla que
        // solo mirase el alta deja pasar exactamente el camino por el que se rompe. Se compara
        // contra la inversa TAL COMO ESTÉ en este momento, retirada incluida.
        ConversionUM? inversa = await conversiones
            .DelParAsync(conversion.UnidadDestinoId, conversion.UnidadOrigenId, cancelacion)
            .ConfigureAwait(false);

        Resultado plausible = LaInversaEsPlausible.Comprobar(
            inversa, ConversionUM.RedondearFactor(peticion.Factor));

        if (!plausible.EsCorrecto)
        {
            return Resultado.Fallo<ConversionUmDto>(plausible.Error!);
        }

        conversion.Modificar(peticion.Factor);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(conversion.ADto());
    }
}
