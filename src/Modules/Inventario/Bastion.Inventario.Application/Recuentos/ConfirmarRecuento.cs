using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Inventario.Application.Ajustes;
using Bastion.Inventario.Application.Trazabilidad;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Contracts.Recuentos;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Recuentos;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Organizacion.Contracts.Ejercicios;

namespace Bastion.Inventario.Application.Recuentos;

/// <summary>Confirma un recuento en curso, con el ajuste de su diferencia.</summary>
public interface IConfirmarRecuento
{
    /// <summary>Ejecuta la confirmación.</summary>
    /// <param name="recuentoId">El recuento.</param>
    /// <param name="version">La versión de la cabecera que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="peticion">La huella del teórico que vio quien confirma.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>El recuento confirmado, o el motivo por el que no se confirma.</returns>
    Task<Resultado<RecuentoDto>> EjecutarAsync(
        Guid recuentoId,
        VersionDeRecurso version,
        ConfirmarRecuentoDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IConfirmarRecuento"/>
/// <remarks>
/// <para>
/// <b>Los cerrojos van en el orden del ADR-0055 §3</b>, que es el del ajuste con la fila del recuento
/// delante. Cada paso de abajo lleva su número de aquella tabla.
/// </para>
/// <para>
/// <b>El teórico se lee dos veces.</b> La primera, sin cerrojo, solo decide si hace falta el número
/// del ajuste, y para la confirmación pronto si el mundo ya cambió. La segunda, con las valoraciones
/// de todas las claves bloqueadas, es la que decide las cantidades: nada que mueva el físico de una
/// de esas claves puede colarse entre ella y el <c>COMMIT</c>.
/// </para>
/// <para>
/// <b>El <c>412</c> sale de aquí y no del testigo de EF Core</b> (ADR-0057). Se compara la versión con
/// la fila ya bloqueada, así que nadie la puede cambiar después, y el <c>UPDATE</c> de la cabecera no
/// choca nunca. La transacción es la del filtro de idempotencia, y un choque al guardar la dejaría a
/// medio deshacer con el trabajo entero hecho.
/// </para>
/// <para>
/// <b>El ajuste se confirma en la misma transacción</b> (ADR-0055 §8): no queda ningún borrador suelto,
/// y si algo falla después de numerarlo, se deshace con los dos números.
/// </para>
/// </remarks>
/// <param name="recuentos">El documento y las existencias de su almacén.</param>
/// <param name="ajustes">Dónde viven el ajuste, las valoraciones y el libro.</param>
/// <param name="numerador">Quién entrega los dos correlativos, en esta misma transacción (R5).</param>
/// <param name="ejercicios">Si hoy se puede escribir, con la fila del ejercicio bloqueada.</param>
/// <param name="trazabilidad">La marca de los artículos, con su fila bloqueada (ADR-0048 §4).</param>
/// <param name="valoracion">Quién valora las líneas del ajuste contra los saldos bloqueados.</param>
/// <param name="versiones">La versión de la cabecera, que se compara con la del <c>If-Match</c>.</param>
/// <param name="unidadTrabajo">La transacción.</param>
/// <param name="reloj">De dónde sale «hoy», que es la fecha de la confirmación y la del ajuste.</param>
internal sealed class ConfirmarRecuento(
    IRepositorioDeRecuentos recuentos,
    IRepositorioDeAjustes ajustes,
    INumeradorDeSeriesDeInventario numerador,
    IConsultaDeEjercicios ejercicios,
    IConsultaDeTrazabilidad trazabilidad,
    IValoracionDeExistencias valoracion,
    IVersionesDeInventario versiones,
    IUnidadTrabajoDeInventario unidadTrabajo,
    TimeProvider reloj) : IConfirmarRecuento
{
    /// <inheritdoc/>
    public async Task<Resultado<RecuentoDto>> EjecutarAsync(
        Guid recuentoId,
        VersionDeRecurso version,
        ConfirmarRecuentoDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        // 1 y 2. LA FILA, BLOQUEADA ANTES DE LEERLA, Y SU VERSIÓN ANTES QUE SU ESTADO. Dos
        // confirmaciones del mismo recuento: la segunda espera aquí, lee la versión que dejó la
        // primera y recibe el 412, que es su causa. Mirar antes el estado le daría un 409 por algo
        // que su If-Match ya había dicho.
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

        // 3. UNA LÍNEA SIN CONTAR NO ES UN CERO (ADR-0055 §5), y se mira antes que nada que cueste:
        // no hace falta saber nada del almacén para saber que falta contar.
        if (await LasQueFaltanPorContarAsync(recuento, cancelacion).ConfigureAwait(false) is { } sinContar)
        {
            return Resultado.Fallo<RecuentoDto>(sinContar);
        }

        DateTimeOffset ahora = reloj.GetUtcNow();
        var hoy = DateOnly.FromDateTime(ahora.UtcDateTime);

        // 4. EL EJERCICIO DE HOY, que es la fecha de la confirmación y la del ajuste (ADR-0055 §1.5).
        // El cerrojo compartido dura hasta el COMMIT: un cierre que llegue ahora espera a este
        // recuento, y uno que ya terminó deja esto contestando «cerrado».
        EstadoDelEjercicioParaEscribir ejercicio = await ejercicios
            .ParaEscribirEnAsync(hoy, cancelacion)
            .ConfigureAwait(false);

        if (ejercicio is not EstadoDelEjercicioParaEscribir.Abierto)
        {
            return Resultado.Fallo<RecuentoDto>(
                ejercicio is EstadoDelEjercicioParaEscribir.SinEjercicio
                    ? ErroresDeRecuento.SinEjercicio(hoy)
                    : ErroresDeRecuento.EnEjercicioCerrado(hoy));
        }

        // 5. LA MARCA DE TODOS SUS ARTÍCULOS, compartida hasta el COMMIT (ADR-0048 §4). De todos y no
        // solo de los que difieren, porque cuáles difieren no se sabe hasta el paso 11.
        Guid[] articulos = [.. recuento.Lineas.Select(linea => linea.ArticuloId).Distinct()];

        IReadOnlyDictionary<Guid, MarcaDeTrazabilidad> marcas = await trazabilidad
            .MarcasParaMoverAsync(articulos, cancelacion)
            .ConfigureAwait(false);

        // 6. EL TEÓRICO SIN CERROJO. No decide cantidades: decide si hace falta el número del ajuste,
        // y para aquí, sin gastar nada, si el mundo ya no es el que vio quien confirma.
        ElTeoricoDeLasLineas sinCerrojo = await ElTeoricoDeAhoraAsync(recuento, cancelacion).ConfigureAwait(false);

        if (ElTeoricoHaCambiado(recuento, sinCerrojo, peticion.HuellaDelTeorico) is { } antes)
        {
            return Resultado.Fallo<RecuentoDto>(antes);
        }

        // 7. LA FORMA DE LAS LÍNEAS QUE SE MOVERÍAN, contra la marca que se acaba de bloquear. Las que
        // cuadran no van al ajuste, y lo que no se mueve no tiene que casar con nada.
        Ajuste? borrador = recuento.AjusteDeLaDiferencia(sinCerrojo.DeAhora, hoy, ahora);

        if (borrador is not null
            && LaTrazabilidadDeLasLineas.LoQueNoCasa(LaTrazabilidadDeLasLineas.DelDocumento(borrador), marcas)
                is { } noCasa)
        {
            return Resultado.Fallo<RecuentoDto>(ErroresDeRecuento.TrazabilidadNoCasa(noCasa));
        }

        // 8. EL NÚMERO DEL RECUENTO, con la fecha de hoy, que es la que tiene que caer en el ejercicio
        // de su serie. La serie se eligió en el alta, y la guarda de verdad es esta (ADR-0055 §1.3).
        Resultado<long> numero = await numerador
            .TomarNumeroAsync(recuento.SerieId, DocumentoQueNumera.Recuento, hoy, cancelacion)
            .ConfigureAwait(false);

        if (!numero.EsCorrecto)
        {
            return Resultado.Fallo<RecuentoDto>(numero.Error!);
        }

        // 9. EL DEL AJUSTE, SOLO SI HAY DIFERENCIAS, y antes que las valoraciones, como en cualquier
        // ajuste: al revés, un recuento y un ajuste de la misma serie se esperarían cada uno con lo
        // que el otro necesita. Si no hay ajuste, su serie no se consulta (ADR-0055 §13).
        long? numeroDelAjuste = null;

        if (borrador is not null)
        {
            Resultado<long> delAjuste = await numerador
                .TomarNumeroAsync(recuento.SerieDelAjusteId, DocumentoQueNumera.Ajuste, hoy, cancelacion)
                .ConfigureAwait(false);

            if (!delAjuste.EsCorrecto)
            {
                return Resultado.Fallo<RecuentoDto>(delAjuste.Error!);
            }

            numeroDelAjuste = delAjuste.Valor;
        }

        // 10. LAS VALORACIONES DE TODAS SUS CLAVES, en orden de clave, y aunque no haya ajuste. Todo lo
        // que mueve el físico de un artículo en un almacén pasa por la valoración de ese par, así que
        // con todas bloqueadas el teórico de abajo no cambia hasta el COMMIT.
        IReadOnlyDictionary<ClaveDeValoracion, SaldoValorado> saldos = await ajustes
            .BloquearLasValoracionesAsync(
                [.. articulos.Select(articulo => new ClaveDeValoracion(articulo, recuento.AlmacenId))],
                recuento.Divisa,
                cancelacion)
            .ConfigureAwait(false);

        // 11. EL TEÓRICO QUE DECIDE, con las valoraciones bloqueadas. Si ya no es el que vio quien
        // confirma, la transacción se deshace con sus dos números.
        ElTeoricoDeLasLineas conCerrojo = await ElTeoricoDeAhoraAsync(recuento, cancelacion).ConfigureAwait(false);

        if (ElTeoricoHaCambiado(recuento, conCerrojo, peticion.HuellaDelTeorico) is { } despues)
        {
            return Resultado.Fallo<RecuentoDto>(despues);
        }

        // 12. LO QUE SUBE NO PUEDE TENER TRÁNSITO HACIA SU CLAVE (ADR-0055 §7): si la mercancía ya
        // llegó y no se ha recibido, contarla y recibirla la sumaría dos veces.
        if (LoQueSubeConTransito(recuento, conCerrojo) is { } conTransito)
        {
            return Resultado.Fallo<RecuentoDto>(conTransito);
        }

        Ajuste? ajuste = recuento.AjusteDeLaDiferencia(conCerrojo.DeAhora, hoy, ahora);

        // LA MISMA HUELLA ES EL MISMO TEÓRICO, así que el ajuste de ahora es el del borrador, y el
        // número del paso 9 es justo el que hace falta. Si no lo fuera, lo que está roto es la
        // huella, y eso no se contesta: se denuncia.
        if ((ajuste is null) != (numeroDelAjuste is null))
        {
            throw new InvalidOperationException(
                $"El recuento {recuentoId} tenía la misma huella en los dos teóricos y uno pide ajuste y " +
                "el otro no: la huella no está distinguiendo dos teóricos distintos.");
        }

        if (ajuste is not null)
        {
            // 13 y 14. EL VALOR, LOS LOTES Y LAS SERIES, Y EL LIBRO, como en cualquier ajuste.
            if (await ConfirmarElAjusteAsync(ajuste, numeroDelAjuste!.Value, saldos, ahora, cancelacion)
                    .ConfigureAwait(false) is { } impedimento)
            {
                return Resultado.Fallo<RecuentoDto>(impedimento);
            }
        }

        recuento.Confirmar(
            numero.Valor,
            hoy,
            conCerrojo.DeAhora,
            ajuste,
            new RecuentoConfirmado(
                recuento.Id, recuento.EmpresaId, recuento.AlmacenId, hoy, recuento.Lineas.Count, ajuste?.Id));

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(recuento.ADto(ElTeoricoDeLasLineas.AlConfirmar(recuento, ajuste)));
    }

    /// <summary>El <c>422</c> de las líneas sin contar, con las primeras y su teórico, o nada.</summary>
    private async Task<ErrorDeOperacion?> LasQueFaltanPorContarAsync(
        Recuento recuento,
        CancellationToken cancelacion)
    {
        IReadOnlyList<LineaDeRecuento> sinContar = recuento.LineasSinContar();

        if (sinContar.Count == 0)
        {
            return null;
        }

        List<LineaDeRecuento> primeras = [.. sinContar.Take(ErroresDeRecuento.LineasEnElConflicto)];

        // EL TEÓRICO SOLO DE LAS QUE SE ENSEÑAN, que es lo que la pantalla necesita para contarlas:
        // leer el almacén entero para un rechazo sería pagar la confirmación sin hacerla.
        ElTeoricoDeLasLineas suyo = await ElTeoricoDeLasLineas
            .LeerAsync(recuento, recuentos, [.. primeras.Select(linea => linea.ArticuloId).Distinct()], cancelacion)
            .ConfigureAwait(false);

        return ErroresDeRecuento.ConLineasSinContar(sinContar.Count)
            .ConElEstadoActual(new LineasEnConflictoDto(
                sinContar.Count, [.. primeras.Select(linea => linea.ADto(suyo))], HuellaDelTeorico: null));
    }

    private async Task<ElTeoricoDeLasLineas> ElTeoricoDeAhoraAsync(
        Recuento recuento,
        CancellationToken cancelacion) =>
        ElTeoricoDeLasLineas.Ahora(
            recuento,
            await recuentos.ExistenciasAsync(recuento.AlmacenId, articulos: null, cancelacion).ConfigureAwait(false));

    /// <summary>El <c>409</c> del teórico, con la huella de ahora y las líneas que han cambiado, o nada.</summary>
    /// <remarks>
    /// <b>Las líneas que van en <c>actual</c> son las de teórico cambiado desde que se contaron</b>
    /// (ADR-0055 §2). Lo que vio quien confirma solo lo dice su huella, así que no se sabe qué línea
    /// cambió desde que la vio: se dice cuál cambió desde que se contó, que es lo que la pantalla marca.
    /// </remarks>
    private static ErrorDeOperacion? ElTeoricoHaCambiado(
        Recuento recuento,
        ElTeoricoDeLasLineas teorico,
        string vista)
    {
        string deAhora = recuento.HuellaDelTeorico(teorico.DeAhora);

        if (string.Equals(deAhora, vista, StringComparison.Ordinal))
        {
            return null;
        }

        IReadOnlyList<LineaDeRecuento> cambiadas = recuento.LineasConElTeoricoCambiado(teorico.DeAhora);

        return ErroresDeRecuento.TeoricoCambiado()
            .ConElEstadoActual(new LineasEnConflictoDto(
                cambiadas.Count,
                [.. cambiadas.Take(ErroresDeRecuento.LineasEnElConflicto).Select(linea => linea.ADto(teorico))],
                deAhora));
    }

    /// <summary>El <c>409</c> de las líneas que suben con tránsito hacia su clave, o nada.</summary>
    private static ErrorDeOperacion? LoQueSubeConTransito(Recuento recuento, ElTeoricoDeLasLineas teorico)
    {
        List<LineaDeRecuento> conTransito =
        [
            .. recuento.LineasQueSuben(teorico.DeAhora).Where(linea => teorico.TransitoDe(linea) > 0m),
        ];

        return conTransito.Count == 0
            ? null
            : ErroresDeRecuento.SubeConTransito()
                .ConElEstadoActual(new LineasEnConflictoDto(
                    conTransito.Count,
                    [.. conTransito.Take(ErroresDeRecuento.LineasEnElConflicto).Select(linea => linea.ADto(teorico))],
                    HuellaDelTeorico: null));
    }

    /// <summary>Valora, resuelve y confirma el ajuste, y escribe su libro; o dice por qué no se valora.</summary>
    private async Task<ErrorDeOperacion?> ConfirmarElAjusteAsync(
        Ajuste ajuste,
        long numeroDelAjuste,
        IReadOnlyDictionary<ClaveDeValoracion, SaldoValorado> saldos,
        DateTimeOffset ahora,
        CancellationToken cancelacion)
    {
        // 13. EL VALOR. El impedimento se pregunta antes de valorar, porque el dominio lanza, y el que
        // contesta es el del ajuste (ADR-0055 §6): una clave añadida que sube sin coste y sin precio
        // medio es `ajuste-entrada-sin-coste-ni-precio-medio`.
        IReadOnlyList<LineaAValorar> lineas = ajuste.LineasAValorar();

        if (valoracion.LoQueImpide(saldos, lineas, ajuste.Divisa, ajuste.FechaDeOperacion) is { } impedimento)
        {
            return ErroresDeAjuste.NoSeValora(impedimento, ajuste.Divisa);
        }

        IReadOnlyList<LineaValorada> valoradas =
            valoracion.Valorar(saldos, lineas, ajuste.Divisa, ajuste.FechaDeOperacion);

        // 14. LOS LOTES Y LAS SERIES, EL DOCUMENTO Y EL LIBRO.
        LotesYSeriesResueltos resueltos = await ajustes
            .ResolverLotesYSeriesAsync(ajuste.LotesQueNombra(), ajuste.SeriesQueNombra(), cancelacion)
            .ConfigureAwait(false);

        // EL EVENTO DEL AJUSTE ES EL QUE LLEGA AL ASIENTO (ADR-0055 §1): el de cualquier otro ajuste.
        var confirmado = new AjusteConfirmado(
            ajuste.Id, ajuste.EmpresaId, ajuste.AlmacenId, ajuste.FechaDeOperacion, ajuste.Lineas.Count);

        IReadOnlyList<MovimientoStock> movimientos =
            ajuste.Confirmar(numeroDelAjuste, confirmado, valoradas, resueltos, ahora);

        ajustes.Agregar(ajuste);
        await ajustes.AnotarEnElLibroAsync(movimientos, cancelacion).ConfigureAwait(false);

        return null;
    }
}
