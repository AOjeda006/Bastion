using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Domain.Empresas;

namespace Bastion.Organizacion.Application.Empresas;

/// <summary>Devuelve una empresa por su identificador.</summary>
public interface IObtenerEmpresa
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la empresa.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ConVersion<EmpresaDto>>> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <summary>Devuelve una página de empresas.</summary>
/// <remarks>
/// Devuelve la página a secas y no un <c>Resultado</c>, a propósito: un listado no tiene desenlace
/// fallido de negocio. Que la paginación pedida sea absurda lo rechaza el borde con sus
/// anotaciones antes de llegar, y una colección vacía es una respuesta correcta, no un error
/// (ADR-0004: el <c>Resultado</c> es para lo que PUEDE fallar de verdad).
/// </remarks>
public interface IListarEmpresas : IListado<EmpresaDto>
{
}

/// <inheritdoc cref="IObtenerEmpresa"/>
internal sealed class ObtenerEmpresa(
    IRepositorioDeEmpresas empresas,
    IVersionesDeOrganizacion versiones) : IObtenerEmpresa
{
    public async Task<Resultado<ConVersion<EmpresaDto>>> EjecutarAsync(Guid id, CancellationToken cancelacion)
    {
        Empresa? empresa = await empresas.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        return empresa is null
            ? Resultado.Fallo<ConVersion<EmpresaDto>>(ErroresDeEmpresa.NoEncontrada(id))
            : Resultado.Correcto(new ConVersion<EmpresaDto>(empresa.ADto(), versiones.De(empresa)));
    }
}

/// <inheritdoc cref="IListarEmpresas"/>
internal sealed class ListarEmpresas(IRepositorioDeEmpresas empresas) : IListarEmpresas
{
    public IReadOnlySet<string> CamposOrdenables => empresas.CamposOrdenables;

    public async Task<PaginaDe<EmpresaDto>> EjecutarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion)
    {
        PaginaDe<Empresa> pagina = await empresas.ListarAsync(paginacion, cancelacion).ConfigureAwait(false);

        return new PaginaDe<EmpresaDto>(
            [.. pagina.Elementos.Select(empresa => empresa.ADto())],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}
