using Bastion.Terceros.Contracts.Terceros;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Terceros.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IConsultaDeTerceros"/>
/// <remarks>
/// Vive en Terceros, que es el dueño de la tabla, y se resuelve en proceso: el módulo que pregunta
/// llama a un método (§4, reglas de frontera 1 y 3).
/// </remarks>
internal sealed class ConsultaDeTerceros(TercerosDbContext contexto) : IConsultaDeTerceros
{
    /// <summary>Las tres respuestas, y la que no hay.</summary>
    /// <remarks>
    /// <para>
    /// <b>Una sola consulta y no tres</b>, por lo mismo que en las unidades y en las divisas: tres
    /// lecturas dejan huecos en los que la fila cambia, y la respuesta describiría un estado que no
    /// fue verdad en ningún instante. El nulo del anulable <b>es</b> la respuesta a «¿existe?».
    /// </para>
    /// <para>
    /// <b>Va por el filtro de inquilinato, y eso no es un detalle de eficiencia.</b> Sin él, un
    /// artículo de una empresa podría colgar de un tercero de otra: el identificador existiría, el
    /// puerto diría que sí, y la R8 quedaría rota por dentro sin que ninguna clave ajena chistara.
    /// Aquí el filtro <b>es</b> la comprobación, así que un <c>IgnoreQueryFilters</c> en esta
    /// consulta no sería una optimización: sería quitar la regla.
    /// </para>
    /// <para>
    /// <b>Y va por el filtro del bloqueo, que por lo mismo tampoco se esquiva.</b> Una ficha
    /// bloqueada no llega a la proyección, así que de ella sale <see cref="EstadoDelTercero.NoExiste"/>
    /// y nada más: ni que existe, ni qué papeles hace. Hasta que la matriz de puerto × estado lo
    /// destapó, aquí había una rama <c>{ EstaBloqueado: true } =&gt; Bloqueado</c> que nunca se
    /// ejecutaba —la fila que habría tenido que ver no pasaba el filtro— y un comentario que
    /// explicaba el orden de una comprobación que no ocurría.
    /// </para>
    /// </remarks>
    /// <param name="terceroId">Identificador del tercero.</param>
    /// <param name="rol">Papel por el que se pregunta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public async Task<EstadoDelTercero> EstadoDeAsync(
        Guid terceroId, RolDeTercero rol, CancellationToken cancelacion)
    {
        bool? haceElRol = await contexto.Terceros
            .Where(tercero => tercero.Id == terceroId)
            .Select(tercero => (bool?)(rol == RolDeTercero.Proveedor
                ? tercero.EsProveedor
                : tercero.EsCliente))
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        return haceElRol switch
        {
            null => EstadoDelTercero.NoExiste,
            false => EstadoDelTercero.NoHaceEseRol,
            true => EstadoDelTercero.Disponible,
        };
    }

    /// <summary>Los que existen en esta empresa y no están bloqueados.</summary>
    /// <remarks>
    /// Una consulta para todo el conjunto, y no una por fila: lo que la pide es un listado, y una
    /// pregunta por fila convertiría una página de veinte en veintiuna idas a la base. El
    /// <c>Distinct</c> es porque el conjunto de entrada puede traer repetidos —dos artículos del
    /// mismo proveedor— y lo que se devuelve es un conjunto.
    /// <para>
    /// <b>No lleva un <c>!EstaBloqueado</c> en el <c>WHERE</c>, y lo llevó.</b> Lo que deja fuera a
    /// los bloqueados es el filtro de repositorio, igual que lo que deja fuera a los de otra
    /// empresa: la condición escrita a mano repetía el filtro, y una condición que ninguna mutación
    /// puede poner roja —quitarla deja el resultado igual— no protege nada y hace creer que sí.
    /// Quien esquive el filtro aquí lo ve en <c>ElPuertoDeTercerosContraLaBaseTests</c>, y por el
    /// efecto en <c>ContratoDeLosCrucesTests</c>, que es donde el listado de un artículo tiene que
    /// dejar de enseñar al proveedor bloqueado.
    /// </para>
    /// </remarks>
    /// <param name="terceroIds">Los identificadores por los que se pregunta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public async Task<IReadOnlySet<Guid>> CualesSePuedenTratarAsync(
        IReadOnlyCollection<Guid> terceroIds, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(terceroIds);

        if (terceroIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        Guid[] preguntados = [.. terceroIds.Distinct()];

        List<Guid> tratables = await contexto.Terceros
            .Where(tercero => preguntados.Contains(tercero.Id))
            .Select(tercero => tercero.Id)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return tratables.ToHashSet();
    }
}
