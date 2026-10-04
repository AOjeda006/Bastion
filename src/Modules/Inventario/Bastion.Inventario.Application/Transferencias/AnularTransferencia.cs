using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Transferencias;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Organizacion.Contracts.Ejercicios;

namespace Bastion.Inventario.Application.Transferencias;

/// <summary>Anula una transferencia enviada o recibida oponiéndole un inverso (R2).</summary>
public interface IAnularTransferencia
{
    /// <summary>Ejecuta la anulación.</summary>
    /// <param name="transferenciaId">El documento que se anula.</param>
    /// <param name="peticion">Por qué se anula.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>El par —original anulado e inverso recibido—, o el motivo por el que no.</returns>
    Task<Resultado<AnulacionDeTransferenciaDto>> EjecutarAsync(
        Guid transferenciaId,
        AnularTransferenciaDto peticion,
        CancellationToken cancelacion);
}

/// <summary>
/// La R2 de la transferencia, en una transacción: nace el inverso, toma su número, niega cada pata
/// que el original escribió, y solo entonces el original queda anulado.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es la anulación del ajuste con dos puntas</b> (ADR-0053 §5). De una enviada niega la salida
/// del origen y el tránsito; de una recibida, la entrada en el destino y la salida del origen. La
/// fecha del inverso es hoy, y la R9 pregunta por hoy; el número, en la serie del original y con su
/// fecha de envío, que es la excepción del ADR-0043 §4.
/// </para>
/// <para>
/// <b>El destino se valora antes que el origen</b>, porque lo que vuelve al origen de una recibida
/// es lo que salió del destino de verdad: como mucho lo que quedaba, y todo lo que quedaba si lo
/// vació (ADR-0053 §6, ADR-0054). Si las unidades ya no están en el destino, la salida choca con la
/// guarda de la existencia y es el <c>422</c> <c>stock-insuficiente</c> de la excepción de la R2.
/// </para>
/// <para>
/// <b>Y con las valoraciones bloqueadas, la versión del original</b>, por lo mismo que en la
/// recepción: quien llega segundo a una carrera sobre la misma transferencia sale con el
/// <c>412</c> del §10, valore lo que valore. Detrás siguen el testigo de la fila y el índice único
/// de <c>anula_a_id</c>, que dicen lo mismo.
/// </para>
/// </remarks>
/// <param name="transferencias">Dónde viven los dos documentos y lo que mueven.</param>
/// <param name="numerador">Quién entrega el correlativo, en esta misma transacción (R5).</param>
/// <param name="ejercicios">Si <b>hoy</b> admite escrituras, con la fila bloqueada (R9).</param>
/// <param name="valoracion">Quién valora las dos patas contra los saldos bloqueados (ADR-0046 §10).</param>
/// <param name="unidadTrabajo">La transacción.</param>
/// <param name="reloj">De dónde sale «hoy».</param>
internal sealed class AnularTransferencia(
    IRepositorioDeTransferencias transferencias,
    INumeradorDeSeriesDeInventario numerador,
    IConsultaDeEjercicios ejercicios,
    IValoracionDeExistencias valoracion,
    IUnidadTrabajoDeInventario unidadTrabajo,
    TimeProvider reloj) : IAnularTransferencia
{
    /// <inheritdoc/>
    public async Task<Resultado<AnulacionDeTransferenciaDto>> EjecutarAsync(
        Guid transferenciaId,
        AnularTransferenciaDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        string motivo = (peticion.Motivo ?? string.Empty).Trim();

        if (motivo.Length is 0 or > Transferencia.LargoDelMotivo)
        {
            return Resultado.Fallo<AnulacionDeTransferenciaDto>(ErroresDeTransferencia.MotivoNoValido());
        }

        Transferencia? original = await transferencias
            .ObtenerAsync(transferenciaId, cancelacion)
            .ConfigureAwait(false);

        if (original is null)
        {
            return Resultado.Fallo<AnulacionDeTransferenciaDto>(ErroresDeTransferencia.NoEncontrada(transferenciaId));
        }

        if (original.AnulaAId is not null
            || original.Estado is not (EstadoDeTransferencia.Enviada or EstadoDeTransferencia.Recibida))
        {
            return Resultado.Fallo<AnulacionDeTransferenciaDto>(
                ErroresDeTransferencia.NoSeAnula(transferenciaId, original.Estado, original.AnulaAId is not null));
        }

        DateTimeOffset ahora = reloj.GetUtcNow();
        var hoy = DateOnly.FromDateTime(ahora.UtcDateTime);

        EstadoDelEjercicioParaEscribir ejercicio = await ejercicios
            .ParaEscribirEnAsync(hoy, cancelacion)
            .ConfigureAwait(false);

        if (ejercicio is not EstadoDelEjercicioParaEscribir.Abierto)
        {
            return Resultado.Fallo<AnulacionDeTransferenciaDto>(
                ejercicio is EstadoDelEjercicioParaEscribir.SinEjercicio
                    ? ErroresDeTransferencia.SinEjercicio(hoy)
                    : ErroresDeTransferencia.EnEjercicioCerrado(hoy));
        }

        Transferencia inverso = original.CrearInverso(hoy, motivo, ahora);

        // LA SERIE DEL ORIGINAL, CON SU FECHA DE ENVÍO: el inverso pertenece a su original, no a su
        // fecha (ADR-0053 §5, ADR-0043 §4).
        Resultado<long> numero = await numerador
            .TomarNumeroAsync(
                inverso.SerieId, TipoDeDocumentoOrigen.Transferencia, original.FechaDeEnvio, cancelacion)
            .ConfigureAwait(false);

        if (!numero.EsCorrecto)
        {
            return Resultado.Fallo<AnulacionDeTransferenciaDto>(numero.Error!);
        }

        bool niegaLaRecepcion = original.Estado is EstadoDeTransferencia.Recibida;
        IReadOnlyList<LineaAValorar>? enElDestino = niegaLaRecepcion ? inverso.LineasAValorarEnElDestino() : null;
        ClaveDeValoracion[] claves =
        [
            .. inverso.Lineas
                .SelectMany(linea => (ClaveDeValoracion[])
                [
                    new(linea.ArticuloId, inverso.AlmacenOrigenId),
                    new(linea.ArticuloId, inverso.AlmacenDestinoId),
                ])
                .Distinct(),
        ];

        // LAS DOS PUNTAS EN UNA SOLA LLAMADA, aunque una enviada no valore el destino: su tránsito
        // baja, y la recepción que compite con esta anulación espera ahí (ADR-0053 §10).
        IReadOnlyDictionary<ClaveDeValoracion, SaldoValorado> saldos = await transferencias
            .BloquearLasValoracionesAsync(claves, inverso.Divisa, cancelacion)
            .ConfigureAwait(false);

        if (!await transferencias.SigueComoSeLeyoAsync(original, cancelacion).ConfigureAwait(false))
        {
            return Resultado.Fallo<AnulacionDeTransferenciaDto>(ErroresDeConcurrencia.ObsoletaYSinRecurso());
        }

        IReadOnlyList<LineaValorada>? valoradasEnElDestino = null;

        if (enElDestino is not null)
        {
            if (valoracion.LoQueImpide(saldos, enElDestino, inverso.Divisa, hoy) is { } enElDestinoNo)
            {
                return Resultado.Fallo<AnulacionDeTransferenciaDto>(
                    ErroresDeTransferencia.NoSeValora(enElDestinoNo, inverso.Divisa));
            }

            valoradasEnElDestino = valoracion.Valorar(saldos, enElDestino, inverso.Divisa, hoy);
        }

        IReadOnlyList<LineaAValorar> enElOrigen = inverso.LineasAValorarEnElOrigen(valoradasEnElDestino);

        if (valoracion.LoQueImpide(saldos, enElOrigen, inverso.Divisa, hoy) is { } enElOrigenNo)
        {
            return Resultado.Fallo<AnulacionDeTransferenciaDto>(
                ErroresDeTransferencia.NoSeValora(enElOrigenNo, inverso.Divisa));
        }

        IReadOnlyList<LineaValorada> valoradasEnElOrigen = valoracion.Valorar(saldos, enElOrigen, inverso.Divisa, hoy);

        // LOS LOTES Y LAS SERIES DEL ORIGINAL, que el inverso copia: resuelven a las mismas filas.
        LotesYSeriesResueltos resueltos = await transferencias
            .ResolverLotesYSeriesAsync(inverso.LotesQueNombra(), inverso.SeriesQueNombra(), cancelacion)
            .ConfigureAwait(false);

        // EL INVERSO NACE RECIBIDO, y lo cuenta como una recepción, con sus dos fechas en hoy.
        var recibido = new TransferenciaRecibida(
            inverso.Id,
            inverso.EmpresaId,
            inverso.AlmacenDestinoId,
            hoy,
            inverso.Lineas.Count);

        LoQueMueveLaTransferencia movido = inverso.ConfirmarComoInverso(
            original, numero.Valor, recibido, valoradasEnElDestino, valoradasEnElOrigen, resueltos, ahora);

        original.Anular(inverso, new TransferenciaAnulada(original.Id, original.EmpresaId));

        transferencias.Agregar(inverso);
        await transferencias.MoverAsync(movido, cancelacion).ConfigureAwait(false);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(new AnulacionDeTransferenciaDto(original.ADto(), inverso.ADto()));
    }
}
