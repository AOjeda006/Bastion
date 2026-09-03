using System.Linq.Expressions;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Listados;
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

    // Por omisión, agrupadas por almacén: una ubicación se lee dentro de su almacén, y una lista
    // que los entremezcla obliga a leerla entera para saber qué hay en uno.
    private static readonly CriteriosDe<Ubicacion> s_criterios = new()
    {
        Ordenables = new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["almacen"] = (Expression<Func<Ubicacion, Guid>>)(ubicacion => ubicacion.AlmacenId),
            ["codigo"] = (Expression<Func<Ubicacion, string>>)(ubicacion => ubicacion.Codigo),
        },
        PorOmision = "almacen",
        Desempate = ordenada => ordenada
            .ThenBy(ubicacion => ubicacion.Codigo)
            .ThenBy(ubicacion => ubicacion.Id),
        Filtro = texto =>
        {
            string patron = Filtros.Contiene(texto);

            return ubicacion => EF.Functions.ILike(ubicacion.Codigo, patron, Filtros.Escape);
        },
    };

    public IReadOnlySet<string> CamposOrdenables => s_criterios.CamposOrdenables;

    public Task<PaginaDe<Ubicacion>> ListarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion) =>
        contexto.Ubicaciones.PaginarAsync(paginacion, s_criterios, cancelacion);

    public void Agregar(Ubicacion ubicacion) => contexto.Ubicaciones.Add(ubicacion);
}
