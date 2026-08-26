using Bastion.BuildingBlocks.Application;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Domain.Ejercicios;

namespace Bastion.Organizacion.Application.Ejercicios;

/// <summary>
/// Borra un ejercicio que todavía no tiene nada colgando.
/// </summary>
/// <remarks>
/// Aquí sí se borra de verdad, y la diferencia con <c>Empresa</c> tiene motivo: un ejercicio no
/// contiene datos personales —es un intervalo de fechas—, así que el art. 32 de la LOPDGDD no le
/// alcanza. Lo que hay que proteger es lo que cuelga de él; por eso se comprueba antes y, en
/// cuanto tiene series, deja de poderse borrar.
/// </remarks>
public interface IEliminarEjercicio
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del ejercicio.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <inheritdoc cref="IEliminarEjercicio"/>
internal sealed class EliminarEjercicio(
    IRepositorioDeEjercicios ejercicios,
    IUnidadTrabajo unidadTrabajo) : IEliminarEjercicio
{
    public async Task<Resultado> EjecutarAsync(Guid id, CancellationToken cancelacion)
    {
        Ejercicio? ejercicio = await ejercicios.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (ejercicio is null)
        {
            return Resultado.Fallo(ErroresDeEjercicio.NoEncontrado(id));
        }

        // La clave ajena es `Restrict`, así que la base también lo impediría. Pero lo haría con
        // una excepción de integridad referencial que sale como 500, y esto no es un fallo del
        // programa: es que el ejercicio tiene series, que es exactamente un 409.
        if (await ejercicios.TieneSeriesAsync(id, cancelacion).ConfigureAwait(false))
        {
            return Resultado.Fallo(ErrorDeOperacion.Conflicto(
                "ejercicio-con-series",
                "El ejercicio tiene series de numeración. Elimínelas antes, si es que ninguna ha " +
                "numerado todavía."));
        }

        ejercicios.Eliminar(ejercicio);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}
