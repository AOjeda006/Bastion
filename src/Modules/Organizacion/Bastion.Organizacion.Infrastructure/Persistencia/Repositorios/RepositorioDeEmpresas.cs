using Bastion.BuildingBlocks.Domain.Identificacion;
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

    // Se compara el OBJETO entero, no `empresa.Nif.Valor`. El NIF está mapeado con un conversor
    // de valor, así que para EF Core la columna es un escalar y no hay ningún `.Valor` en el que
    // entrar: la versión con la cadena compilaba igual de bien y reventaba en ejecución con «no
    // se pudo traducir la expresión», o sea, un 500 en cada alta de empresa. Un doble en memoria
    // habría evaluado ese `.Valor` en LINQ-to-Objects y habría dado verde.
    //
    // Comparar el objeto también usa el índice único, porque el conversor produce exactamente el
    // valor normalizado que está en la columna.
    public Task<bool> ExisteConNifAsync(Nif nif, CancellationToken cancelacion) =>
        contexto.Empresas.AnyAsync(empresa => empresa.Nif == nif, cancelacion);

    public Task<bool> ExisteAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Empresas.AnyAsync(empresa => empresa.Id == id, cancelacion);

    public Task<bool> EstaActivaAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Empresas.AnyAsync(
            empresa => empresa.Id == id && empresa.Estado == EstadoDeEmpresa.Activa,
            cancelacion);

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
