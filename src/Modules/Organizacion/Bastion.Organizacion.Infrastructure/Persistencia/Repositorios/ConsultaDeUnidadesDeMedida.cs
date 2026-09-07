using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Unidades;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IConsultaDeUnidadesDeMedida"/>
/// <remarks>
/// Vive en Organización, que es la dueña de la tabla, y se resuelve en proceso: el módulo que
/// pregunta llama a un método (§4, reglas de frontera 1 y 3).
/// </remarks>
internal sealed class ConsultaDeUnidadesDeMedida(OrganizacionDbContext contexto)
    : IConsultaDeUnidadesDeMedida
{
    /// <summary>Las tres respuestas, desde el ítem 1.7.</summary>
    /// <remarks>
    /// <para>
    /// La tercera —<c>SoloResuelveLoViejo</c>— llegaba «con la retirada del ítem 1.7», y llegó. Lo
    /// que ese comentario no traía era nada que comprobara que llegaba: el enumerado tenía un valor
    /// que dos de sus tres productores no podían contestar, en verde y durante cuatro ítems. Eso lo
    /// vigila ahora <c>LaMatrizDePuertoYEstadoTests</c>, que compara la lista cerrada contra el
    /// conjunto de puertos que la producen y exige que cada casilla esté afirmada por un caso.
    /// </para>
    /// <para>
    /// <b>Una sola consulta y no dos</b>, por lo mismo que en las divisas: dos lecturas dejan un
    /// hueco en el que la fila cambia, y la respuesta describiría un estado que no fue verdad en
    /// ningún instante. El nulo del anulable <b>es</b> la respuesta a «¿existe?».
    /// </para>
    /// </remarks>
    /// <param name="unidadId">Identificador de la unidad de medida.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public async Task<EstadoDeMaestro> EstadoDeAsync(Guid unidadId, CancellationToken cancelacion)
    {
        bool? retirada = await contexto.UnidadesDeMedida
            .Where(unidad => unidad.Id == unidadId)
            .Select(unidad => (bool?)unidad.EstaRetirada)
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        return retirada switch
        {
            null => EstadoDeMaestro.NoExiste,
            true => EstadoDeMaestro.SoloResuelveLoViejo,
            false => EstadoDeMaestro.SeOfreceParaLoNuevo,
        };
    }
}
