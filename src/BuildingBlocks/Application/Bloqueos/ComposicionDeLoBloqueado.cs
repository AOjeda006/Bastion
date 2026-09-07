using Bastion.BuildingBlocks.Contracts.Paginacion;

namespace Bastion.BuildingBlocks.Application.Bloqueos;

/// <summary>
/// Junta en una sola página lo que cada módulo contesta por su puerto.
/// </summary>
/// <remarks>
/// <para>
/// <b>Esto existe porque el repositorio anterior tenía razón en el problema y no en la
/// conclusión.</b> Su comentario decía: «tres consultas paginadas por separado no se pueden juntar
/// en una página: no hay forma de saber cuántas filas traer de cada una sin traerlas todas». La
/// primera mitad es cierta —paginar cada fuente por separado y concatenar da una página
/// equivocada—; la segunda no. Para la página <i>k</i> de un orden global bastan las
/// <c>salto + tamaño</c> primeras filas de <b>cada</b> fuente: es la cota de una fusión k-vías, y
/// se demuestra sola — una fila que no está entre las <c>salto + tamaño</c> primeras de su propia
/// fuente tiene por delante, solo en su fuente, más filas que las que caben hasta el final de la
/// página, así que no puede aparecer en ella.
/// </para>
/// <para>
/// <b>Y no es asintóticamente peor que el <c>UNION</c> que sustituye.</b> Aquel también tenía que
/// recorrer <c>OFFSET + LIMIT</c> filas para llegar a la página <i>k</i>; la diferencia es un
/// factor N de módulos en el número de viajes, con <c>Tamanio</c> topado en
/// <see cref="Paginacion.TamanioMaximo"/>. A cambio, la consulta deja de cruzar esquemas, que es lo
/// que el monolito modular no puede hacer y lo que dejaba fuera del listado a los usuarios y a los
/// terceros bloqueados.
/// </para>
/// <para>
/// <b>El total es exacto, no estimado.</b> Cada módulo cuenta las suyas con el filtro puesto y los
/// totales se suman: es el mismo número que daba el <c>COUNT</c> sobre la unión.
/// </para>
/// <para>
/// <b>El orden de las cadenas es ordinal a los dos lados, y eso hay que decirlo.</b> El orden final
/// lo decide este comparador en memoria; cada módulo ordena en su base para saber cuáles son sus
/// primeras. Si las dos ordenaciones no coincidieran, una fila del borde podría quedarse fuera del
/// candidato aunque el orden global la incluyera. Por eso las consultas ordenan con la
/// intercalación <c>C</c> de PostgreSQL —orden de bytes— y aquí se usa
/// <see cref="StringComparer.Ordinal"/>: son la misma relación de orden. Sin fijarlo, el resultado
/// dependería de la intercalación de la base y de la cultura del proceso, que es el fallo que solo
/// aparece en la máquina que no debía.
/// </para>
/// </remarks>
public static class ComposicionDeLoBloqueado
{
    /// <summary>Por qué campos se puede ordenar el listado del art. 32.</summary>
    /// <remarks>
    /// Es una sola lista para todos los módulos, y eso es una mejora del traslado: antes la
    /// publicaba el repositorio de Organización, así que un módulo nuevo podía traer sus filas y no
    /// tener nada que decir sobre por dónde se ordenan.
    /// </remarks>
    public static IReadOnlySet<string> CamposOrdenables { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "tipo", "codigo", "nombre", "fecha" };

    /// <summary>El campo por el que se ordena si nadie dice otra cosa.</summary>
    /// <remarks>
    /// Por fecha y de la más reciente a la más antigua: quien abre esta pantalla suele venir de un
    /// bloqueo que acaba de hacerse por error, no de uno de hace seis años.
    /// </remarks>
    public const string CampoPorOmision = "fecha";

    /// <summary>Y en qué sentido.</summary>
    public const bool DescendentePorOmision = true;

    /// <summary>
    /// Pide a cada puerto sus primeras filas y compone la página pedida.
    /// </summary>
    /// <param name="puertos">Los módulos que bloquean. Uno por módulo, resueltos del contenedor.</param>
    /// <param name="paginacion">Qué página, de qué tamaño, con qué orden y con qué filtro.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public static async Task<PaginaDe<RecursoBloqueado>> ComponerAsync(
        IEnumerable<IConsultaDeLoBloqueado> puertos,
        Paginacion paginacion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(puertos);
        ArgumentNullException.ThrowIfNull(paginacion);

