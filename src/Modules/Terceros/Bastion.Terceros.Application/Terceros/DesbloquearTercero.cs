using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Terceros.Domain.Terceros;

namespace Bastion.Terceros.Application.Terceros;

/// <summary>
/// Levanta el bloqueo de un tercero, devolviéndolo a la operativa.
/// </summary>
/// <remarks>
/// Permiso propio, distinto del de bloquear, y aquí con más motivo que en ningún otro recurso:
/// devolver al tratamiento unos datos que el art. 32 había sacado de él no es lo contrario de una
/// baja administrativa, es una decisión que hay que poder auditar.
/// </remarks>
public interface IDesbloquearTercero
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <remarks>
    /// <b>No lleva versión, y no puede llevarla</b> (ADR-0017). El <c>If-Match</c> se cita leyendo
    /// antes el recurso, y un recurso bloqueado no se puede leer por ningún camino ordinario: la
    /// precondición pediría una llave que no existe.
    /// </remarks>
    /// <param name="id">Identificador del tercero.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <inheritdoc cref="IDesbloquearTercero"/>
internal sealed class DesbloquearTercero(
    IRepositorioDeTerceros terceros,
    IUnidadTrabajoDeTerceros unidadTrabajo,
    IAccesoALoBloqueado bloqueados) : IDesbloquearTercero
{
    public async Task<Resultado> EjecutarAsync(Guid id, CancellationToken cancelacion)
    {
        // Apertura declarada, con su motivo de la lista cerrada y anotada en el registro: para
        // levantar un bloqueo hay que poder leer lo que está bloqueado.
        using IDisposable _ = bloqueados.ViendoLoBloqueado(
            MotivoParaVerLoBloqueado.AdministracionDelBloqueo);

        Tercero? tercero = await terceros.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (tercero is null)
        {
            return Resultado.Fallo(ErroresDeTercero.NoEncontrado(id));
        }

        // No hay colisión que resolver al desbloquear, y no por suerte: la unicidad de (empresa,
        // identificador) abarca también lo bloqueado, así que mientras esta ficha estuvo bloqueada
        // nadie pudo dar de alta otra con su identificador. Es la otra mitad de la decisión que
        // hace que el alta contra un bloqueado dé conflicto — con una unicidad parcial, el
        // conflicto no existiría y esta línea tendría que ser código.
        tercero.Desbloquear();
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}
