using Bastion.Organizacion.Application.Almacenes;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Domain.Almacenes;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeAlmacenes"/>
internal sealed class RepositorioDeAlmacenes(OrganizacionDbContext contexto) : IRepositorioDeAlmacenes
{
    public Task<Almacen?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Almacenes.FirstOrDefaultAsync(almacen => almacen.Id == id, cancelacion);

    public Task<bool> ExisteElCodigoAsync(Guid empresaId, string codigo, CancellationToken cancelacion) =>
        contexto.Almacenes.AnyAsync(
            almacen => almacen.EmpresaId == empresaId && almacen.Codigo == codigo, cancelacion);

    public Task<PaginaDe<Almacen>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion) =>
        contexto.Almacenes
            .OrderBy(almacen => almacen.Codigo)
            .ThenBy(almacen => almacen.Id)
            .PaginarAsync(paginacion, cancelacion);

    public void Agregar(Almacen almacen) => contexto.Almacenes.Add(almacen);
}
