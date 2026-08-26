using Bastion.BuildingBlocks.Application.Autorizacion;
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
    IUsuarioActual usuarioActual,
    IRepositorioDeEjercicios ejercicios,
    IRepositorioDeEmpresas empresas,
    IUnidadTrabajoDeOrganizacion unidadTrabajo) : ICrearEjercicio
{
    public async Task<Resultado<EjercicioDto>> EjecutarAsync(
        CrearEjercicioDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        // La empresa sale del CLAIM y no de la petición (R8). `CrearEjercicioDto` no tiene el
        // campo, así que no hay ningún camino por el que pueda entrar otra.
        Guid empresaId = usuarioActual.EmpresaId;

        // Se comprueba aquí y no se deja a la clave ajena: la base impediría el insert igualmente,
        // pero con una excepción de PostgreSQL que sale como 500. Y ya no es «un identificador mal
        // escrito» —viene del token—, sino una sesión que apunta a una empresa que ya no opera.
        if (!await empresas.EstaActivaAsync(empresaId, cancelacion).ConfigureAwait(false))
        {
            return Resultado.Fallo<EjercicioDto>(ErroresDeEmpresa.NoOperativa());
        }

        var errores = new ErroresPorCampo();

        ReglasDeFechas.Comprobar(peticion.FechaDeInicio, peticion.FechaDeFin, errores);

        if (errores.Hay)
        {
            return Resultado.Fallo<EjercicioDto>(errores.AError());
        }

        if (await ejercicios.ExisteElAnioAsync(empresaId, peticion.Anio, cancelacion)
                .ConfigureAwait(false))
        {
            return Resultado.Fallo<EjercicioDto>(ErrorDeOperacion.Conflicto(
                "ejercicio-duplicado",
                $"La empresa ya tiene abierto el ejercicio {peticion.Anio}."));
        }

        var ejercicio = Ejercicio.Crear(
            empresaId, peticion.Anio, peticion.FechaDeInicio, peticion.FechaDeFin);

        ejercicios.Agregar(ejercicio);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(ejercicio.ADto());
    }
}
