using Bastion.Organizacion.Application.Ubicaciones;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Domain.Ubicaciones;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeUbicaciones"/>
internal sealed class RepositorioDeUbicaciones(OrganizacionDbContext contexto)
    : IRepositorioDeUbicaciones
{
    public Task<Ubicacion?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Ubicaciones.FirstOrDefaultAsync(ubicacion => ubicacion.Id == id, cancelacion);

    // Sin comparar la empresa a mano: el filtro global del contexto ya la impone sobre CUALQUIER
    // consulta a esta tabla (R8), y repetirlo aquí daría a entender que hace falta escribirlo —y
    // por tanto que se puede olvidar— en la siguiente consulta que alguien añada.
    public Task<bool> ExisteElCodigoAsync(
        Guid almacenId,
        string codigo,
        CancellationToken cancelacion) =>
        contexto.Ubicaciones.AnyAsync(
            ubicacion => ubicacion.AlmacenId == almacenId && ubicacion.Codigo == codigo,
            cancelacion);

    public Task<PaginaDe<Ubicacion>> ListarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion) =>
        contexto.Ubicaciones
            .OrderBy(ubicacion => ubicacion.AlmacenId)
            .ThenBy(ubicacion => ubicacion.Codigo)
            .ThenBy(ubicacion => ubicacion.Id)
            .PaginarAsync(paginacion, cancelacion);

    public void Agregar(Ubicacion ubicacion) => contexto.Ubicaciones.Add(ubicacion);
}
