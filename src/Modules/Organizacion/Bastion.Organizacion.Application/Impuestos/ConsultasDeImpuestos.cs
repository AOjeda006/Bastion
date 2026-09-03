using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Impuestos;
using Bastion.Organizacion.Domain.Impuestos;

namespace Bastion.Organizacion.Application.Impuestos;

/// <summary>Devuelve un tramo de impuesto por su identificador.</summary>
public interface IObtenerImpuesto
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del tramo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ConVersion<ImpuestoDto>>> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <summary>Devuelve una página de tramos de impuesto.</summary>
public interface IListarImpuestos : IListado<ImpuestoDto>
{
}

/// <inheritdoc cref="IObtenerImpuesto"/>
internal sealed class ObtenerImpuesto(
    IRepositorioDeImpuestos impuestos,
    IVersionesDeOrganizacion versiones) : IObtenerImpuesto
{
    public async Task<Resultado<ConVersion<ImpuestoDto>>> EjecutarAsync(
        Guid id,
        CancellationToken cancelacion)
    {
        Impuesto? impuesto = await impuestos.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        return impuesto is null
            ? Resultado.Fallo<ConVersion<ImpuestoDto>>(ErroresDeImpuesto.NoEncontrado(id))
            : Resultado.Correcto(new ConVersion<ImpuestoDto>(impuesto.ADto(), versiones.De(impuesto)));
    }
}

/// <inheritdoc cref="IListarImpuestos"/>
internal sealed class ListarImpuestos(IRepositorioDeImpuestos impuestos) : IListarImpuestos
{
    public IReadOnlySet<string> CamposOrdenables => impuestos.CamposOrdenables;

    public async Task<PaginaDe<ImpuestoDto>> EjecutarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion)
    {
        PaginaDe<Impuesto> pagina = await impuestos.ListarAsync(paginacion, cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<ImpuestoDto>(
            [.. pagina.Elementos.Select(impuesto => impuesto.ADto())],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}
