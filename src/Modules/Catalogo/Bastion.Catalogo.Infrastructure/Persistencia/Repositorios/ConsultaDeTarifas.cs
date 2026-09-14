using Bastion.Catalogo.Contracts.Catalogo;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Catalogo.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IConsultaDeTarifas"/>
/// <remarks>
/// Vive en Catálogo, que es el dueño de la tabla, y se resuelve en proceso: el módulo que pregunta
/// llama a un método (§4, reglas de frontera 1 y 3).
/// </remarks>
internal sealed class ConsultaDeTarifas(CatalogoDbContext contexto) : IConsultaDeTarifas
{
    /// <summary>Las tres respuestas, de una sola consulta.</summary>
    /// <remarks>
    /// <para>
    /// <b>La vigencia se evalúa en SQL y con la misma convención que el dominio:</b> cerrada por
    /// los dos extremos, <c>desde &lt;= día</c> y <c>día &lt;= hasta</c> cuando hay hasta. Es lo
    /// mismo que dice <c>Tarifa.RigeEl</c> y lo mismo que dice el <c>daterange(..., '[]')</c> de la
    /// restricción de exclusión. Tres sitios, una convención: el día en que un tramo acaba, todavía
    /// cuenta.
    /// </para>
    /// <para>
    /// <b>Una sola consulta y no dos</b> —existir y regir—, por lo mismo que en los otros puertos:
    /// dos lecturas dejan un hueco en el que la fila cambia. El nulo del anulable <b>es</b> la
    /// respuesta a «¿existe?».
    /// </para>
    /// <para>
    /// <b>Y va por el filtro de inquilinato (R8).</b> Una tarifa de otra empresa contesta
    /// <see cref="EstadoDeLaTarifa.NoExiste"/>, que es lo que tiene que contestar: si contestara
    /// otra cosa, un tercero de una empresa podría quedar asignado a la lista de precios de otra.
    /// </para>
    /// </remarks>
    /// <param name="tarifaId">Identificador de la tarifa.</param>
    /// <param name="enLaFecha">Día para el que se pregunta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public async Task<EstadoDeLaTarifa> EstadoDeAsync(
        Guid tarifaId, DateOnly enLaFecha, CancellationToken cancelacion)
    {
        bool? rige = await contexto.Tarifas
            .Where(tarifa => tarifa.Id == tarifaId)
            .Select(tarifa => (bool?)(tarifa.VigenteDesde <= enLaFecha
                && (tarifa.VigenteHasta == null || enLaFecha <= tarifa.VigenteHasta)))
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        return rige switch
        {
            null => EstadoDeLaTarifa.NoExiste,
            true => EstadoDeLaTarifa.RigeEnEsaFecha,
            false => EstadoDeLaTarifa.SoloResuelveLoViejo,
        };
    }
}
