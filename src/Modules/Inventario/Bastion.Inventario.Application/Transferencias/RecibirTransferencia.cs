using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Transferencias;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Organizacion.Contracts.Ejercicios;

namespace Bastion.Inventario.Application.Transferencias;

/// <summary>Recibe entera una transferencia enviada: sale del tránsito y entra en el destino.</summary>
public interface IRecibirTransferencia
{
    /// <summary>Ejecuta la recepción.</summary>
    /// <param name="transferenciaId">El documento.</param>
    /// <param name="peticion">El día en que llega.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>La transferencia recibida, o el motivo por el que no se recibe.</returns>
    Task<Resultado<TransferenciaDto>> EjecutarAsync(
        Guid transferenciaId,
        RecibirTransferenciaDto peticion,
        CancellationToken cancelacion);
}

/// <summary>
/// La recepción: la entrada en el destino, con el valor que viajó, y el tránsito que se descuenta,
/// en el mismo <c>COMMIT</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Entera</b> (ADR-0053 §4): no pregunta líneas, y una diferencia se regulariza después con un
/// ajuste en el destino.
/// </para>
/// <para>
/// <b>Sin número y sin marca</b> (ADR-0053 §10): el número lo dio el envío, y la marca ya no puede
/// cambiar, porque el envío movió el artículo. Así que los cerrojos son el ejercicio de la fecha de
/// recepción, las valoraciones del destino, los lotes y las series, la fila del documento y las
/// existencias.
/// </para>
/// <para>
/// <b>Y con la valoración ya bloqueada, la versión del documento</b>. Quien pierde una carrera sobre
/// la misma transferencia —otra recepción, o una anulación— espera en esa valoración. Si siguiera
/// sin mirar, valoraría con su fecha contra lo que dejó la ganadora, y una fecha anterior a la suya
/// le daría el <c>422</c> del ADR-0047 en vez del <c>412</c> del §10. Lo encontró la revisión del
/// paso 4 del 2.11.
/// </para>
/// </remarks>
/// <param name="transferencias">Dónde viven el documento y lo que mueve.</param>
/// <param name="ejercicios">Si la fecha de recepción se puede escribir, con la fila bloqueada.</param>
/// <param name="valoracion">Quién valora la entrada contra los saldos bloqueados (ADR-0046 §10).</param>
/// <param name="unidadTrabajo">La transacción.</param>
/// <param name="reloj">De dónde sale «ahora».</param>
internal sealed class RecibirTransferencia(
    IRepositorioDeTransferencias transferencias,
    IConsultaDeEjercicios ejercicios,
    IValoracionDeExistencias valoracion,
    IUnidadTrabajoDeInventario unidadTrabajo,
    TimeProvider reloj) : IRecibirTransferencia
{
    /// <inheritdoc/>
    public async Task<Resultado<TransferenciaDto>> EjecutarAsync(
        Guid transferenciaId,
        RecibirTransferenciaDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        // EL [Required] DEL CUERPO LA GARANTIZA: una petición sin fecha no llega hasta aquí.
        DateOnly fecha = peticion.FechaDeRecepcion ?? throw new ArgumentException(
            "La recepción llega sin fecha, y el borde la exige antes de llamar.", nameof(peticion));

        Transferencia? transferencia = await transferencias
            .ObtenerAsync(transferenciaId, cancelacion)
            .ConfigureAwait(false);

        if (transferencia is null)
        {
            return Resultado.Fallo<TransferenciaDto>(ErroresDeTransferencia.NoEncontrada(transferenciaId));
        }

        if (transferencia.Estado != EstadoDeTransferencia.Enviada)
        {
            return Resultado.Fallo<TransferenciaDto>(
                ErroresDeTransferencia.NoEstaEnviada(transferenciaId, transferencia.Estado));
        }

        // LAS DOS FECHAS, ANTES QUE EL EJERCICIO: ninguna de las dos toma cerrojos (ADR-0053 §3).
        if (fecha > DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime))
        {
            return Resultado.Fallo<TransferenciaDto>(ErroresDeTransferencia.ConFechaFutura(fecha));
        }

        if (fecha < transferencia.FechaDeEnvio)
        {
            return Resultado.Fallo<TransferenciaDto>(
                ErroresDeTransferencia.RecepcionAntesDelEnvio(fecha, transferencia.FechaDeEnvio));
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

        IReadOnlyList<LineaAValorar> lineas = transferencia.LineasAValorarEnElDestino();

        IReadOnlyDictionary<ClaveDeValoracion, SaldoValorado> saldos = await transferencias
            .BloquearLasValoracionesAsync(
                [.. lineas.Select(linea => linea.Clave).Distinct()], transferencia.Divisa, cancelacion)
            .ConfigureAwait(false);

        if (!await transferencias.SigueComoSeLeyoAsync(transferencia, cancelacion).ConfigureAwait(false))
        {
            return Resultado.Fallo<TransferenciaDto>(ErroresDeConcurrencia.ObsoletaYSinRecurso());
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

        var evento = new TransferenciaRecibida(
            transferencia.Id,
            transferencia.EmpresaId,
            transferencia.AlmacenDestinoId,
            fecha,
            transferencia.Lineas.Count);

        LoQueMueveLaTransferencia movido =
            transferencia.Recibir(fecha, evento, valoradas, resueltos, reloj.GetUtcNow());

        await transferencias.MoverAsync(movido, cancelacion).ConfigureAwait(false);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(transferencia.ADto());
    }
}
