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
    ICerrojoDeEjercicios cerrojo,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones,
    IEnumerable<IDocumentosDeUnPeriodo> documentos) : IModificarEjercicio
{
    /// <inheritdoc />
    /// <remarks>
    /// <b>Dentro de UNA transacción y con el cerrojo exclusivo lo primero, como cerrar.</b> Mover
    /// compite con confirmar por lo mismo que cerrar: decide mirando qué documentos hay dentro, y
    /// un documento que se está confirmando en ese momento no se ve hasta su <c>COMMIT</c>. El
    /// <c>UPDATE</c> del final también toma un cerrojo sobre la fila, y también espera a la
    /// confirmación en vuelo; pero espera DESPUÉS de haber preguntado, así que lo que decide es
    /// una respuesta vieja. Y la R11 no lo recoge por detrás: confirmar no escribe la fila del
    /// ejercicio, su <c>FOR SHARE</c> no mueve el <c>xmin</c>, y el <c>UPDATE</c> pasa. Sin el
    /// cerrojo, el inverso de una anulación en vuelo se quedaba fuera de todo ejercicio con un 200.
    /// </remarks>
    public Task<Resultado<EjercicioDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarEjercicioDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        return unidadTrabajo.EnTransaccionAsync(
            dentro => MoverDentroDeLaTransaccionAsync(id, version, peticion, dentro), cancelacion);
    }

    private async Task<Resultado<EjercicioDto>> MoverDentroDeLaTransaccionAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarEjercicioDto peticion,
        CancellationToken cancelacion)
    {
        // EL CERROJO ANTES DE PREGUNTAR AL PUERTO, que es lo que compra: la pregunta de abajo se
        // hace cuando ya no puede entrar ningún documento nuevo en el intervalo, y después de que
        // los que se estaban confirmando hayan llegado a su `COMMIT`. Va antes también de la
        // lectura por el ORM, por el motivo que da `CerrarEjercicio`: así no hay dos versiones de
        // la fila en la misma operación.
        if (await cerrojo.TomarEnExclusivaAsync(id, cancelacion).ConfigureAwait(false) is null)
        {
            return Resultado.Fallo<EjercicioDto>(ErroresDeEjercicio.NoEncontrado(id));
        }

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
