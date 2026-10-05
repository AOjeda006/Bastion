using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Inventario.Application.Trazabilidad;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Transferencias;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Organizacion.Contracts.Ejercicios;

namespace Bastion.Inventario.Application.Transferencias;

/// <summary>Envía una transferencia: sale del origen, y lo que sale queda en tránsito.</summary>
public interface IEnviarTransferencia
{
    /// <summary>Ejecuta el envío.</summary>
    /// <param name="transferenciaId">El documento.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>La transferencia enviada, o el motivo por el que no sale.</returns>
    Task<Resultado<TransferenciaDto>> EjecutarAsync(Guid transferenciaId, CancellationToken cancelacion);
}

/// <summary>
/// El envío: el número, la salida del origen y el tránsito del destino, en el mismo <c>COMMIT</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es la confirmación del ajuste con una pata más</b>, y dobla la R12 por el mismo motivo: el
/// documento, las filas del libro y lo que vuela caen juntos, o la R3 tendría una ventana con la
/// mercancía fuera del origen y en ningún sitio.
/// </para>
/// <para>
/// <b>Los cerrojos van en el orden del ADR-0053 §10</b>: el ejercicio de la fecha de envío, la
/// marca de los artículos, el contador de la serie, las valoraciones del origen y del destino en una
/// sola llamada y en orden de clave, los lotes y las series, la fila del documento y las
/// existencias. Las del destino se bloquean aunque el envío no las valore, porque lo que vuela se
/// suma a ellas: así dos transferencias cruzadas, A→B y B→A, toman las mismas dos claves en el
/// mismo orden y no se interbloquean.
/// </para>
/// </remarks>
/// <param name="transferencias">Dónde viven el documento y lo que mueve.</param>
/// <param name="numerador">Quién entrega el correlativo, en esta misma transacción (R5).</param>
/// <param name="ejercicios">Si la fecha de envío se puede escribir, con la fila bloqueada.</param>
/// <param name="trazabilidad">La marca de los artículos, con la fila bloqueada (ADR-0048 §4).</param>
/// <param name="valoracion">Quién valora la salida contra los saldos bloqueados (ADR-0046 §10).</param>
/// <param name="unidadTrabajo">La transacción.</param>
/// <param name="reloj">De dónde sale «ahora».</param>
internal sealed class EnviarTransferencia(
    IRepositorioDeTransferencias transferencias,
    INumeradorDeSeriesDeInventario numerador,
    IConsultaDeEjercicios ejercicios,
    IConsultaDeTrazabilidad trazabilidad,
    IValoracionDeExistencias valoracion,
    IUnidadTrabajoDeInventario unidadTrabajo,
    TimeProvider reloj) : IEnviarTransferencia
{
    /// <inheritdoc/>
    public async Task<Resultado<TransferenciaDto>> EjecutarAsync(
        Guid transferenciaId,
        CancellationToken cancelacion)
    {
        Transferencia? transferencia = await transferencias
            .ObtenerAsync(transferenciaId, cancelacion)
            .ConfigureAwait(false);

        if (transferencia is null)
        {
            return Resultado.Fallo<TransferenciaDto>(ErroresDeTransferencia.NoEncontrada(transferenciaId));
        }

        // El estado, antes de transitar: el dominio lanza, y el borde necesita un 409 (ADR-0004).
        if (transferencia.Estado != EstadoDeTransferencia.Borrador)
        {
            return Resultado.Fallo<TransferenciaDto>(
                ErroresDeTransferencia.NoEstaEnBorrador(transferenciaId, transferencia.Estado));
        }

        DateOnly fecha = transferencia.FechaDeEnvio;

        // NADA CON FECHA FUTURA, antes que el ejercicio porque no toma cerrojos. Es un 422 y no el
        // 409 del ajuste: lo fija la tabla del ADR-0053 §3.
        if (fecha > DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime))
        {
            return Resultado.Fallo<TransferenciaDto>(ErroresDeTransferencia.ConFechaFutura(fecha));
        }

        EstadoDelEjercicioParaEscribir ejercicio = await ejercicios
            .ParaEscribirEnAsync(fecha, cancelacion)
            .ConfigureAwait(false);

        if (ejercicio is not EstadoDelEjercicioParaEscribir.Abierto)
        {
            return Resultado.Fallo<TransferenciaDto>(
                ejercicio is EstadoDelEjercicioParaEscribir.SinEjercicio
                    ? ErroresDeTransferencia.SinEjercicio(fecha)
                    : ErroresDeTransferencia.EnEjercicioCerrado(fecha));
        }

        // LA MARCA, BAJO CERROJO: es la guarda (ADR-0048 §4). Al recibir y al anular ya no se lee,
        // porque este envío mueve el artículo y su marca deja de poder cambiar.
        IReadOnlyDictionary<Guid, MarcaDeTrazabilidad> marcas = await trazabilidad
            .MarcasParaMoverAsync([.. transferencia.Lineas.Select(linea => linea.ArticuloId).Distinct()], cancelacion)
            .ConfigureAwait(false);

        if (LaTrazabilidadDeLasLineas.LoQueNoCasa(LaTrazabilidadDeLasLineas.DelDocumento(transferencia), marcas)
            is { } noCasa)
        {
            return Resultado.Fallo<TransferenciaDto>(ErroresDeTransferencia.TrazabilidadNoCasa(noCasa));
        }

        // EL NÚMERO, EN LA SERIE DEL EJERCICIO DE LA FECHA DE ENVÍO (ADR-0053 §3, ADR-0043).
        Resultado<long> numero = await numerador
            .TomarNumeroAsync(transferencia.SerieId, DocumentoQueNumera.Transferencia, fecha, cancelacion)
            .ConfigureAwait(false);

        if (!numero.EsCorrecto)
        {
            return Resultado.Fallo<TransferenciaDto>(numero.Error!);
        }

        IReadOnlyList<LineaAValorar> lineas = transferencia.LineasAValorarEnElOrigen();
        ClaveDeValoracion[] delDestino =
        [
            .. transferencia.Lineas
                .Select(linea => new ClaveDeValoracion(linea.ArticuloId, transferencia.AlmacenDestinoId))
                .Distinct(),
        ];

        // LAS DOS PUNTAS EN UNA SOLA LLAMADA, que las bloquea en orden de clave (ADR-0053 §10).
        IReadOnlyDictionary<ClaveDeValoracion, SaldoValorado> saldos = await transferencias
            .BloquearLasValoracionesAsync(
                [.. lineas.Select(linea => linea.Clave).Concat(delDestino).Distinct()],
                transferencia.Divisa,
                cancelacion)
            .ConfigureAwait(false);

        // EL DESTINO NO SE VALORA AL ENVIAR, pero recibe lo que vuela: una clave con existencias o
        // con tránsito en otra divisa no puede sumarlo. Vacía, empieza de nuevo en la del documento,
        // como en la valoración.
        foreach (ClaveDeValoracion clave in delDestino)
        {
            SaldoValorado saldo = saldos[clave];

            if (!saldo.EstaVacio && saldo.Valor.Divisa != transferencia.Divisa)
            {
                return Resultado.Fallo<TransferenciaDto>(
                    ErroresDeTransferencia.ValoracionEnOtraDivisa(clave, transferencia.Divisa));
            }
        }

        if (valoracion.LoQueImpide(saldos, lineas, transferencia.Divisa, fecha) is { } impedimento)
        {
            return Resultado.Fallo<TransferenciaDto>(
                ErroresDeTransferencia.NoSeValora(impedimento, transferencia.Divisa));
        }

        IReadOnlyList<LineaValorada> valoradas = valoracion.Valorar(saldos, lineas, transferencia.Divisa, fecha);

        LotesYSeriesResueltos resueltos = await transferencias
            .ResolverLotesYSeriesAsync(transferencia.LotesQueNombra(), transferencia.SeriesQueNombra(), cancelacion)
            .ConfigureAwait(false);

        var evento = new TransferenciaEnviada(
            transferencia.Id,
            transferencia.EmpresaId,
            transferencia.AlmacenOrigenId,
            transferencia.AlmacenDestinoId,
            fecha,
            transferencia.Lineas.Count);

        LoQueMueveLaTransferencia movido =
            transferencia.Enviar(numero.Valor, evento, valoradas, resueltos, reloj.GetUtcNow());

        await transferencias.MoverAsync(movido, cancelacion).ConfigureAwait(false);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(transferencia.ADto());
    }
}
