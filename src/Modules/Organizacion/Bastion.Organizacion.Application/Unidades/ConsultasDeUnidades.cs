using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Unidades;
using Bastion.Organizacion.Domain.Unidades;

namespace Bastion.Organizacion.Application.Unidades;

/// <summary>Devuelve una unidad de medida por su identificador.</summary>
public interface IObtenerUnidadMedida
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la unidad.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ConVersion<UnidadMedidaDto>>> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <summary>Devuelve una página de unidades de medida.</summary>
public interface IListarUnidadesDeMedida : IListado<UnidadMedidaDto>
{
}

/// <summary>Devuelve una conversión por su identificador.</summary>
public interface IObtenerConversionUm
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la conversión.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ConVersion<ConversionUmDto>>> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <summary>Devuelve una página de conversiones.</summary>
public interface IListarConversionesUm : IListado<ConversionUmDto>
{
}

/// <inheritdoc cref="IObtenerUnidadMedida"/>
internal sealed class ObtenerUnidadMedida(
    IRepositorioDeUnidadesDeMedida unidades,
    IVersionesDeOrganizacion versiones) : IObtenerUnidadMedida
{
    public async Task<Resultado<ConVersion<UnidadMedidaDto>>> EjecutarAsync(
        Guid id,
        CancellationToken cancelacion)
    {
        UnidadMedida? unidad = await unidades.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        return unidad is null
            ? Resultado.Fallo<ConVersion<UnidadMedidaDto>>(ErroresDeUnidad.NoEncontrada(id))
            : Resultado.Correcto(new ConVersion<UnidadMedidaDto>(unidad.ADto(), versiones.De(unidad)));
    }
}

/// <inheritdoc cref="IListarUnidadesDeMedida"/>
internal sealed class ListarUnidadesDeMedida(IRepositorioDeUnidadesDeMedida unidades)
    : IListarUnidadesDeMedida
{
    public IReadOnlySet<string> CamposOrdenables => unidades.CamposOrdenables;

    public async Task<PaginaDe<UnidadMedidaDto>> EjecutarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion)
    {
        PaginaDe<UnidadMedida> pagina = await unidades.ListarAsync(paginacion, cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<UnidadMedidaDto>(
            [.. pagina.Elementos.Select(unidad => unidad.ADto())],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}

/// <inheritdoc cref="IObtenerConversionUm"/>
internal sealed class ObtenerConversionUm(
    IRepositorioDeConversiones conversiones,
    IVersionesDeOrganizacion versiones) : IObtenerConversionUm
{
    public async Task<Resultado<ConVersion<ConversionUmDto>>> EjecutarAsync(
        Guid id,
        CancellationToken cancelacion)
    {
        ConversionUM? conversion = await conversiones.ObtenerAsync(id, cancelacion)
            .ConfigureAwait(false);

        return conversion is null
            ? Resultado.Fallo<ConVersion<ConversionUmDto>>(ErroresDeUnidad.ConversionNoEncontrada(id))
            : Resultado.Correcto(
                new ConVersion<ConversionUmDto>(conversion.ADto(), versiones.De(conversion)));
    }
}

/// <inheritdoc cref="IListarConversionesUm"/>
internal sealed class ListarConversionesUm(IRepositorioDeConversiones conversiones)
    : IListarConversionesUm
{
    public IReadOnlySet<string> CamposOrdenables => conversiones.CamposOrdenables;

    public async Task<PaginaDe<ConversionUmDto>> EjecutarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion)
    {
        PaginaDe<ConversionUM> pagina = await conversiones.ListarAsync(paginacion, cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<ConversionUmDto>(
            [.. pagina.Elementos.Select(conversion => conversion.ADto())],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}
