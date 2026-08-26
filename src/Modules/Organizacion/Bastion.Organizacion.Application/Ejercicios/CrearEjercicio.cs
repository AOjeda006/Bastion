using Bastion.BuildingBlocks.Application;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Application.Empresas;
using Bastion.Organizacion.Contracts.Ejercicios;
using Bastion.Organizacion.Domain.Ejercicios;

namespace Bastion.Organizacion.Application.Ejercicios;

/// <summary>Abre un ejercicio contable.</summary>
public interface ICrearEjercicio
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="peticion">Datos del ejercicio que se quiere abrir.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<EjercicioDto>> EjecutarAsync(CrearEjercicioDto peticion, CancellationToken cancelacion);
}

/// <inheritdoc cref="ICrearEjercicio"/>
internal sealed class CrearEjercicio(
    IRepositorioDeEjercicios ejercicios,
    IRepositorioDeEmpresas empresas,
    IUnidadTrabajo unidadTrabajo) : ICrearEjercicio
{
    public async Task<Resultado<EjercicioDto>> EjecutarAsync(
        CrearEjercicioDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        var errores = new ErroresPorCampo();

        // La empresa se comprueba aquí y no se deja a la clave ajena. La base impediría el
        // insert igualmente, pero con una excepción de PostgreSQL que sale como 500; y el
        // usuario no ha hecho nada mal salvo escribir mal un identificador, que es un 400 de
        // ese campo.
        if (!await empresas.ExisteAsync(peticion.EmpresaId, cancelacion).ConfigureAwait(false))
        {
            errores.Agregar("empresaId", "No hay ninguna empresa con ese identificador.");
        }

        ReglasDeFechas.Comprobar(peticion.FechaDeInicio, peticion.FechaDeFin, errores);

        if (errores.Hay)
        {
            return Resultado.Fallo<EjercicioDto>(errores.AError());
        }

        if (await ejercicios.ExisteElAnioAsync(peticion.EmpresaId, peticion.Anio, cancelacion)
                .ConfigureAwait(false))
        {
            return Resultado.Fallo<EjercicioDto>(ErrorDeOperacion.Conflicto(
                "ejercicio-duplicado",
                $"La empresa ya tiene abierto el ejercicio {peticion.Anio}."));
        }

        var ejercicio = Ejercicio.Crear(
            peticion.EmpresaId, peticion.Anio, peticion.FechaDeInicio, peticion.FechaDeFin);

        ejercicios.Agregar(ejercicio);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(ejercicio.ADto());
    }
}
