using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Ejercicios;
using Bastion.Organizacion.Domain.Ejercicios;

namespace Bastion.Organizacion.Application.Ejercicios;

/// <summary>Devuelve un ejercicio por su identificador.</summary>
public interface IObtenerEjercicio
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del ejercicio.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ConVersion<EjercicioDto>>> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <summary>Devuelve una página de ejercicios.</summary>
public interface IListarEjercicios : IListado<EjercicioDto>
{
}

/// <inheritdoc cref="IObtenerEjercicio"/>
internal sealed class ObtenerEjercicio(
    IRepositorioDeEjercicios ejercicios,
    IVersionesDeOrganizacion versiones) : IObtenerEjercicio
{
    public async Task<Resultado<ConVersion<EjercicioDto>>> EjecutarAsync(Guid id, CancellationToken cancelacion)
    {
        Ejercicio? ejercicio = await ejercicios.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        return ejercicio is null
            ? Resultado.Fallo<ConVersion<EjercicioDto>>(ErroresDeEjercicio.NoEncontrado(id))
            : Resultado.Correcto(new ConVersion<EjercicioDto>(ejercicio.ADto(), versiones.De(ejercicio)));
    }
}

/// <inheritdoc cref="IListarEjercicios"/>
internal sealed class ListarEjercicios(IRepositorioDeEjercicios ejercicios) : IListarEjercicios
{
    public IReadOnlySet<string> CamposOrdenables => ejercicios.CamposOrdenables;

    public async Task<PaginaDe<EjercicioDto>> EjecutarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion)
    {
        PaginaDe<Ejercicio> pagina = await ejercicios.ListarAsync(paginacion, cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<EjercicioDto>(
            [.. pagina.Elementos.Select(ejercicio => ejercicio.ADto())],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}
