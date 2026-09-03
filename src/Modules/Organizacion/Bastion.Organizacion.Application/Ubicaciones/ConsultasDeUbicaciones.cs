using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Bastion.Organizacion.Domain.Ubicaciones;

namespace Bastion.Organizacion.Application.Ubicaciones;

/// <summary>Devuelve una ubicación por su identificador.</summary>
public interface IObtenerUbicacion
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la ubicación.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ConVersion<UbicacionDto>>> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <summary>Devuelve una página de ubicaciones.</summary>
public interface IListarUbicaciones : IListado<UbicacionDto>
{
}

/// <inheritdoc cref="IObtenerUbicacion"/>
internal sealed class ObtenerUbicacion(
    IRepositorioDeUbicaciones ubicaciones,
    IVersionesDeOrganizacion versiones) : IObtenerUbicacion
{
    public async Task<Resultado<ConVersion<UbicacionDto>>> EjecutarAsync(
        Guid id,
        CancellationToken cancelacion)
    {
        Ubicacion? ubicacion = await ubicaciones.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        return ubicacion is null
            ? Resultado.Fallo<ConVersion<UbicacionDto>>(ErroresDeUbicacion.NoEncontrada(id))
            : Resultado.Correcto(
                new ConVersion<UbicacionDto>(ubicacion.ADto(), versiones.De(ubicacion)));
    }
}

/// <inheritdoc cref="IListarUbicaciones"/>
internal sealed class ListarUbicaciones(IRepositorioDeUbicaciones ubicaciones) : IListarUbicaciones
{
    public IReadOnlySet<string> CamposOrdenables => ubicaciones.CamposOrdenables;

    public async Task<PaginaDe<UbicacionDto>> EjecutarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion)
    {
        PaginaDe<Ubicacion> pagina = await ubicaciones.ListarAsync(paginacion, cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<UbicacionDto>(
            [.. pagina.Elementos.Select(ubicacion => ubicacion.ADto())],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}
