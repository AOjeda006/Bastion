using Bastion.Identidad.Contracts.Comun;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Identidad.Infrastructure.Persistencia.Repositorios;

/// <summary>
/// Trae una página de una consulta, con el total.
/// </summary>
/// <remarks>
/// En un solo sitio porque los repositorios del módulo paginan igual, y porque las dos cosas
/// que hay que acertar aquí se aciertan una vez: contar ANTES de paginar (contar después contaría
/// la página) y no traer nada rastreado, que en una consulta de lectura solo sirve para llenar el
/// rastreador de entidades que nadie va a modificar.
/// </remarks>
internal static class Paginador
{
    internal static async Task<PaginaDe<T>> PaginarAsync<T>(
        this IQueryable<T> consulta,
        Paginacion paginacion,
        CancellationToken cancelacion)
        where T : class
    {
        long total = await consulta.LongCountAsync(cancelacion).ConfigureAwait(false);

        List<T> elementos = await consulta
            .Skip(paginacion.Salto)
            .Take(paginacion.Tamanio)
            .AsNoTracking()
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<T>(elementos, paginacion.Pagina, paginacion.Tamanio, total);
    }
}
