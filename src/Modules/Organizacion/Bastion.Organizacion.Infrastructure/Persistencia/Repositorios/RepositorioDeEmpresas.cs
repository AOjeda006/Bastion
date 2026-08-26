using Bastion.Organizacion.Application.Empresas;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Domain.Empresas;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeEmpresas"/>
internal sealed class RepositorioDeEmpresas(OrganizacionDbContext contexto) : IRepositorioDeEmpresas
{
    public Task<Empresa?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Empresas.FirstOrDefaultAsync(empresa => empresa.Id == id, cancelacion);

    // Sobre el valor ya normalizado: la columna guarda el NIF normalizado y sobre ella hay un
    // índice único. Comparar contra lo que escribió el usuario no usaría el índice y, peor,
    // dejaría pasar duplicados escritos de otra forma.
    public Task<bool> ExisteConNifAsync(string nif, CancellationToken cancelacion) =>
        contexto.Empresas.AnyAsync(empresa => empresa.Nif.Valor == nif, cancelacion);

    public Task<bool> ExisteAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Empresas.AnyAsync(empresa => empresa.Id == id, cancelacion);

    // Orden estable y explícito. Sin `ORDER BY`, PostgreSQL no promete ningún orden entre
    // consultas, así que la página 2 podría repetir o saltarse filas de la 1 sin que nadie
    // hubiera tocado nada.
    public Task<PaginaDe<Empresa>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion) =>
        contexto.Empresas
            .OrderBy(empresa => empresa.RazonSocial)
            .ThenBy(empresa => empresa.Id)
            .PaginarAsync(paginacion, cancelacion);

    public void Agregar(Empresa empresa) => contexto.Empresas.Add(empresa);
}
