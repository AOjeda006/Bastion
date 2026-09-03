using System.Linq.Expressions;
using System.Reflection;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Microsoft.EntityFrameworkCore;

namespace Bastion.BuildingBlocks.Infrastructure.Listados;

/// <summary>
/// Trae una página de una consulta: filtra, ordena y cuenta.
/// </summary>
/// <remarks>
/// <para>
/// En un solo sitio porque todos los repositorios de todos los módulos paginan igual, y porque
/// las cosas que hay que acertar aquí se aciertan una vez: contar ANTES de paginar (contar después
/// contaría la página), no traer nada rastreado (en una consulta de lectura solo llena el
/// rastreador de entidades que nadie va a modificar), y no dejar nunca una consulta sin orden ni
/// desempate.
/// </para>
/// <para>
/// Estaba duplicado, letra por letra, en Identidad y en Organización. Vive en el bloque común
/// desde el ítem 1.3.
/// </para>
/// </remarks>
public static class Paginador
{
    // El `OrderBy` de `Queryable`, sin cerrar. Hay dos sobrecargas y se distinguen por el número
    // de parámetros: la de dos es la que no lleva comparador.
    private static readonly MethodInfo s_ascendente = MetodoDeOrden(nameof(Queryable.OrderBy));
    private static readonly MethodInfo s_descendente = MetodoDeOrden(nameof(Queryable.OrderByDescending));

    /// <summary>
    /// Aplica el filtro y el orden que pide el cliente, y devuelve la página con su total.
    /// </summary>
    /// <remarks>
    /// El orden se aplica SIEMPRE, aunque el cliente no pida ninguno: sin <c>ORDER BY</c>,
    /// PostgreSQL no promete el mismo orden entre dos consultas, y entonces la página 2 puede
    /// repetir o saltarse filas de la 1 sin que nadie haya tocado nada.
    /// </remarks>
    /// <typeparam name="T">La entidad que se lista.</typeparam>
    /// <param name="consulta">La consulta de partida, ya acotada por lo que toque.</param>
    /// <param name="paginacion">Qué página, de qué tamaño, con qué orden y con qué filtro.</param>
    /// <param name="criterios">Por qué campos deja ordenar y filtrar este recurso.</param>
    /// <param name="cancelacion">Testigo de cancelación.</param>
    public static async Task<PaginaDe<T>> PaginarAsync<T>(
        this IQueryable<T> consulta,
        Paginacion paginacion,
        CriteriosDe<T> criterios,
        CancellationToken cancelacion)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(consulta);
        ArgumentNullException.ThrowIfNull(paginacion);
        ArgumentNullException.ThrowIfNull(criterios);

        IQueryable<T> filtrada = Filtrar(consulta, paginacion.Filtro, criterios);

        long total = await filtrada.LongCountAsync(cancelacion).ConfigureAwait(false);

        List<T> elementos = await Ordenar(filtrada, paginacion.Orden, criterios)
            .Skip(paginacion.Salto)
            .Take(paginacion.Tamanio)
            .AsNoTracking()
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<T>(elementos, paginacion.Pagina, paginacion.Tamanio, total);
    }

    /// <summary>
    /// Ordena por el campo pedido —o por el de omisión— y desempata siempre.
    /// </summary>
    /// <remarks>
    /// Si el campo pedido no está en los criterios, LANZA. No es un desenlace de negocio: el
    /// borde ya ha rechazado con un <c>400</c> cualquier nombre que no esté en la lista, así que
    /// llegar aquí con uno significa que alguien construyó la paginación saltándose el borde
    /// (ADR-0004: las guardas de argumento lanzan).
    /// </remarks>
    /// <typeparam name="T">La entidad que se lista.</typeparam>
    /// <param name="consulta">La consulta a ordenar.</param>
    /// <param name="orden">El orden pedido, o nulo para el de omisión.</param>
    /// <param name="criterios">Por qué campos deja ordenar este recurso.</param>
    public static IOrderedQueryable<T> Ordenar<T>(
        this IQueryable<T> consulta,
        Orden? orden,
        CriteriosDe<T> criterios)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(consulta);
        ArgumentNullException.ThrowIfNull(criterios);

        string campo = orden?.Campo ?? criterios.PorOmision;

        if (!criterios.Ordenables.TryGetValue(campo, out LambdaExpression? clave))
        {
            throw new ArgumentException(
                $"El campo de orden {campo} no está entre los que admite este listado " +
                $"({string.Join(", ", criterios.Ordenables.Keys)}). El borde tenía que haberlo " +
                "rechazado con un 400 antes de llegar aquí.", nameof(orden));
        }

        bool descendente = orden?.Descendente ?? criterios.DescendentePorOmision;

        MethodInfo aplicar = (descendente ? s_descendente : s_ascendente)
            .MakeGenericMethod(typeof(T), clave.ReturnType);

        var ordenada = (IOrderedQueryable<T>)aplicar.Invoke(null, [consulta, clave])!;

        return criterios.Desempate(ordenada);
    }

    private static IQueryable<T> Filtrar<T>(IQueryable<T> consulta, string? filtro, CriteriosDe<T> criterios)
        where T : class
    {
        // Un `?q=` vacío o en blanco es «sin filtro», no «lo que contenga la cadena vacía»: lo
        // segundo sería un `ILIKE '%%'` que trae la tabla entera pasando por el índice.
        if (criterios.Filtro is null || string.IsNullOrWhiteSpace(filtro))
        {
            return consulta;
        }

        return consulta.Where(criterios.Filtro(filtro.Trim()));
    }

    private static MethodInfo MetodoDeOrden(string nombre) =>
        typeof(Queryable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(metodo => metodo.Name == nombre && metodo.GetParameters().Length == 2);
}
