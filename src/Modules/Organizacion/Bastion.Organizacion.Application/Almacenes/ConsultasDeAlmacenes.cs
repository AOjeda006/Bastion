using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Fechas;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Application.Empresas;
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
    IRepositorioDeEmpresas empresas,
    IVersionesDeOrganizacion versiones) : IObtenerAlmacen
{
    public async Task<Resultado<ConVersion<AlmacenDto>>> EjecutarAsync(Guid id, CancellationToken cancelacion)
    {
        Almacen? almacen = await almacenes.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (almacen is null)
        {
            return Resultado.Fallo<ConVersion<AlmacenDto>>(ErroresDeAlmacen.NoEncontrado(id));
        }

        ZonaHoraria zona = await empresas.ZonaParaPublicarAsync(almacen.EmpresaId, cancelacion)
            .ConfigureAwait(false);

        return Resultado.Correcto(new ConVersion<AlmacenDto>(almacen.ADto(zona), versiones.De(almacen)));
    }
}

/// <inheritdoc cref="IListarAlmacenes"/>
internal sealed class ListarAlmacenes(
    IUsuarioActual usuarioActual,
    IRepositorioDeAlmacenes almacenes,
    IRepositorioDeEmpresas empresas) : IListarAlmacenes
{
    public IReadOnlySet<string> CamposOrdenables => almacenes.CamposOrdenables;

    public async Task<PaginaDe<AlmacenDto>> EjecutarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion)
    {
        PaginaDe<Almacen> pagina = await almacenes.ListarAsync(paginacion, cancelacion)
            .ConfigureAwait(false);

        // Todos son de la empresa del claim, que es la única que deja ver el filtro de R8: una
        // sola zona para la página entera.
        ZonaHoraria zona = await empresas.ZonaParaPublicarAsync(usuarioActual.EmpresaId, cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<AlmacenDto>(
            [.. pagina.Elementos.Select(almacen => almacen.ADto(zona))],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}
