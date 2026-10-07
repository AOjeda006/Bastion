using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Recuentos;
using Bastion.Inventario.Domain.Recuentos;

namespace Bastion.Inventario.Application.Recuentos;

/// <summary>Descarta un recuento en curso, con su motivo.</summary>
public interface IDescartarRecuento
{
    /// <summary>Ejecuta el descarte.</summary>
    /// <param name="recuentoId">El recuento.</param>
    /// <param name="version">La versión de la cabecera que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="peticion">Por qué se descarta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>El recuento descartado, o el motivo por el que no se descarta.</returns>
    Task<Resultado<RecuentoDto>> EjecutarAsync(
        Guid recuentoId,
        VersionDeRecurso version,
        DescartarRecuentoDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IDescartarRecuento"/>
/// <remarks>
/// <para>
/// <b>Descartar no mueve nada</b> (ADR-0055 §1.6): el recuento se queda sin número, con su motivo y con
/// lo contado, y el almacén admite otro en curso, porque el índice único solo mira los que están en
/// curso.
/// </para>
/// <para>
/// <b>La transacción es la de la unidad de trabajo, o la del filtro si llega con clave</b>: la clave se
/// admite y no se exige, porque descartar no numera. Con las dos, el <c>412</c> sale de aquí, con la
/// fila bloqueada y antes de escribir, como al confirmar (ADR-0057).
/// </para>
/// </remarks>
/// <param name="recuentos">El documento.</param>
/// <param name="versiones">La versión de la cabecera, que se compara con la del <c>If-Match</c>.</param>
/// <param name="unidadTrabajo">La transacción.</param>
internal sealed class DescartarRecuento(
    IRepositorioDeRecuentos recuentos,
    IVersionesDeInventario versiones,
    IUnidadTrabajoDeInventario unidadTrabajo) : IDescartarRecuento
{
    /// <inheritdoc/>
    public Task<Resultado<RecuentoDto>> EjecutarAsync(
        Guid recuentoId,
        VersionDeRecurso version,
        DescartarRecuentoDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        return unidadTrabajo.EnTransaccionAsync(
            enCurso => DescartarAsync(recuentoId, version, peticion, enCurso), cancelacion);
    }

    private async Task<Resultado<RecuentoDto>> DescartarAsync(
        Guid recuentoId,
        VersionDeRecurso version,
        DescartarRecuentoDto peticion,
        CancellationToken cancelacion)
    {
        string motivo = (peticion.Motivo ?? string.Empty).Trim();

        if (motivo.Length is 0 or > Recuento.LargoDelMotivo)
        {
            return Resultado.Fallo<RecuentoDto>(ErroresDeRecuento.MotivoDeAnularODescartarNoValido());
        }

        // LA VERSIÓN ANTES QUE EL ESTADO (ADR-0057 §3): quien descarta dos veces con la misma versión
        // recibe el 412, y con la de ahora, el 409 de un recuento que ya no está en curso.
        Resultado<Recuento> leido = await ElRecuentoEnCurso
            .BloquearYLeerEnSuVersionAsync(recuentos, versiones, recuentoId, version, cancelacion)
            .ConfigureAwait(false);

        if (!leido.EsCorrecto)
        {
            return Resultado.Fallo<RecuentoDto>(leido.Error!);
        }

        Recuento recuento = leido.Valor;

        if (recuento.Estado != EstadoDeRecuento.EnCurso)
        {
            return Resultado.Fallo<RecuentoDto>(
                ErroresDeRecuento.NoEstaEnCurso(recuentoId, recuento.Estado.ToString()));
        }

        recuento.Descartar(motivo, new RecuentoDescartado(recuento.Id, recuento.EmpresaId, recuento.AlmacenId));

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        // UNO DESCARTADO NO TIENE TEÓRICO: ni el de ahora, porque ya no está en curso, ni el de la
        // confirmación, porque no se confirmó.
        ElTeoricoDeLasLineas sinTeorico = await ElTeoricoDeLasLineas
            .LeerAsync(recuento, recuentos, articulos: null, cancelacion)
            .ConfigureAwait(false);

        return Resultado.Correcto(recuento.ADto(sinTeorico));
    }
}
