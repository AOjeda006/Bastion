using Bastion.BuildingBlocks.Application.Bloqueos;
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
    /// <remarks>
    /// <b>No lleva versión, y desde el 0.10 no puede llevarla.</b> El <c>If-Match</c> se cita
    /// leyendo antes el recurso, y un recurso bloqueado no se puede leer por ningún camino
    /// ordinario: la precondición pediría una llave que no existe (ADR-0017).
    /// </remarks>
    /// <param name="id">Identificador del almacén.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <inheritdoc cref="IDesbloquearAlmacen"/>
internal sealed class DesbloquearAlmacen(
    IRepositorioDeAlmacenes almacenes,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IAccesoALoBloqueado bloqueados) : IDesbloquearAlmacen
{
    public async Task<Resultado> EjecutarAsync(Guid id, CancellationToken cancelacion)
    {
        // El ÚNICO camino ordinario que necesita ver lo bloqueado, y por una razón de lógica:
        // para levantar un bloqueo hay que poder leer lo que está bloqueado. Es una apertura
        // declarada, con su motivo de la lista cerrada y anotada en el registro — no un
        // `IgnoreQueryFilters`, que además apagaría de paso el filtro de empresa.
        using IDisposable _ = bloqueados.ViendoLoBloqueado(MotivoParaVerLoBloqueado.AdministracionDelBloqueo);

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
