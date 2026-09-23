using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Application.Validacion;
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
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="peticion">Las fechas nuevas.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<EjercicioDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarEjercicioDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IModificarEjercicio"/>
internal sealed class ModificarEjercicio(
    IRepositorioDeEjercicios ejercicios,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones,
    IEnumerable<IDocumentosDeUnPeriodo> documentos) : IModificarEjercicio
{
    public async Task<Resultado<EjercicioDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarEjercicioDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Ejercicio? ejercicio = await ejercicios.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (ejercicio is null)
        {
            return Resultado.Fallo<EjercicioDto>(ErroresDeEjercicio.NoEncontrado(id));
        }

        versiones.Exigir(ejercicio, version);

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

        // Mover un ejercicio puede meterlo encima de otro, así que pregunta lo mismo que el alta.
        // `excepto: id` es lo que hace que guardar sin mover las fechas no se encuentre solapado
        // consigo mismo, que es el 409 más absurdo que se puede dar.
        if (await ejercicios
                .HaySolapeAsync(
                    ejercicio.EmpresaId, peticion.FechaDeInicio, peticion.FechaDeFin, id, cancelacion)
                .ConfigureAwait(false))
        {
            return Resultado.Fallo<EjercicioDto>(ErroresDeEjercicio.Solapado());
        }

        // Y LA MISMA PREGUNTA QUE EL CIERRE, sobre lo que el intervalo nuevo dejaría fuera. No
        // sobre el intervalo entero: preguntar por todo dejaría sin poder moverse cualquier
        // ejercicio con un solo movimiento dentro, que es todos. Aquí se pregunta por documentos
        // en CUALQUIER estado, no solo borradores: un confirmado que se queda fuera es peor,
        // porque ya está contado en un periodo del que va a dejar de formar parte.
        IReadOnlyList<(DateOnly Desde, DateOnly Hasta)> fuera =
            ejercicio.LoQueDejariaFuera(peticion.FechaDeInicio, peticion.FechaDeFin);

        if (fuera.Count > 0)
        {
            IReadOnlyList<string> conDocumentos = await LosModulosConDocumentos
                .QuienTieneDocumentosAsync(
                    LosModulosConDocumentos.Inscritos(documentos, "mover este ejercicio"),
                    ejercicio.EmpresaId,
                    fuera,
                    cancelacion)
                .ConfigureAwait(false);

            if (conDocumentos.Count > 0)
            {
                return Resultado.Fallo<EjercicioDto>(
                    ErroresDeEjercicio.DejariaDocumentosFuera(conDocumentos));
            }
        }

        ejercicio.Modificar(peticion.FechaDeInicio, peticion.FechaDeFin);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(ejercicio.ADto());
    }
}
