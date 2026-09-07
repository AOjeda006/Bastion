using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Microsoft.EntityFrameworkCore;

namespace Bastion.BuildingBlocks.Infrastructure.Bloqueos;

/// <summary>
/// El filtro, el orden, el corte y el recuento de lo bloqueado, escritos una vez.
/// </summary>
/// <remarks>
/// <para>
/// Cada módulo que bloquea aporta lo único que es suyo —la proyección de SUS tablas a
/// <see cref="RecursoBloqueado"/>— y todo lo demás pasa por aquí. Repartido, tres implementaciones
/// del mismo puerto divergirían el día que una se equivocara en el <c>ILIKE</c> o se olvidara la
/// intercalación, y el listado saldría verde con una página mal ordenada.
/// </para>
/// <para>
/// <b>La intercalación <c>C</c> no es un adorno.</b> El orden final lo decide un comparador en
/// memoria —<c>ComposicionDeLoBloqueado</c>— y aquí se decide qué filas son candidatas. Si la base
/// ordenara por su intercalación por omisión y la memoria por orden de bytes, una fila del borde
/// podría quedarse fuera del candidato aunque el orden global la incluyera: la página saldría
/// completa, plausible y con una fila cambiada. <c>C</c> es orden de bytes, que es exactamente lo
/// que hace <c>string.CompareOrdinal</c> al otro lado.
/// </para>
/// </remarks>
public static class ConsultasDeLoBloqueado
{
    /// <summary>
    /// Contesta lo que el puerto promete: las primeras filas según el criterio, y el total.
    /// </summary>
    /// <param name="filas">Lo bloqueado de un módulo, ya proyectado.</param>
    /// <param name="criterio">Filtro, orden y cuántas filas como mucho.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public static async Task<LoBloqueadoDeUnModulo> ResponderAsync(
        IQueryable<RecursoBloqueado> filas,
        CriterioDeLoBloqueado criterio,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(filas);
        ArgumentNullException.ThrowIfNull(criterio);

        IQueryable<RecursoBloqueado> filtradas = Filtrar(filas, criterio.Filtro);

        // El total va con el filtro puesto y SIN el corte: es lo que se suma para el total de la
        // página. Contar después de `Take` daría como mucho `Cuantos`, que es un total falso.
        long total = await filtradas.LongCountAsync(cancelacion).ConfigureAwait(false);

        // Por `Consulta` y no por `Ordenar(filtradas, ...)` a mano: si fueran dos expresiones
        // distintas, la que ejerce el barrido de traducción y la que se ejecuta de verdad podrían
        // separarse sin que nada avisara, y el barrido pasaría a certificar una consulta que no es
        // esta. Vuelve a filtrar sobre `filas`, que es el mismo árbol y el mismo SQL.
        List<RecursoBloqueado> primeros = await Consulta(filas, criterio)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return new LoBloqueadoDeUnModulo(primeros, total);
    }

    /// <summary>
    /// La consulta de las primeras filas, sin ejecutarla.
    /// </summary>
    /// <remarks>
    /// <b>Existe para que se pueda comprobar que se traduce, y no es un rodeo.</b>
    /// <see cref="ResponderAsync"/> cuenta ANTES de traer, así que contra una conexión muerta lo
    /// que revienta es el <c>COUNT</c> y el <c>ORDER BY</c> no llega a traducirse nunca: un
    /// <c>?sort=</c> intraducible —el <c>Convert</c> que mete una lambda mal declarada, un
    /// <c>Collate</c> sobre algo que no es texto— saldría verde y aparecería como un 500 en la
    /// pantalla del art. 32. Con la consulta en la mano, <c>ToQueryString()</c> la traduce entera
    /// sin abrir conexión, que es lo que permite ejercerla con Docker parado.
    /// </remarks>
    /// <param name="filas">Lo bloqueado de un módulo, ya proyectado.</param>
    /// <param name="criterio">Filtro, orden y cuántas filas como mucho.</param>
    public static IQueryable<RecursoBloqueado> Consulta(
        IQueryable<RecursoBloqueado> filas, CriterioDeLoBloqueado criterio)
    {
        ArgumentNullException.ThrowIfNull(filas);
        ArgumentNullException.ThrowIfNull(criterio);

        return Ordenar(Filtrar(filas, criterio.Filtro), criterio).Take(criterio.Cuantos);
    }

    /// <summary>
    /// El <c>?q=</c> sobre código y nombre.
    /// </summary>
    /// <remarks>
    /// Los dos campos por los que una persona reconoce una ficha, y ninguno de los dos está en la
    /// lista de lo que no puede viajar en una URL (ADR-0025). El NIF de una empresa o de un tercero
    /// bloqueados NO se busca desde aquí: para eso está la búsqueda por cuerpo de su módulo.
    /// </remarks>
    private static IQueryable<RecursoBloqueado> Filtrar(
        IQueryable<RecursoBloqueado> filas, string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return filas;
        }

        string patron = Filtros.Contiene(texto);

        return filas.Where(fila =>
            EF.Functions.ILike(fila.Codigo!, patron, Filtros.Escape)
            || EF.Functions.ILike(fila.Nombre, patron, Filtros.Escape));
    }

    private static IOrderedQueryable<RecursoBloqueado> Ordenar(
        IQueryable<RecursoBloqueado> filas, CriterioDeLoBloqueado criterio)
    {
        // El desempate por identificador va SIEMPRE y en el mismo sentido que al componer: sin él,
        // dos filas con la misma fecha al milisegundo pueden salir en distinto orden en dos
        // consultas y la página 2 repetiría una fila de la 1.
        return criterio switch
        {
            { Campo: "tipo", Descendente: true } =>
                filas.OrderByDescending(f => f.Tipo).ThenBy(f => f.Id),
            { Campo: "tipo" } =>
                filas.OrderBy(f => f.Tipo).ThenBy(f => f.Id),

            { Campo: "codigo", Descendente: true } =>
                filas.OrderByDescending(f => EF.Functions.Collate(f.Codigo, "C")).ThenBy(f => f.Id),
            { Campo: "codigo" } =>
                filas.OrderBy(f => EF.Functions.Collate(f.Codigo, "C")).ThenBy(f => f.Id),

            { Campo: "nombre", Descendente: true } =>
                filas.OrderByDescending(f => EF.Functions.Collate(f.Nombre, "C")).ThenBy(f => f.Id),
            { Campo: "nombre" } =>
                filas.OrderBy(f => EF.Functions.Collate(f.Nombre, "C")).ThenBy(f => f.Id),

            { Descendente: true } =>
                filas.OrderByDescending(f => f.BloqueadoEn).ThenBy(f => f.Id),
            _ =>
                filas.OrderBy(f => f.BloqueadoEn).ThenBy(f => f.Id),
        };
    }
}
