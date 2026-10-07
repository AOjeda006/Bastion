using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Recuentos;
using Bastion.Inventario.Domain.Recuentos;

namespace Bastion.Inventario.Application.Recuentos;

/// <summary>Anota lo contado en una línea de un recuento en curso.</summary>
public interface IContarLineaDeRecuento
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="recuentoId">El recuento.</param>
    /// <param name="lineaId">La línea.</param>
    /// <param name="version">La versión de la línea que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="peticion">Lo contado.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>La línea contada, con el teórico de ahora, o el motivo por el que no se cuenta.</returns>
    Task<Resultado<LineaDeRecuentoDto>> EjecutarAsync(
        Guid recuentoId,
        Guid lineaId,
        VersionDeRecurso version,
        ContarLineaDeRecuentoDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IContarLineaDeRecuento"/>
/// <remarks>
/// <para>
/// <b>La versión que se exige es la de la línea, y la que cambia además es la de la cabecera</b>
/// (ADR-0055 §4). Dos personas que cuentan líneas distintas se esperan en el cerrojo de la cabecera
/// un instante, y ninguna recibe un <c>412</c> por lo que hizo la otra. Dos que cuentan la misma, sí:
/// la segunda contaría sin haber visto la cifra de la primera.
/// </para>
/// <para>
/// <b>El teórico que se anota es el de ahora, y no decide nada</b>: sirve para que la pantalla diga
/// qué líneas han cambiado desde que alguien las contó. El que decide es el de la confirmación, leído
/// con las valoraciones bloqueadas. Por eso se lee sin cerrojo, y solo del artículo de la línea.
/// </para>
/// </remarks>
/// <param name="recuentos">El documento y las existencias.</param>
/// <param name="versiones">La versión de la línea, que se exige, y la de la cabecera, que se toca.</param>
/// <param name="unidadTrabajo">La transacción que abarca el cerrojo, la lectura y el guardado.</param>
internal sealed class ContarLineaDeRecuento(
    IRepositorioDeRecuentos recuentos,
    IVersionesDeInventario versiones,
    IUnidadTrabajoDeInventario unidadTrabajo) : IContarLineaDeRecuento
{
    /// <inheritdoc/>
    public Task<Resultado<LineaDeRecuentoDto>> EjecutarAsync(
        Guid recuentoId,
        Guid lineaId,
        VersionDeRecurso version,
        ContarLineaDeRecuentoDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        return unidadTrabajo.EnTransaccionAsync(
            enCurso => ContarAsync(recuentoId, lineaId, version, peticion, enCurso), cancelacion);
    }

    private async Task<Resultado<LineaDeRecuentoDto>> ContarAsync(
        Guid recuentoId,
        Guid lineaId,
        VersionDeRecurso version,
        ContarLineaDeRecuentoDto peticion,
        CancellationToken cancelacion)
    {
        Resultado<Recuento> enCurso = await ElRecuentoEnCurso
            .BloquearYLeerAsync(recuentos, recuentoId, cancelacion)
            .ConfigureAwait(false);

        if (!enCurso.EsCorrecto)
        {
            return Resultado.Fallo<LineaDeRecuentoDto>(enCurso.Error!);
        }

        Recuento recuento = enCurso.Valor;
        LineaDeRecuento? linea = recuento.Lineas.FirstOrDefault(candidata => candidata.Id == lineaId);

        if (linea is null)
        {
            return Resultado.Fallo<LineaDeRecuentoDto>(ErroresDeRecuento.LineaNoEncontrada(recuentoId, lineaId));
        }

        versiones.Exigir(linea, version);

        if (peticion.Contado is not { } contado || !LineaDeRecuento.SePuedeContar(contado, linea.NumeroDeSerie is not null))
        {
            return Resultado.Fallo<LineaDeRecuentoDto>(ErroresDeRecuento.ContadoNoValido());
        }

        ElTeoricoDeLasLineas teorico = await ElTeoricoDeLasLineas
            .LeerAsync(recuento, recuentos, [linea.ArticuloId], cancelacion)
            .ConfigureAwait(false);

        // LA LÍNEA ES DEL ARTÍCULO QUE SE HA LEÍDO, así que tiene teórico, aunque sea cero: una clave
        // sin fila en las existencias no tiene nada. Si falta, la lectura y la línea no casan.
        decimal teoricoAhora = teorico.De(linea) ?? throw new InvalidOperationException(
            $"La línea {linea.Numero} del recuento {recuentoId} se ha quedado sin teórico, y se leyó su " +
            "artículo.");

        recuento.Contar(linea.Id, contado, teoricoAhora);
        ElRecuentoEnCurso.Tocar(versiones, recuento);

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(linea.ADto(teorico));
    }
}
