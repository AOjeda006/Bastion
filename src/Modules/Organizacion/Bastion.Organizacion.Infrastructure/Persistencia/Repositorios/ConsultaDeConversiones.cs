using Bastion.Organizacion.Contracts.Unidades;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IConsultaDeConversiones"/>
/// <remarks>
/// Vive en Organización, que es la dueña de la tabla, y se resuelve en proceso: el módulo que
/// pregunta llama a un método (§4, reglas de frontera 1 y 3).
/// </remarks>
internal sealed class ConsultaDeConversiones(OrganizacionDbContext contexto) : IConsultaDeConversiones
{
    /// <inheritdoc/>
    /// <remarks>
    /// <b>Lee el producto de los orígenes por los destinos y se queda con los pares pedidos.</b> Una
    /// lista de pares no se traduce a SQL como un <c>IN</c> de tuplas, y dos <c>IN</c>, uno por
    /// columna, traen como mucho unas pocas filas de más: las de un origen pedido hacia un destino
    /// pedido para otro origen. El filtro exacto va en memoria.
    /// </remarks>
    public async Task<IReadOnlyDictionary<ParDeUnidades, ConversionResuelta>> DeLosParesAsync(
        IReadOnlyCollection<ParDeUnidades> pares,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(pares);

        HashSet<ParDeUnidades> pedidos = [.. pares];

        if (pedidos.Count == 0)
        {
            return new Dictionary<ParDeUnidades, ConversionResuelta>();
        }

        Guid[] origenes = [.. pedidos.Select(par => par.OrigenId).Distinct()];
        Guid[] destinos = [.. pedidos.Select(par => par.DestinoId).Distinct()];

        var filas = await contexto.ConversionesDeUnidades
            .AsNoTracking()
            .Where(conversion => origenes.Contains(conversion.UnidadOrigenId)
                && destinos.Contains(conversion.UnidadDestinoId))
            .Select(conversion => new
            {
                conversion.UnidadOrigenId,
                conversion.UnidadDestinoId,
                conversion.Factor,
                conversion.EstaRetirada,
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return filas
            .Select(fila => (
                Par: new ParDeUnidades(fila.UnidadOrigenId, fila.UnidadDestinoId),
                Conversion: new ConversionResuelta(fila.Factor, fila.EstaRetirada)))
            .Where(fila => pedidos.Contains(fila.Par))
            .ToDictionary(fila => fila.Par, fila => fila.Conversion);
    }
}
