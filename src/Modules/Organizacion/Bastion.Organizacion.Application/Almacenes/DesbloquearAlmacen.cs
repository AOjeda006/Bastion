using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Domain.Almacenes;

namespace Bastion.Organizacion.Application.Almacenes;

/// <summary>
/// Levanta el bloqueo de un almacén, devolviéndolo a la operativa.
/// </summary>
/// <remarks>
/// Permiso propio, distinto del de bloquear, por lo mismo que en <c>Empresa</c>: quien puede
/// retirar un almacén de la operativa no tiene por qué poder devolver a ella uno que se retiró.
/// </remarks>
public interface IDesbloquearAlmacen
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del almacén.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <inheritdoc cref="IDesbloquearAlmacen"/>
internal sealed class DesbloquearAlmacen(
    IRepositorioDeAlmacenes almacenes,
    IUnidadTrabajoDeOrganizacion unidadTrabajo) : IDesbloquearAlmacen
{
    public async Task<Resultado> EjecutarAsync(Guid id, CancellationToken cancelacion)
    {
        Almacen? almacen = await almacenes.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (almacen is null)
        {
            return Resultado.Fallo(ErroresDeAlmacen.NoEncontrado(id));
        }

        almacen.Desbloquear();
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}
