using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Domain.Almacenes;

namespace Bastion.Organizacion.Application.Almacenes;

/// <summary>Devuelve un almacén por su identificador.</summary>
public interface IObtenerAlmacen
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del almacén.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ConVersion<AlmacenDto>>> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <summary>Devuelve una página de almacenes.</summary>
public interface IListarAlmacenes : IListado<AlmacenDto>
{
}

/// <inheritdoc cref="IObtenerAlmacen"/>
internal sealed class ObtenerAlmacen(
    IRepositorioDeAlmacenes almacenes,
    IVersionesDeOrganizacion versiones) : IObtenerAlmacen
{
    public async Task<Resultado<ConVersion<AlmacenDto>>> EjecutarAsync(Guid id, CancellationToken cancelacion)
    {
        Almacen? almacen = await almacenes.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        return almacen is null
            ? Resultado.Fallo<ConVersion<AlmacenDto>>(ErroresDeAlmacen.NoEncontrado(id))
            : Resultado.Correcto(new ConVersion<AlmacenDto>(almacen.ADto(), versiones.De(almacen)));
    }
}

/// <inheritdoc cref="IListarAlmacenes"/>
internal sealed class ListarAlmacenes(IRepositorioDeAlmacenes almacenes) : IListarAlmacenes
{
    public IReadOnlySet<string> CamposOrdenables => almacenes.CamposOrdenables;

    public async Task<PaginaDe<AlmacenDto>> EjecutarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion)
    {
        PaginaDe<Almacen> pagina = await almacenes.ListarAsync(paginacion, cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<AlmacenDto>(
            [.. pagina.Elementos.Select(almacen => almacen.ADto())],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}
