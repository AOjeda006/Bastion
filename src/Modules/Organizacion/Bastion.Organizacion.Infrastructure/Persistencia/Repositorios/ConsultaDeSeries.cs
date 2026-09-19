using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Domain.Series;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IConsultaDeSeries"/>
/// <remarks>
/// Vive en Organización, que es la dueña de la tabla, y se resuelve en proceso: el módulo que
/// pregunta llama a un método (§4, reglas de frontera 1 y 3).
/// </remarks>
internal sealed class ConsultaDeSeries(OrganizacionDbContext contexto) : IConsultaDeSeries
{
    /// <summary>Las tres respuestas, sacadas de una sola lectura.</summary>
    /// <remarks>
    /// <para>
    /// <b>Una sola consulta y no dos</b>, por lo mismo que en las divisas y en las unidades: dos
    /// lecturas dejan un hueco en el que la fila cambia, y la respuesta describiría un estado que
    /// no fue verdad en ningún instante. El nulo del anulable <b>es</b> la respuesta a «¿existe?».
    /// </para>
    /// <para>
    /// <b>No se toca el contador</b>, ni siquiera para leerlo. Esta consulta corre fuera de la
    /// transacción que numera —es una pregunta del alta, no de la confirmación— y traer el último
    /// número aquí sería publicar un dato que caduca en cuanto se contesta.
    /// </para>
    /// </remarks>
    /// <param name="serieId">Identificador de la serie.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public async Task<EstadoDeMaestro> EstadoDeAsync(Guid serieId, CancellationToken cancelacion)
    {
        // El filtro de la R8 lo pone el contexto, así que una serie de otra empresa no llega a
        // esta proyección y cae en el `null` de abajo: la misma respuesta que una que no existe.
        EstadoDeSerie? estado = await contexto.Series
            .Where(serie => serie.Id == serieId)
            .Select(serie => (EstadoDeSerie?)serie.Estado)
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        return estado switch
        {
            null => EstadoDeMaestro.NoExiste,
            EstadoDeSerie.Activa => EstadoDeMaestro.SeOfreceParaLoNuevo,
            EstadoDeSerie.Cerrada => EstadoDeMaestro.SoloResuelveLoViejo,

            // No es defensivo por costumbre: un tercer estado de serie tiene que reventar en la
            // primera petición y no colarse por la rama permisiva, que es la que autoriza a
            // numerar (ADR-0004).
            _ => throw new ArgumentOutOfRangeException(
                nameof(serieId),
                estado,
                "La serie está en un estado que este puerto no sabe traducir a `EstadoDeMaestro`."),
        };
    }
}
