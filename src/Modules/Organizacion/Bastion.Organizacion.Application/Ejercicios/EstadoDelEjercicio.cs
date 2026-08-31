using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Domain.Ejercicios;

namespace Bastion.Organizacion.Application.Ejercicios;

/// <summary>
/// Cierra un ejercicio contable (R9).
/// </summary>
/// <remarks>
/// Cerrar impide imputar operaciones al intervalo y bloquea el cambio de fechas. Es la operación
/// que convierte un periodo en definitivo, así que lleva su propio permiso: quien lleva el día a
/// día crea y modifica ejercicios; cerrar el año lo decide quien responde de las cuentas.
/// </remarks>
public interface ICerrarEjercicio
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del ejercicio.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, VersionDeRecurso version, CancellationToken cancelacion);
}

/// <summary>
/// Reabre un ejercicio cerrado (R9).
/// </summary>
/// <remarks>
/// <b>El permiso más restrictivo de los cuatro de este recurso</b>, y separado del de cerrar a
/// propósito: reabrir vuelve a admitir apuntes en un periodo que ya se dio por cerrado y del que,
/// probablemente, ya se presentaron modelos. Que sea posible es necesario —una subsanación existe—;
/// que lo pueda hacer cualquiera que sepa cerrar, no.
/// </remarks>
public interface IReabrirEjercicio
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del ejercicio.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, VersionDeRecurso version, CancellationToken cancelacion);
}

/// <inheritdoc cref="ICerrarEjercicio"/>
internal sealed class CerrarEjercicio(
    IRepositorioDeEjercicios ejercicios,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones) : ICerrarEjercicio
{
    public async Task<Resultado> EjecutarAsync(Guid id, VersionDeRecurso version, CancellationToken cancelacion)
    {
        Ejercicio? ejercicio = await ejercicios.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (ejercicio is null)
        {
            return Resultado.Fallo(ErroresDeEjercicio.NoEncontrado(id));
        }

        versiones.Exigir(ejercicio, version);

        ejercicio.Cerrar();
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}

/// <inheritdoc cref="IReabrirEjercicio"/>
internal sealed class ReabrirEjercicio(
    IRepositorioDeEjercicios ejercicios,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones) : IReabrirEjercicio
{
    public async Task<Resultado> EjecutarAsync(Guid id, VersionDeRecurso version, CancellationToken cancelacion)
    {
        Ejercicio? ejercicio = await ejercicios.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (ejercicio is null)
        {
            return Resultado.Fallo(ErroresDeEjercicio.NoEncontrado(id));
        }

        versiones.Exigir(ejercicio, version);

        ejercicio.Reabrir();
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}
