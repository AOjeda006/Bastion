using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Catalogo.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IConsultaDeTarifas"/>
/// <remarks>
/// Vive en Catálogo, que es el dueño de la tabla, y se resuelve en proceso: el módulo que pregunta
/// llama a un método (§4, reglas de frontera 1 y 3).
/// </remarks>
internal sealed class ConsultaDeTarifas(CatalogoDbContext contexto) : IConsultaDeTarifas
{
    /// <summary>Las cuatro respuestas, de una sola consulta.</summary>
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
    /// dos lecturas dejan un hueco en el que la fila cambia. La fila que falta <b>es</b> la
    /// respuesta a «¿existe?». <b>Y el tipo se mira antes que la vigencia</b>, porque es el que no
    /// tiene arreglo dentro del mismo código (<see cref="EstadoDeLaTarifa.EsDeOtroTipo"/>).
    /// </para>
    /// <para>
    /// <b>Y va por el filtro de inquilinato (R8).</b> Una tarifa de otra empresa contesta
    /// <see cref="EstadoDeLaTarifa.NoExiste"/>, que es lo que tiene que contestar: si contestara
    /// otra cosa, un tercero de una empresa podría quedar asignado a la lista de precios de otra.
    /// </para>
    /// </remarks>
    /// <param name="tarifaId">Identificador de la tarifa.</param>
    /// <param name="tipo">El tipo que tiene que tener.</param>
    /// <param name="enLaFecha">Día para el que se pregunta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public async Task<EstadoDeLaTarifa> EstadoDeAsync(
        Guid tarifaId, TipoDeLaTarifa tipo, DateOnly enLaFecha, CancellationToken cancelacion)
    {
        TipoDeTarifa buscado = tipo switch
        {
            TipoDeLaTarifa.Venta => TipoDeTarifa.Venta,
            TipoDeLaTarifa.Compra => TipoDeTarifa.Compra,
            _ => throw new ArgumentOutOfRangeException(
                nameof(tipo), tipo, "Una tarifa es de venta o de compra."),
        };

        var fila = await contexto.Tarifas
            .Where(tarifa => tarifa.Id == tarifaId)
            .Select(tarifa => new
            {
                tarifa.Tipo,
                Rige = tarifa.VigenteDesde <= enLaFecha
                    && (tarifa.VigenteHasta == null || enLaFecha <= tarifa.VigenteHasta),
            })
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        if (fila is null)
        {
            return EstadoDeLaTarifa.NoExiste;
        }

        if (fila.Tipo != buscado)
        {
            return EstadoDeLaTarifa.EsDeOtroTipo;
        }

        return fila.Rige ? EstadoDeLaTarifa.RigeEnEsaFecha : EstadoDeLaTarifa.SoloResuelveLoViejo;
    }
}
