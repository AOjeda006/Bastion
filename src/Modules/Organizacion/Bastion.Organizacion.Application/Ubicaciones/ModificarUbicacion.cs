using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Bastion.Organizacion.Domain.Ubicaciones;

namespace Bastion.Organizacion.Application.Ubicaciones;

/// <summary>
/// Cambia las coordenadas y la descripción de una ubicación.
/// </summary>
/// <remarks>
/// Ni el código ni el almacén. El código va impreso en la etiqueta pegada al estante, y mover una
/// ubicación de almacén sería mover la mercancía que hay dentro sin registrar ni un movimiento.
/// </remarks>
public interface IModificarUbicacion
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la ubicación.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="peticion">Los datos nuevos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<UbicacionDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarUbicacionDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IModificarUbicacion"/>
internal sealed class ModificarUbicacion(
    IRepositorioDeUbicaciones ubicaciones,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones) : IModificarUbicacion
{
    public async Task<Resultado<UbicacionDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarUbicacionDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Ubicacion? ubicacion = await ubicaciones.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (ubicacion is null)
        {
            return Resultado.Fallo<UbicacionDto>(ErroresDeUbicacion.NoEncontrada(id));
        }

        versiones.Exigir(ubicacion, version);

        // Sin comprobar el bloqueo, igual que en el almacén y por lo mismo: la consulta de arriba
        // ya no trae lo bloqueado, así que la respuesta ordinaria a modificar una ubicación
        // bloqueada es ese 404. La invariante sigue dentro de la entidad, donde protege a quien
        // llegue desde un ámbito abierto a propósito.
        ubicacion.Modificar(
            peticion.Pasillo, peticion.Estante, peticion.Hueco, peticion.Descripcion);

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(ubicacion.ADto());
    }
}