        string campo = paginacion.Orden?.Campo ?? CampoPorOmision;
        bool descendente = paginacion.Orden?.Descendente ?? DescendentePorOmision;

        // La cota de la fusión: lo que hace falta para poder construir ESTA página, ni una fila
        // más. `checked` porque `Salto + Tamanio` con una página absurdamente alta desbordaría en
        // silencio y pediría un número negativo de filas, que es un 500 raro en vez de una página
        // vacía.
        int cuantos = SumarConTope(paginacion.Salto, paginacion.Tamanio);

        CriterioDeLoBloqueado criterio = new(paginacion.Filtro, campo, descendente, cuantos);

        // Uno detrás de otro y NO con `Task.WhenAll`, a propósito. Cada puerto arrastra el contexto
        // de su módulo, y lanzar tres consultas de EF Core a la vez solo es seguro mientras cada
        // contexto tenga su propia conexión: el día que dos compartieran una —para poder abrir una
        // transacción común, que es una petición razonable— esto reventaría con «a second operation
        // was started on this context», y reventaría en el listado del art. 32, que es donde menos
        // falta hace. Son tres viajes en una pantalla de administración; el paralelismo ahorraría
        // milisegundos a cambio de un acoplamiento con algo que no se ve desde aquí.
        List<LoBloqueadoDeUnModulo> respuestas = [];

        foreach (IConsultaDeLoBloqueado puerto in puertos)
        {
            respuestas.Add(await puerto.PrimerosAsync(criterio, cancelacion).ConfigureAwait(false));
        }

        Comparer<RecursoBloqueado> comparador = Comparador(campo, descendente);

        IReadOnlyList<RecursoBloqueado> pagina =
        [
            .. respuestas
                .SelectMany(respuesta => respuesta.Primeros)
                .Order(comparador)
                .Skip(paginacion.Salto)
                .Take(paginacion.Tamanio),
        ];

        return new PaginaDe<RecursoBloqueado>(
            pagina, paginacion.Pagina, paginacion.Tamanio, respuestas.Sum(r => r.Total));
    }

    /// <summary>
    /// El orden, con su desempate.
    /// </summary>
    /// <remarks>
    /// <b>Desempata por identificador y no por tipo.</b> Aquí conviven cinco agregados de tres
    /// módulos, y dos filas de tablas distintas pueden compartir fecha al milisegundo si se
    /// bloquearon en la misma petición. Sin desempate único, la página 2 repetiría filas de la 1.
    /// </remarks>
    private static Comparer<RecursoBloqueado> Comparador(string campo, bool descendente)
    {
        Comparison<RecursoBloqueado> por = campo switch
        {
            "tipo" => (a, b) => a.Tipo.CompareTo(b.Tipo),
            "codigo" => (a, b) => string.CompareOrdinal(a.Codigo, b.Codigo),
            "nombre" => (a, b) => string.CompareOrdinal(a.Nombre, b.Nombre),
            _ => (a, b) => a.BloqueadoEn.CompareTo(b.BloqueadoEn),
        };

        return Comparer<RecursoBloqueado>.Create((a, b) =>
        {
            int principal = por(a, b);
            int orientado = descendente ? -principal : principal;

            // El desempate NO se invierte con el orden principal: solo tiene que ser estable y
            // total, y dar la vuelta lo dejaría igual de estable pero costaría explicarlo.
            return orientado != 0 ? orientado : a.Id.CompareTo(b.Id);
        });
    }

    /// <summary>
    /// <c>salto + tamaño</c> sin desbordar.
    /// </summary>
    /// <remarks>
    /// <c>Pagina</c> no tiene tope —solo lo tiene <c>Tamanio</c>—, así que una petición con
    /// <c>?page=20000000</c> desbordaría el entero y pediría un número negativo de filas. Topado,
    /// una página imposible devuelve vacío, que es lo que corresponde.
    /// </remarks>
    private static int SumarConTope(int salto, int tamanio)
    {
        long suma = (long)salto + tamanio;

        return suma > int.MaxValue ? int.MaxValue : (int)suma;
    }
}
