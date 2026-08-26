using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Contracts.Ejercicios;
using Bastion.Organizacion.Domain.Ejercicios;

namespace Bastion.Organizacion.Application.Ejercicios;

/// <summary>Cambia las fechas de un ejercicio abierto.</summary>
public interface IModificarEjercicio
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del ejercicio.</param>
    /// <param name="peticion">Las fechas nuevas.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<EjercicioDto>> EjecutarAsync(
        Guid id,
        ModificarEjercicioDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IModificarEjercicio"/>
internal sealed class ModificarEjercicio(
    IRepositorioDeEjercicios ejercicios,
    IUnidadTrabajoDeOrganizacion unidadTrabajo) : IModificarEjercicio
{
    public async Task<Resultado<EjercicioDto>> EjecutarAsync(
        Guid id,
        ModificarEjercicioDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Ejercicio? ejercicio = await ejercicios.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (ejercicio is null)
        {
            return Resultado.Fallo<EjercicioDto>(ErroresDeEjercicio.NoEncontrado(id));
        }

        if (ejercicio.Estado == EstadoDeEjercicio.Cerrado)
        {
            return Resultado.Fallo<EjercicioDto>(ErroresDeEjercicio.Cerrado(id));
        }

        var errores = new ErroresPorCampo();
        ReglasDeFechas.Comprobar(peticion.FechaDeInicio, peticion.FechaDeFin, errores);

        if (errores.Hay)
        {
            return Resultado.Fallo<EjercicioDto>(errores.AError());
        }

        ejercicio.Modificar(peticion.FechaDeInicio, peticion.FechaDeFin);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(ejercicio.ADto());
    }
}
