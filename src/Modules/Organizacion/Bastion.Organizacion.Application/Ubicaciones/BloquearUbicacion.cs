using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Bloqueos;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Domain.Ubicaciones;

namespace Bastion.Organizacion.Application.Ubicaciones;

/// <summary>
/// Bloquea una ubicación. Es lo que hace el <c>DELETE</c> del recurso.
/// </summary>
/// <remarks>
/// Mismo mecanismo que el almacén y mismo motivo: cada movimiento de existencias apunta a la
/// ubicación de la que salió y a la que entró, para siempre. Borrar la fila dejaría ese histórico
/// señalando a una ubicación que no existe, y eso no se reconstruye después.
/// </remarks>
public interface IBloquearUbicacion
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la ubicación.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, VersionDeRecurso version, CancellationToken cancelacion);
}

/// <summary>
/// Levanta el bloqueo de una ubicación, devolviéndola a la operativa.
/// </summary>
/// <remarks>
/// Permiso propio, distinto del de bloquear, por lo mismo que en el almacén: quien puede retirar
/// una ubicación de la operativa no tiene por qué poder devolver a ella una que se retiró.
/// </remarks>
public interface IDesbloquearUbicacion
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <remarks>
    /// <b>No lleva versión, y no puede llevarla.</b> El <c>If-Match</c> se cita leyendo antes el
    /// recurso, y una ubicación bloqueada no se lee por ningún camino ordinario: la precondición
    /// pediría una llave que no se puede conseguir (ADR-0017).
    /// </remarks>
    /// <param name="id">Identificador de la ubicación.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <inheritdoc cref="IBloquearUbicacion"/>
internal sealed class BloquearUbicacion(
    IRepositorioDeUbicaciones ubicaciones,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones,
    TimeProvider reloj) : IBloquearUbicacion
{
    public async Task<Resultado> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        CancellationToken cancelacion)
    {
        Ubicacion? ubicacion = await ubicaciones.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (ubicacion is null)
        {
            return Resultado.Fallo(ErroresDeUbicacion.NoEncontrada(id));
        }

        versiones.Exigir(ubicacion, version);

        // Cese de uso, no art. 32: una ubicación no es una persona. Mismo mecanismo, motivo
        // distinto, y la columna lo distingue.
        ubicacion.Bloquear(MotivoDeBloqueo.CeseDeUso, reloj.GetUtcNow());
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}

/// <inheritdoc cref="IDesbloquearUbicacion"/>
internal sealed class DesbloquearUbicacion(
    IRepositorioDeUbicaciones ubicaciones,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IAccesoALoBloqueado bloqueados) : IDesbloquearUbicacion
{
    public async Task<Resultado> EjecutarAsync(Guid id, CancellationToken cancelacion)
    {
        // Para levantar un bloqueo hay que poder leer lo bloqueado. Apertura declarada, con su
        // motivo de la lista cerrada y anotada en el registro — no un `IgnoreQueryFilters`, que
        // además apagaría de paso el filtro de empresa.
        using IDisposable _ = bloqueados.ViendoLoBloqueado(
            MotivoParaVerLoBloqueado.AdministracionDelBloqueo);

        Ubicacion? ubicacion = await ubicaciones.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (ubicacion is null)
        {
            return Resultado.Fallo(ErroresDeUbicacion.NoEncontrada(id));
        }

        ubicacion.Desbloquear();
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}
