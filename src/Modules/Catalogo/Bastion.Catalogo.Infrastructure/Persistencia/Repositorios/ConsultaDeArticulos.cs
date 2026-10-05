using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Catalogo.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IConsultaDeArticulos"/>
/// <remarks>
/// Vive en Catálogo, que es el dueño de la tabla, y se resuelve en proceso: el módulo que pregunta
/// llama a un método (§4, reglas de frontera 1 y 3).
/// </remarks>
internal sealed class ConsultaDeArticulos(CatalogoDbContext contexto) : IConsultaDeArticulos
{
    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// <b>No abre ningún ámbito, y eso es la mitad de la respuesta.</b> El artículo no es
    /// bloqueable —no guarda ni un dato de una persona, y lo comprueba
    /// <c>ElCatalogoNoGuardaDatosDeNadieTests</c> recorriendo el modelo— así que aquí no hay filtro
    /// de R16 que esquivar. El único filtro que queda puesto es el de empresa, y por eso un
    /// artículo de otra empresa contesta <c>NoExiste</c>: desde fuera de Catálogo eso y no haberlo
    /// son la misma cosa.
    /// </para>
    /// <para>
    /// <b>Una sola consulta</b>, con el nulo del anulable contestando «¿existe?» y el valor
    /// contestando «¿se almacena?». Son las dos preguntas del puerto y salen de la misma lectura.
    /// </para>
    /// </remarks>
    /// <param name="articuloId">Identificador del artículo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public async Task<AptitudParaMoverExistencias> AptitudDeAsync(
        Guid articuloId, CancellationToken cancelacion)
    {
        TipoDeArticulo? tipo = await contexto.Articulos
            .Where(articulo => articulo.Id == articuloId)
            .Select(articulo => (TipoDeArticulo?)articulo.Tipo)
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        return tipo switch
        {
            null => AptitudParaMoverExistencias.NoExiste,
            TipoDeArticulo.Servicio => AptitudParaMoverExistencias.NoSeAlmacena,
            _ => AptitudParaMoverExistencias.SeOfreceParaLoNuevo,
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// El filtro de empresa es el que deja fuera el artículo de otra empresa, como en
    /// <see cref="AptitudDeAsync"/>. Sin cerrojo: la unidad base de un artículo no cambia.
    /// </remarks>
    /// <param name="articulos">Los artículos por los que se pregunta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public async Task<IReadOnlyDictionary<Guid, Guid>> UnidadesBaseDeAsync(
        IReadOnlyCollection<Guid> articulos, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(articulos);

        if (articulos.Count == 0)
        {
            return new Dictionary<Guid, Guid>();
        }

        return await contexto.Articulos
            .AsNoTracking()
            .Where(articulo => articulos.Contains(articulo.Id))
            .Select(articulo => new { articulo.Id, articulo.UnidadBaseId })
            .ToDictionaryAsync(fila => fila.Id, fila => fila.UnidadBaseId, cancelacion)
            .ConfigureAwait(false);
    }
}
