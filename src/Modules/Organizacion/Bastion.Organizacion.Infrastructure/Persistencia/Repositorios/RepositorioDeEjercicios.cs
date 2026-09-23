using System.Linq.Expressions;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Listados;
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

    public Task<bool> HaySolapeAsync(
        Guid empresaId,
        DateOnly inicio,
        DateOnly fin,
        Guid? excepto,
        CancellationToken cancelacion) =>
        contexto.Ejercicios.AnyAsync(
            ejercicio => ejercicio.EmpresaId == empresaId
                && (excepto == null || ejercicio.Id != excepto)

                // Dos intervalos CERRADOS se pisan cuando cada uno empieza antes de que acabe el
                // otro. Las dos comparaciones son `<=` porque los dos extremos están INCLUIDOS: con
                // `<`, dos ejercicios que compartieran un solo día pasarían por aquí y los pararía
                // la base con un 500 en vez de con el 409 que esto existe para poder dar. Es el
                // mismo `'[]'` que lleva el `daterange` de la restricción, y tienen que decir lo
                // mismo o la comprobación de aquí sobraría y estorbaría.
                //
                // Aquí no hay extremo nulo, a diferencia de un tramo de tarifa: un ejercicio
                // siempre tiene fin, y por eso no aparece ningún `DateOnly.MaxValue`.
                && ejercicio.FechaDeInicio <= fin
                && inicio <= ejercicio.FechaDeFin,
            cancelacion);

    public Task<bool> ExisteAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Ejercicios.AnyAsync(ejercicio => ejercicio.Id == id, cancelacion);

    public Task<bool> TieneSeriesAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Series.AnyAsync(serie => serie.EjercicioId == id, cancelacion);

    private static readonly CriteriosDe<Ejercicio> s_criterios = new()
    {
        Ordenables = new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["inicio"] = (Expression<Func<Ejercicio, DateOnly>>)(ejercicio => ejercicio.FechaDeInicio),
            ["anio"] = (Expression<Func<Ejercicio, int>>)(ejercicio => ejercicio.Anio),
        },
        PorOmision = "inicio",
        // Del más reciente al más antiguo: quien abre la pantalla de ejercicios busca el que está
        // usando, que es el último, y no el de hace ocho años.
        DescendentePorOmision = true,
        Desempate = ordenada => ordenada.ThenBy(ejercicio => ejercicio.Id),
    };

    public IReadOnlySet<string> CamposOrdenables => s_criterios.CamposOrdenables;

    public Task<PaginaDe<Ejercicio>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion) =>
        contexto.Ejercicios.PaginarAsync(paginacion, s_criterios, cancelacion);

    public void Agregar(Ejercicio ejercicio) => contexto.Ejercicios.Add(ejercicio);

    public void Eliminar(Ejercicio ejercicio) => contexto.Ejercicios.Remove(ejercicio);
}
