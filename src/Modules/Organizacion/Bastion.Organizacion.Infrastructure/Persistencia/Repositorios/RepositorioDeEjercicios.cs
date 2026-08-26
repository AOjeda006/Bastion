using Bastion.Organizacion.Application.Ejercicios;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Domain.Ejercicios;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeEjercicios"/>
internal sealed class RepositorioDeEjercicios(OrganizacionDbContext contexto) : IRepositorioDeEjercicios
{
    public Task<Ejercicio?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Ejercicios.FirstOrDefaultAsync(ejercicio => ejercicio.Id == id, cancelacion);

    public Task<bool> ExisteElAnioAsync(Guid empresaId, int anio, CancellationToken cancelacion) =>
        contexto.Ejercicios.AnyAsync(
            ejercicio => ejercicio.EmpresaId == empresaId && ejercicio.Anio == anio, cancelacion);

    public Task<bool> ExisteAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Ejercicios.AnyAsync(ejercicio => ejercicio.Id == id, cancelacion);

    public Task<bool> TieneSeriesAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Series.AnyAsync(serie => serie.EjercicioId == id, cancelacion);

    // Del más reciente al más antiguo: quien abre la pantalla de ejercicios busca el que está
    // usando, que es el último, y no el de hace ocho años.
    public Task<PaginaDe<Ejercicio>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion) =>
        contexto.Ejercicios
            .OrderByDescending(ejercicio => ejercicio.FechaDeInicio)
            .ThenBy(ejercicio => ejercicio.Id)
            .PaginarAsync(paginacion, cancelacion);

    public void Agregar(Ejercicio ejercicio) => contexto.Ejercicios.Add(ejercicio);

    public void Eliminar(Ejercicio ejercicio) => contexto.Ejercicios.Remove(ejercicio);
}
