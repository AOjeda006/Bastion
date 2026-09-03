using System.Linq.Expressions;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Listados;
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

    private static readonly CriteriosDe<Almacen> s_criterios = new()
    {
        Ordenables = new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["codigo"] = (Expression<Func<Almacen, string>>)(almacen => almacen.Codigo),
            ["nombre"] = (Expression<Func<Almacen, string>>)(almacen => almacen.Nombre),
        },
        PorOmision = "codigo",
        Desempate = ordenada => ordenada.ThenBy(almacen => almacen.Id),
        Filtro = texto =>
        {
            string patron = Filtros.Contiene(texto);

            return almacen => EF.Functions.ILike(almacen.Codigo, patron, Filtros.Escape)
                || EF.Functions.ILike(almacen.Nombre, patron, Filtros.Escape);
        },
    };

    public IReadOnlySet<string> CamposOrdenables => s_criterios.CamposOrdenables;

    public Task<PaginaDe<Almacen>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion) =>
        contexto.Almacenes.PaginarAsync(paginacion, s_criterios, cancelacion);

    public void Agregar(Almacen almacen) => contexto.Almacenes.Add(almacen);
}
