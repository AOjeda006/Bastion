using System.Linq.Expressions;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Identidad.Application.Roles;
using Bastion.Identidad.Domain.Roles;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Identidad.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeRoles"/>
internal sealed class RepositorioDeRoles(IdentidadDbContext contexto) : IRepositorioDeRoles
{
    private IQueryable<Rol> ConPermisos => contexto.Roles.Include(rol => rol.Permisos);

    public Task<Rol?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        ConPermisos.FirstOrDefaultAsync(rol => rol.Id == id, cancelacion);

    public Task<Rol?> ObtenerPorCodigoAsync(string codigo, CancellationToken cancelacion) =>
        ConPermisos.FirstOrDefaultAsync(rol => rol.Codigo == codigo, cancelacion);

    public Task<bool> ExisteConCodigoAsync(string codigo, CancellationToken cancelacion) =>
        contexto.Roles.AnyAsync(rol => rol.Codigo == codigo, cancelacion);

    public Task<bool> ExisteAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Roles.AnyAsync(rol => rol.Id == id, cancelacion);

    private static readonly CriteriosDe<Rol> s_criterios = new()
    {
        Ordenables = new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["codigo"] = (Expression<Func<Rol, string>>)(rol => rol.Codigo),
            ["nombre"] = (Expression<Func<Rol, string>>)(rol => rol.Nombre),
        },
        PorOmision = "codigo",
        Desempate = ordenada => ordenada.ThenBy(rol => rol.Id),
        Filtro = texto =>
        {
            string patron = Filtros.Contiene(texto);

            return rol => EF.Functions.ILike(rol.Codigo, patron, Filtros.Escape)
                || EF.Functions.ILike(rol.Nombre, patron, Filtros.Escape);
        },
    };

    public IReadOnlySet<string> CamposOrdenables => s_criterios.CamposOrdenables;

    public Task<PaginaDe<Rol>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion) =>
        ConPermisos.PaginarAsync(paginacion, s_criterios, cancelacion);

    // La unión de los permisos de varios roles, resuelta en la base y no en memoria: es la
    // consulta del camino del login, y traerse los roles enteros para unirlos aquí sería traerse
    // filas que nadie va a mirar. `Distinct` porque dos roles conceden a menudo lo mismo, y un
    // `claim` repetido engorda el token sin añadir nada.
    public async Task<IReadOnlyList<string>> PermisosDeAsync(
        IReadOnlyCollection<Guid> rolIds,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(rolIds);

        if (rolIds.Count == 0)
        {
            return [];
        }

        return await contexto.Set<PermisoDeRol>()
            .Where(permiso => rolIds.Contains(permiso.RolId))
            .Select(permiso => permiso.Permiso)
            .Distinct()
            .OrderBy(permiso => permiso)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Rol>> PorIdsAsync(
        IReadOnlyCollection<Guid> rolIds,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(rolIds);

        if (rolIds.Count == 0)
        {
            return [];
        }

        return await ConPermisos
            .Where(rol => rolIds.Contains(rol.Id))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
    }

    public void Agregar(Rol rol) => contexto.Roles.Add(rol);
}
