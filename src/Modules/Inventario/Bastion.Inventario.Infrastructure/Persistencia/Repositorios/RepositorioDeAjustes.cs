using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.Inventario.Application.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Infrastructure.Persistencia.Existencias;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Inventario.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeAjustes"/>
/// <param name="contexto">El contexto del módulo.</param>
/// <param name="inquilino">De donde sale la empresa cuyas existencias se mueven.</param>
internal sealed class RepositorioDeAjustes(InventarioDbContext contexto, IInquilinoActual inquilino)
    : IRepositorioDeAjustes
{
    /// <inheritdoc/>
    /// <remarks>
    /// Las líneas vienen con el documento sin pedirlas: la navegación está marcada
    /// <c>AutoInclude</c> en su configuración, porque son su agregado y confirmarlo sin ellas
    /// escribiría un libro vacío.
    /// </remarks>
    public Task<Ajuste?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Ajustes.FirstOrDefaultAsync(ajuste => ajuste.Id == id, cancelacion);

    /// <inheritdoc/>
    public void Agregar(Ajuste ajuste) => contexto.Ajustes.Add(ajuste);

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// <b>Primero la existencia y después el libro, y el orden da igual</b>: los dos van en la
    /// misma transacción, así que nadie ve el uno sin el otro. La existencia se mueve ya, con una
    /// sentencia; las filas del libro se escriben cuando la unidad de trabajo guarda.
    /// </para>
    /// <para>
    /// <b>La empresa sale del inquilino y no de las filas</b>, y la sentencia comprueba que cada
    /// fila es de ella antes de mandar nada (ADR-0044). Un ámbito sin inquilino no puede anotar: la
    /// sentencia cruda no pasa por el filtro global y sumaría en la empresa de cualquiera.
    /// </para>
    /// </remarks>
    public async Task AnotarEnElLibroAsync(
        IReadOnlyCollection<MovimientoStock> movimientos, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(movimientos);

        Guid empresaId = inquilino.EmpresaDelFiltro ?? throw new InvalidOperationException(
            "Se está anotando el libro dentro de un ámbito sin inquilino, y una existencia es " +
            "siempre de una empresa: sin ella la sentencia sumaría en la de cualquiera.");

        await LaProyeccionDelLibro.MoverAsync(contexto, empresaId, movimientos, cancelacion)
            .ConfigureAwait(false);

        contexto.Movimientos.AddRange(movimientos);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// <b>Sin rastrear</b>: esto es la mitad de lectura de la R13 y nadie modifica lo que trae —no
    /// podría: la tabla no admite <c>UPDATE</c>—, así que meter el libro entero de un documento en
    /// el rastreador solo costaría memoria.
    /// </para>
    /// <para>
    /// <b>El orden es el del identificador</b>, y no es arbitrario: es un UUID v7, cuyo prefijo es
    /// el instante de creación, así que ordenar por él devuelve las filas en el orden en que se
    /// escribieron. No se ordena por <c>fecha_de_operacion</c> porque todas las de un mismo
    /// documento la comparten —es la del documento— y dejaría el desempate al azar del plan.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<MovimientoStock>> MovimientosDeAsync(
        TipoDeDocumentoOrigen tipo,
        Guid documentoId,
        CancellationToken cancelacion) =>
        await contexto.Movimientos
            .Where(movimiento => movimiento.DocumentoOrigenTipo == tipo
                && movimiento.DocumentoOrigenId == documentoId)
            .OrderBy(movimiento => movimiento.Id)
            .AsNoTracking()
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
}
