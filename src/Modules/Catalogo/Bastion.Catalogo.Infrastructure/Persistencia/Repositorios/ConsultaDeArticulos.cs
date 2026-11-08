using Bastion.BuildingBlocks.Contracts.Maestros;
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
    /// <b>Una sola consulta</b>, con la fila que falta contestando «¿existe?», el tipo contestando
    /// «¿se almacena?» y la marca contestando «¿se ofrece para lo nuevo?». Son las tres preguntas
    /// del puerto y salen de la misma lectura.
    /// </para>
    /// <para>
    /// <b>«¿Se almacena?» va antes que «¿está retirado?»</b>, y el orden es la decisión: un servicio
    /// retirado contesta <c>NoSeAlmacena</c>, que ya no autoriza nada (ADR-0082).
    /// </para>
    /// </remarks>
    /// <param name="articuloId">Identificador del artículo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public async Task<AptitudParaMoverExistencias> AptitudDeAsync(
        Guid articuloId, CancellationToken cancelacion)
    {
        var fila = await contexto.Articulos
            .Where(articulo => articulo.Id == articuloId)
            .Select(articulo => new { articulo.Tipo, articulo.EstaRetirada })
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        return fila switch
        {
            null => AptitudParaMoverExistencias.NoExiste,
            { Tipo: TipoDeArticulo.Servicio } => AptitudParaMoverExistencias.NoSeAlmacena,
            { EstaRetirada: true } => AptitudParaMoverExistencias.SoloResuelveLoViejo,
            _ => AptitudParaMoverExistencias.SeOfreceParaLoNuevo,
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// El filtro de empresa es el que deja fuera el artículo de otra empresa, como en
    /// <see cref="AptitudDeAsync"/>. Sin cerrojo: la unidad base de un artículo no cambia. Y sin
    /// mirar la retirada: el recuento la pide para cada artículo con físico, y un retirado con
    /// existencias es justo el que hay que contar para sacarlas (ADR-0082).
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

    /// <inheritdoc/>
    /// <remarks>
    /// El filtro de empresa es el único, como en <see cref="UnidadesBaseDeAsync"/>: el artículo
    /// no es bloqueable, y el de otra empresa no vuelve. Un retirado sí, porque lo que ya lo
    /// nombra se sigue leyendo (ADR-0082).
    /// </remarks>
    /// <param name="articulos">Los artículos por los que se pregunta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public async Task<IReadOnlyDictionary<Guid, NombreDeMaestro>> NombresDeAsync(
        IReadOnlyCollection<Guid> articulos, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(articulos);

        return await contexto.Articulos
            .AsNoTracking()
            .Where(articulo => articulos.Contains(articulo.Id))
            .Select(articulo => new { articulo.Id, articulo.Codigo, Nombre = articulo.Descripcion })
            .ToDictionaryAsync(
                fila => fila.Id, fila => new NombreDeMaestro(fila.Codigo, fila.Nombre), cancelacion)
            .ConfigureAwait(false);
    }
}
