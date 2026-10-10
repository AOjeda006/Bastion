using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Inventario.Application.Trazabilidad;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Reservas;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Organizacion.Contracts.Ejercicios;
using Bastion.Organizacion.Contracts.Empresas;

namespace Bastion.Inventario.Application.Reservas;

/// <summary>Saca por el libro una parte o todo lo que aparta una reserva, con un albarán.</summary>
public interface IConsumirReserva
{
    /// <summary>Ejecuta el consumo.</summary>
    /// <param name="peticion">De qué reserva, con qué documento, qué día y de qué huecos.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>La reserva tras el consumo, o el motivo por el que no sale nada.</returns>
    Task<Resultado<ReservaDto>> EjecutarAsync(ConsumirReservaDto peticion, CancellationToken cancelacion);
}

/// <summary>
/// Consumir, en el orden del ADR-0059 §2 y §6: la salida y el consumo en el mismo <c>COMMIT</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es una salida del ajuste sin contador</b>, porque el albarán lo numera Ventas: el ejercicio de
/// la fecha, la marca del artículo, la valoración de la clave como cualquier salida, las caducadas,
/// la reserva, el físico de cada hueco, el valor, y la escritura.
/// </para>
/// <para>
/// <b>Dobla la R12</b>: la reserva, su consumo, las filas del libro, las existencias y la valoración
/// caen juntos. Con dos <c>COMMIT</c>, lo que sale contaría un instante dos veces, en el físico y en
/// lo reservado.
/// </para>
/// <para>
/// <b>El físico de cada hueco se mira antes de escribir</b>: la unidad de trabajo confirma aunque el
/// caso diga que no, y la restricción de las existencias solo se traduce a <c>422</c> en el borde
/// HTTP, que aquí no hay. Sin esta pregunta, el rechazo llegaría como una excepción de la base.
/// </para>
/// </remarks>
/// <param name="usuarioActual">De dónde sale la empresa (R8).</param>
/// <param name="reservas">El cerrojo, la reserva, el físico y la escritura.</param>
/// <param name="empresas">La divisa base de la empresa, la de las filas del libro.</param>
/// <param name="ejercicios">Si la fecha se puede escribir, con la fila bloqueada.</param>
/// <param name="trazabilidad">La marca del artículo, con la fila bloqueada (ADR-0048 §4).</param>
/// <param name="valoracion">Quién valora la salida contra el saldo bloqueado (ADR-0046 §10).</param>
/// <param name="unidadTrabajo">La transacción.</param>
/// <param name="reloj">De dónde sale «ahora».</param>
internal sealed class ConsumirReserva(
    IUsuarioActual usuarioActual,
    IRepositorioDeReservas reservas,
    IConsultaDeEmpresas empresas,
    IConsultaDeEjercicios ejercicios,
    IConsultaDeTrazabilidad trazabilidad,
    IValoracionDeExistencias valoracion,
    IUnidadTrabajoDeInventario unidadTrabajo,
    TimeProvider reloj) : IConsumirReserva
{
    /// <inheritdoc/>
    public Task<Resultado<ReservaDto>> EjecutarAsync(ConsumirReservaDto peticion, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        return unidadTrabajo.EnTransaccionAsync(enCurso => ConsumirAsync(peticion, enCurso), cancelacion);
    }

    private async Task<Resultado<ReservaDto>> ConsumirAsync(
        ConsumirReservaDto peticion, CancellationToken cancelacion)
    {
        DateTimeOffset ahora = reloj.GetUtcNow();

        if (LaPeticion(peticion) is { } malFormada)
        {
            return Resultado.Fallo<ReservaDto>(malFormada);
        }

        OrigenDeLaReserva origen = peticion.Origen;
        DateOnly fecha = peticion.FechaDeOperacion;

        // NADA CON FECHA FUTURA, antes que el ejercicio porque no toma cerrojos: un 422, como la
        // transferencia (ADR-0059 §6).
        if (fecha > DateOnly.FromDateTime(ahora.UtcDateTime))
        {
            return Resultado.Fallo<ReservaDto>(ErroresDeReserva.ConFechaFutura(fecha));
        }

        // LA CLAVE, SIN CERROJO: hace falta para saber qué bloquear. La reserva se vuelve a leer
        // después del cerrojo, que es la lectura que decide.
        ClaveDeValoracion? deLaReserva = await reservas
            .ClaveDelOrigenAsync(origen.Tipo, origen.Id, origen.Linea, cancelacion)
            .ConfigureAwait(false);

        if (deLaReserva is not { } clave)
        {
            return Resultado.Fallo<ReservaDto>(ErroresDeReserva.NoEncontrada(origen));
        }

        string? divisa = await empresas
            .DivisaBaseDeAsync(usuarioActual.EmpresaId, cancelacion)
            .ConfigureAwait(false);

        if (divisa is null)
        {
            return Resultado.Fallo<ReservaDto>(ErroresDeInquilinato.EmpresaActivaNoOperativa());
        }

        // 1. EL EJERCICIO DE LA FECHA, en compartido.
        EstadoDelEjercicioParaEscribir ejercicio = await ejercicios
            .ParaEscribirEnAsync(fecha, cancelacion)
            .ConfigureAwait(false);

        if (ejercicio is not EstadoDelEjercicioParaEscribir.Abierto)
        {
            return Resultado.Fallo<ReservaDto>(
                ejercicio is EstadoDelEjercicioParaEscribir.SinEjercicio
                    ? ErroresDeReserva.SinEjercicio(fecha)
                    : ErroresDeReserva.EnEjercicioCerrado(fecha));
        }

        // 2. LA MARCA DEL ARTÍCULO, en compartido: es la guarda (ADR-0048 §4).
        IReadOnlyDictionary<Guid, MarcaDeTrazabilidad> marcas = await trazabilidad
            .MarcasParaMoverAsync([clave.ArticuloId], cancelacion)
            .ConfigureAwait(false);

        IEnumerable<LineaConCodigos> conCodigos = peticion.Lineas.Select((linea, indice) =>
            new LineaConCodigos(indice + 1, clave.ArticuloId, linea.CodigoDeLote, linea.NumeroDeSerie));

        if (LaTrazabilidadDeLasLineas.LoQueNoCasa(conCodigos, marcas) is { } noCasa)
        {
            return Resultado.Fallo<ReservaDto>(ErroresDeReserva.TrazabilidadNoCasa(noCasa));
        }

        // 3. LA VALORACIÓN DE LA CLAVE, como cualquier salida: bloqueada y leída.
        IReadOnlyDictionary<ClaveDeValoracion, SaldoValorado> saldos = await reservas
            .BloquearLasValoracionesAsync([clave], divisa, cancelacion)
            .ConfigureAwait(false);

        Reserva reserva = await reservas
            .ObtenerPorOrigenAsync(origen.Tipo, origen.Id, origen.Linea, cancelacion)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"La reserva del origen {origen} estaba antes del cerrojo y no está después: una " +
                "reserva no se borra, así que la lectura no ha visto lo que debía.");

        // 4. LAS CADUCADAS DE LA CLAVE, entre ellas esta si caducó: se escriben si el consumo sale.
        await LasCaducadasDeLaClave.LiberarAsync(reservas, clave, ahora, cancelacion).ConfigureAwait(false);

        // 5. LA RESERVA: sin este documento, activa ahora y con bastante pendiente. El documento va
        // antes que el estado: el reintento del albarán que la dejó consumida tiene que oír que ese
        // albarán ya salió, y no que la reserva no está activa (ADR-0059 §6).
        if (reserva.Consumos.Any(consumo =>
            consumo.DocumentoTipo == peticion.DocumentoTipo && consumo.DocumentoId == peticion.DocumentoId))
        {
            return Resultado.Fallo<ReservaDto>(ErroresDeReserva.DocumentoYaLaConsumio(reserva.Id, peticion.DocumentoId));
        }

        if (LoQueSeLePideALaReserva.SiNoEstaActiva(reserva, ahora) is { } noEstaActiva)
        {
            return Resultado.Fallo<ReservaDto>(noEstaActiva);
        }

        decimal sale = peticion.Lineas.Sum(linea => linea.Cantidad);

        if (sale > reserva.Pendiente)
        {
            return Resultado.Fallo<ReservaDto>(
                ErroresDeReserva.ConsumoPorEncimaDeLoPendiente(reserva.Id, sale, reserva.Pendiente));
        }

        // 6. EL FÍSICO DE CADA HUECO, buscando los lotes y las series sin crearlos.
        LotesYSeriesResueltos resueltos = await reservas
            .BuscarLotesYSeriesAsync(
                [.. CodigosDe(peticion.Lineas, clave.ArticuloId, linea => linea.CodigoDeLote)],
                [.. CodigosDe(peticion.Lineas, clave.ArticuloId, linea => linea.NumeroDeSerie)],
                cancelacion)
            .ConfigureAwait(false);

        if (await LaLineaSinStockAsync(peticion.Lineas, clave, resueltos, cancelacion).ConfigureAwait(false)
            is { } sinStock)
        {
            return Resultado.Fallo<ReservaDto>(sinStock);
        }

        // 7. EL VALOR, contra el saldo bloqueado.
        LineaDeConsumo[] lineas =
        [
            .. peticion.Lineas.Select(linea =>
                new LineaDeConsumo(linea.UbicacionId, linea.Cantidad, linea.CodigoDeLote, linea.NumeroDeSerie)),
        ];

        IReadOnlyList<LineaAValorar> aValorar = reserva.LineasAValorar(lineas);

        if (valoracion.LoQueImpide(saldos, aValorar, divisa, fecha) is { } impedimento)
        {
            return Resultado.Fallo<ReservaDto>(ErroresDeReserva.NoSeValora(impedimento, divisa));
        }

        IReadOnlyList<LineaValorada> valoradas = valoracion.Valorar(saldos, aValorar, divisa, fecha);

        // 8. LAS EXISTENCIAS, LA VALORACIÓN Y EL LIBRO, con la reserva y su consumo, en un COMMIT.
        IReadOnlyList<MovimientoStock> filas = reserva.Consumir(
            peticion.DocumentoTipo,
            peticion.DocumentoId,
            fecha,
            lineas,
            valoradas,
            resueltos,
            divisa,
            ahora);

        await reservas.AnotarEnElLibroAsync([.. filas], cancelacion).ConfigureAwait(false);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(reserva.ADto(ahora));
    }

    /// <summary>Lo que el dominio lanzaría con esta petición, contestado como un <c>400</c>.</summary>
    /// <param name="peticion">La petición.</param>
    /// <returns>El error, o <see langword="null"/> si tiene forma.</returns>
    private static ErrorDeOperacion? LaPeticion(ConsumirReservaDto peticion)
    {
        if (!LoQueSeLePideALaReserva.EsUnOrigen(peticion.Origen))
        {
            return ErroresDeReserva.OrigenNoValido();
        }

        if (peticion.DocumentoTipo != TipoDeDocumentoOrigen.Albaran || peticion.DocumentoId == Guid.Empty)
        {
            return ErroresDeReserva.DocumentoNoValido(peticion.DocumentoTipo);
        }

        if (peticion.Lineas is null || peticion.Lineas.Count == 0)
        {
            return ErroresDeReserva.SinLineas();
        }

        for (int indice = 0; indice < peticion.Lineas.Count; indice++)
        {
            if (peticion.Lineas[indice] is not { } linea || !Reserva.EsUnaCantidadValida(linea.Cantidad))
            {
                return ErroresDeReserva.CantidadNoValida(indice + 1);
            }
        }

        // EL ARTÍCULO ES EL MISMO EN TODAS: el de la reserva, que todavía no se ha leído. La forma
        // solo lo usa para ver una serie repetida, así que basta con que sea el mismo.
        IEnumerable<LineaConForma> conForma = peticion.Lineas.Select((linea, indice) => new LineaConForma(
            indice + 1, Guid.Empty, linea.Cantidad, 1m, linea.CodigoDeLote, linea.NumeroDeSerie));

        return LaTrazabilidadDeLasLineas.LoQueNoTieneForma(conForma) is { } sinForma
            ? ErroresDeReserva.DeLaForma(sinForma)
            : null;
    }

    /// <summary>Los códigos de lote o de serie que nombran las líneas, ya normalizados.</summary>
    /// <param name="lineas">Las líneas, con la forma ya comprobada.</param>
    /// <param name="articuloId">El artículo de la reserva.</param>
    /// <param name="codigo">Cuál de los dos códigos.</param>
    /// <returns>Los códigos, sin repetir.</returns>
    private static IEnumerable<CodigoDeUnArticulo> CodigosDe(
        IReadOnlyList<LineaDeConsumoDto> lineas, Guid articuloId, Func<LineaDeConsumoDto, string?> codigo) =>
        lineas
            .Select(codigo)
            .OfType<string>()
            .Select(texto => new CodigoDeUnArticulo(articuloId, CodigoGs1.Normalizar(texto)!))
            .Distinct();

    /// <summary>
    /// La primera línea que saca de su hueco más de lo que hay, sumando las que sacan del mismo
    /// hueco, con el mismo lote o la misma serie (ADR-0059 §6).
    /// </summary>
    /// <remarks>
    /// <b>Un lote o una serie que no existen no tienen nada que sacar</b>: su hueco no se pregunta, y
    /// su físico es cero.
    /// </remarks>
    /// <param name="lineas">Las líneas de la petición.</param>
    /// <param name="clave">La clave, ya bloqueada.</param>
    /// <param name="resueltos">Los lotes y las series que existen.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>El error de la primera que no cabe, o <see langword="null"/>.</returns>
    private async Task<ErrorDeOperacion?> LaLineaSinStockAsync(
        IReadOnlyList<LineaDeConsumoDto> lineas,
        ClaveDeValoracion clave,
        LotesYSeriesResueltos resueltos,
        CancellationToken cancelacion)
    {
        var porHueco = new Dictionary<HuecoDeExistencias, (int PrimeraLinea, decimal Sale)>();

        for (int indice = 0; indice < lineas.Count; indice++)
        {
            LineaDeConsumoDto linea = lineas[indice];
            Guid? loteId = null;
            Guid? serieId = null;

            if (linea.CodigoDeLote is { } lote)
            {
                if (!resueltos.Lotes.TryGetValue(new(clave.ArticuloId, CodigoGs1.Normalizar(lote)!), out Guid fila))
                {
                    return ErroresDeReserva.ConsumoSinStock(indice + 1, linea.UbicacionId, 0m);
                }

                loteId = fila;
            }

            if (linea.NumeroDeSerie is { } serie)
            {
                if (!resueltos.Series.TryGetValue(new(clave.ArticuloId, CodigoGs1.Normalizar(serie)!), out Guid fila))
                {
                    return ErroresDeReserva.ConsumoSinStock(indice + 1, linea.UbicacionId, 0m);
                }

                serieId = fila;
            }

            var hueco = new HuecoDeExistencias(linea.UbicacionId, loteId, serieId);

            porHueco[hueco] = porHueco.TryGetValue(hueco, out (int PrimeraLinea, decimal Sale) suma)
                ? (suma.PrimeraLinea, suma.Sale + linea.Cantidad)
                : (indice + 1, linea.Cantidad);
        }

        IReadOnlyDictionary<HuecoDeExistencias, decimal> fisicos = await reservas
            .FisicoDeLosHuecosAsync(clave, porHueco.Keys, cancelacion)
            .ConfigureAwait(false);

        foreach ((HuecoDeExistencias hueco, (int primeraLinea, decimal sale)) in porHueco.OrderBy(par => par.Value.PrimeraLinea))
        {
            decimal fisico = fisicos.GetValueOrDefault(hueco);

            if (sale > fisico)
            {
                return ErroresDeReserva.ConsumoSinStock(primeraLinea, hueco.UbicacionId, fisico);
            }
        }

        return null;
    }
}
