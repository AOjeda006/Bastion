using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Application.Ajustes;
using Bastion.Inventario.Contracts.Recuentos;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Recuentos;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Organizacion.Contracts.Ejercicios;

namespace Bastion.Inventario.Application.Recuentos;

/// <summary>Anula un recuento confirmado, con el ajuste de su diferencia si lo tiene.</summary>
public interface IAnularRecuento
{
    /// <summary>Ejecuta la anulación.</summary>
    /// <param name="recuentoId">El recuento.</param>
    /// <param name="version">La versión de la cabecera que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="peticion">Por qué se anula.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>El recuento anulado, o el motivo por el que no se anula.</returns>
    Task<Resultado<RecuentoDto>> EjecutarAsync(
        Guid recuentoId,
        VersionDeRecurso version,
        AnularRecuentoDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IAnularRecuento"/>
/// <remarks>
/// <para>
/// <b>Anular el recuento es anular su ajuste</b> (ADR-0055 §9), en la misma transacción y con el
/// núcleo de <see cref="AnularAjuste"/>: el inverso, con la fecha de hoy, numera en la serie del
/// ajuste y compensa su libro. Es el único camino que anula el ajuste de un recuento, porque el público
/// lo rechaza con su <c>409</c>.
/// </para>
/// <para>
/// <b>Si no movió el libro, solo cambia de estado</b>: no hay nada que compensar, y no se pregunta por
/// el ejercicio de hoy, porque no se escribe nada con esa fecha. Un recuento confirmado sin diferencias
/// se anula aunque el ejercicio de su confirmación esté cerrado, como se anula un ajuste viejo.
/// </para>
/// <para>
/// <b>El <c>412</c> sale de aquí y no del testigo de EF Core</b>, como al confirmar (ADR-0057). Dos
/// anulaciones del mismo recuento se ponen en fila en su cabecera, y la segunda lee la versión que dejó
/// la primera. El ajuste no puede chocar contra su testigo: solo lo anula este camino, y siempre con la
/// cabecera bloqueada.
/// </para>
/// </remarks>
/// <param name="recuentos">El documento y su ajuste.</param>
/// <param name="ajustes">Dónde viven el ajuste, las valoraciones y el libro.</param>
/// <param name="numerador">Quién entrega el correlativo del inverso, en esta misma transacción (R5).</param>
/// <param name="ejercicios">Si hoy se puede escribir, con la fila del ejercicio bloqueada (R9).</param>
/// <param name="valoracion">Quién valora el inverso contra los saldos bloqueados.</param>
/// <param name="versiones">La versión de la cabecera, que se compara con la del <c>If-Match</c>.</param>
/// <param name="unidadTrabajo">La transacción.</param>
/// <param name="reloj">De dónde sale «hoy», que es la fecha del inverso.</param>
internal sealed class AnularRecuento(
    IRepositorioDeRecuentos recuentos,
    IRepositorioDeAjustes ajustes,
    INumeradorDeSeriesDeInventario numerador,
    IConsultaDeEjercicios ejercicios,
    IValoracionDeExistencias valoracion,
    IVersionesDeInventario versiones,
    IUnidadTrabajoDeInventario unidadTrabajo,
    TimeProvider reloj) : IAnularRecuento
{
    /// <inheritdoc/>
    public async Task<Resultado<RecuentoDto>> EjecutarAsync(
        Guid recuentoId,
        VersionDeRecurso version,
        AnularRecuentoDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        // EL MOTIVO, AQUÍ Y NO SOLO EN EL DOMINIO: el dominio lanza, y un cuerpo mal escrito es un 400
        // con su código (ADR-0004).
        string motivo = (peticion.Motivo ?? string.Empty).Trim();

        if (motivo.Length is 0 or > Recuento.LargoDelMotivo)
        {
            return Resultado.Fallo<RecuentoDto>(ErroresDeRecuento.MotivoDeAnularODescartarNoValido());
        }

        // LA FILA, BLOQUEADA, Y SU VERSIÓN ANTES QUE SU ESTADO (ADR-0057 §3): quien anula dos veces con
        // la misma versión recibe el 412, que es su causa.
        Resultado<Recuento> leido = await ElRecuentoEnCurso
            .BloquearYLeerEnSuVersionAsync(recuentos, versiones, recuentoId, version, cancelacion)
            .ConfigureAwait(false);

        if (!leido.EsCorrecto)
        {
            return Resultado.Fallo<RecuentoDto>(leido.Error!);
        }

        Recuento recuento = leido.Valor;

        if (recuento.Estado != EstadoDeRecuento.Confirmado)
        {
            return Resultado.Fallo<RecuentoDto>(
                ErroresDeRecuento.NoEstaConfirmado(recuentoId, recuento.Estado.ToString()));
        }

        Ajuste? ajuste = null;

        if (recuento.MovioElLibro)
        {
            Resultado<Ajuste> anulado = await AnularSuAjusteAsync(recuento, motivo, cancelacion).ConfigureAwait(false);

            if (!anulado.EsCorrecto)
            {
                return Resultado.Fallo<RecuentoDto>(anulado.Error!);
            }

            ajuste = anulado.Valor;
        }

        recuento.Anular(motivo, ajuste, new RecuentoAnulado(recuento.Id, recuento.EmpresaId, recuento.AlmacenId));

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(recuento.ADto(ElTeoricoDeLasLineas.AlConfirmar(recuento, ajuste)));
    }

    /// <summary>El ajuste del recuento, anulado con su inverso, o por qué no se puede.</summary>
    private async Task<Resultado<Ajuste>> AnularSuAjusteAsync(
        Recuento recuento,
        string motivo,
        CancellationToken cancelacion)
    {
        DateTimeOffset ahora = reloj.GetUtcNow();
        var hoy = DateOnly.FromDateTime(ahora.UtcDateTime);

        // EL EJERCICIO DE HOY, que es la fecha del inverso, antes de gastar su número: la misma pregunta
        // que hace la anulación pública, y por lo mismo (R9).
        EstadoDelEjercicioParaEscribir ejercicio = await ejercicios
            .ParaEscribirEnAsync(hoy, cancelacion)
            .ConfigureAwait(false);

        if (ejercicio is not EstadoDelEjercicioParaEscribir.Abierto)
        {
            return Resultado.Fallo<Ajuste>(
                ejercicio is EstadoDelEjercicioParaEscribir.SinEjercicio
                    ? ErroresDeRecuento.SinEjercicio(hoy)
                    : ErroresDeRecuento.EnEjercicioCerrado(hoy));
        }

        // EL AJUSTE, POR LA FLECHA CONTRARIA Y SEGUIDO: la consulta del recuento lo lee para enseñarlo,
        // y aquí hace falta el que el contexto guardará anulado.
        Guid ajusteId = (await recuentos.AjusteDeAsync(recuento.Id, cancelacion).ConfigureAwait(false))?.Id
            ?? throw new InvalidOperationException(
                $"El recuento {recuento.Id} movió el libro y no tiene ajuste que apunte a él: la doble " +
                "flecha del ADR-0055 §8 está rota.");

        Ajuste original = await ajustes.ObtenerAsync(ajusteId, cancelacion).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"El ajuste {ajusteId} del recuento {recuento.Id} se ha leído y ya no está.");

        Ajuste inverso = recuento.CrearInversoDeSuAjuste(original, hoy, motivo, ahora);

        Resultado<Ajuste> compensado = await ElInversoQueCompensa
            .CompensarAsync(ajustes, numerador, valoracion, original, inverso, ahora, cancelacion)
            .ConfigureAwait(false);

        return compensado.EsCorrecto ? Resultado.Correcto(original) : Resultado.Fallo<Ajuste>(compensado.Error!);
    }
}
