using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Contracts.Unidades;
using Bastion.Organizacion.Domain.Unidades;

namespace Bastion.Organizacion.Application.Unidades;

/// <summary>
/// Cambia el nombre de una unidad de medida.
/// </summary>
/// <remarks>
/// Los decimales no se tocan: bajarlos dejaría inválidas las existencias ya registradas con más
/// precisión —los 1,250 kg que hay en una estantería— sin tocarlas ni avisar. Una unidad con otra
/// precisión es otra unidad.
/// </remarks>
public interface IModificarUnidadMedida
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la unidad.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="peticion">Los datos nuevos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<UnidadMedidaDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarUnidadMedidaDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IModificarUnidadMedida"/>
internal sealed class ModificarUnidadMedida(
    IRepositorioDeUnidadesDeMedida unidades,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones) : IModificarUnidadMedida
{
    public async Task<Resultado<UnidadMedidaDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarUnidadMedidaDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        UnidadMedida? unidad = await unidades.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (unidad is null)
        {
            return Resultado.Fallo<UnidadMedidaDto>(ErroresDeUnidad.NoEncontrada(id));
        }

        versiones.Exigir(unidad, version);

        unidad.Modificar(peticion.Nombre);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(unidad.ADto());
    }
}
