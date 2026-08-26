using Bastion.BuildingBlocks.Application;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Domain.Almacenes;

namespace Bastion.Organizacion.Application.Almacenes;

/// <summary>
/// Bloquea un almacén. Es lo que hace el <c>DELETE</c> del recurso.
/// </summary>
/// <remarks>
/// El motivo no es el mismo que en <c>Empresa</c> —un almacén no es una persona y el art. 32 de
/// la LOPDGDD no le alcanza— pero la forma sí, y a propósito: cada movimiento de existencias
/// apunta a su almacén para siempre. Borrar la fila rompería el histórico de valoración, que es
/// justo lo que no se puede reconstruir después.
/// </remarks>
public interface IBloquearAlmacen
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del almacén.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <inheritdoc cref="IBloquearAlmacen"/>
internal sealed class BloquearAlmacen(
    IRepositorioDeAlmacenes almacenes,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : IBloquearAlmacen
{
    public async Task<Resultado> EjecutarAsync(Guid id, CancellationToken cancelacion)
    {
        Almacen? almacen = await almacenes.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (almacen is null)
        {
            return Resultado.Fallo(ErroresDeAlmacen.NoEncontrado(id));
        }

        almacen.Bloquear(reloj.GetUtcNow());
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}
