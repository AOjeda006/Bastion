using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Domain.Recuentos;

namespace Bastion.Inventario.Application.Recuentos;

/// <summary>Quita una línea de un recuento en curso, y su clave queda como está.</summary>
public interface IQuitarLineaDeRecuento
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="recuentoId">El recuento.</param>
    /// <param name="lineaId">La línea.</param>
    /// <param name="version">La versión de la línea que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>Correcto, o el motivo por el que no se quita.</returns>
    Task<Resultado> EjecutarAsync(
        Guid recuentoId,
        Guid lineaId,
        VersionDeRecurso version,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IQuitarLineaDeRecuento"/>
/// <remarks>
/// <para>
/// <b>Quitar es decir que esa clave no se cuenta</b> (ADR-0055 §5): una línea sin contar no es un
/// cero, y no se confirma con ella. El recuento solo dice algo de las claves que lleva, así que la
/// que se quita queda en el libro como estaba.
/// </para>
/// <para>
/// <b>Con la versión de la línea</b>, por lo mismo que contarla: quien la quita la ha visto, y si
/// otro la ha contado después, no se la lleva sin verlo. El borrado lleva la versión en su
/// <c>WHERE</c>, y si ya no es la suya, <c>412</c>.
/// </para>
/// </remarks>
/// <param name="recuentos">El documento.</param>
/// <param name="versiones">La versión de la línea, que se exige, y la de la cabecera, que se toca.</param>
/// <param name="unidadTrabajo">La transacción que abarca el cerrojo, la lectura y el guardado.</param>
internal sealed class QuitarLineaDeRecuento(
    IRepositorioDeRecuentos recuentos,
    IVersionesDeInventario versiones,
    IUnidadTrabajoDeInventario unidadTrabajo) : IQuitarLineaDeRecuento
{
    /// <inheritdoc/>
    public Task<Resultado> EjecutarAsync(
        Guid recuentoId,
        Guid lineaId,
        VersionDeRecurso version,
        CancellationToken cancelacion) =>
        unidadTrabajo.EnTransaccionAsync(
            enCurso => QuitarAsync(recuentoId, lineaId, version, enCurso), cancelacion);

    private async Task<Resultado> QuitarAsync(
        Guid recuentoId,
        Guid lineaId,
        VersionDeRecurso version,
        CancellationToken cancelacion)
    {
        Resultado<Recuento> enCurso = await ElRecuentoEnCurso
            .BloquearYLeerAsync(recuentos, recuentoId, cancelacion)
            .ConfigureAwait(false);

        if (!enCurso.EsCorrecto)
        {
            return Resultado.Fallo(enCurso.Error!);
        }

        Recuento recuento = enCurso.Valor;
        LineaDeRecuento? linea = recuento.Lineas.FirstOrDefault(candidata => candidata.Id == lineaId);

        if (linea is null)
        {
            return Resultado.Fallo(ErroresDeRecuento.LineaNoEncontrada(recuentoId, lineaId));
        }

        versiones.Exigir(linea, version);
        recuento.QuitarLinea(linea.Id);
        ElRecuentoEnCurso.Tocar(versiones, recuento);

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}
