using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Reservas;
using Bastion.Inventario.Domain.Valoraciones;
using Shouldly;

namespace Bastion.Inventario.UnitTests.Reservas;

/// <summary>
/// Consumir escribe su salida (ADR-0059 §6): una fila del libro por línea, del albarán, en la unidad
/// base de la reserva y al precio medio, y un consumo que baja lo pendiente en la misma
/// transacción. Se puede consumir una parte.
/// </summary>
/// <remarks>
/// <b>Cada prohibición deja la reserva como estaba</b>, y su pareja sale bien: lo que el caso de uso
/// mira antes y traduce a un rechazo, el dominio lo vuelve a mirar y lanza, sin haber escrito nada.
/// </remarks>
public sealed class ElConsumoDeLaReservaTests
{
    /// <summary>Consumir una parte la deja activa con lo que queda, y anota el consumo del albarán.</summary>
    [Fact]
    public void Consumir_una_parte_la_deja_activa_con_lo_que_queda()
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m);

        LaReservaDeLaPrueba.Consumir(
            reserva, LaReservaDeLaPrueba.Albaran, LaReservaDeLaPrueba.Momento, LaReservaDeLaPrueba.UnaLinea(4m));

        reserva.EstadoEn(LaReservaDeLaPrueba.Momento).ShouldBe(EstadoDeReserva.Activa);
        reserva.Consumida.ShouldBe(4m);
        reserva.Pendiente.ShouldBe(6m);
        reserva.ApartaEn(LaReservaDeLaPrueba.Momento).ShouldBe(6m);

        ConsumoDeReserva consumo = reserva.Consumos.ShouldHaveSingleItem();
        consumo.ReservaId.ShouldBe(reserva.Id);
        consumo.DocumentoTipo.ShouldBe(TipoDeDocumentoOrigen.Albaran);
        consumo.DocumentoId.ShouldBe(LaReservaDeLaPrueba.Albaran);
        consumo.FechaDeOperacion.ShouldBe(LaReservaDeLaPrueba.Hoy);
        consumo.Cantidad.ShouldBe(4m);
        consumo.CreadoEn.ShouldBe(LaReservaDeLaPrueba.Momento);
    }

    /// <summary>Consumir lo que queda la deja consumida, y ya no aparta nada.</summary>
    [Fact]
    public void Consumir_lo_que_queda_la_deja_consumida()
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m);
        LaReservaDeLaPrueba.Consumir(
            reserva, LaReservaDeLaPrueba.Albaran, LaReservaDeLaPrueba.Momento, LaReservaDeLaPrueba.UnaLinea(4m));

        LaReservaDeLaPrueba.Consumir(
            reserva, LaReservaDeLaPrueba.OtroAlbaran, LaReservaDeLaPrueba.Momento, LaReservaDeLaPrueba.UnaLinea(6m));

        reserva.EstadoEn(LaReservaDeLaPrueba.Momento).ShouldBe(EstadoDeReserva.Consumida);
        reserva.Consumida.ShouldBe(10m);
        reserva.Pendiente.ShouldBe(0m);
        reserva.ApartaEn(LaReservaDeLaPrueba.Momento).ShouldBe(0m);
        reserva.Consumos.Select(consumo => consumo.DocumentoId).ShouldBe(
            [LaReservaDeLaPrueba.Albaran, LaReservaDeLaPrueba.OtroAlbaran]);
    }

    /// <summary>
    /// Cada línea es una salida del albarán: en la unidad base de la reserva con factor uno, con el
    /// valor que trae su valoración y sin coste, que es lo de una salida (ADR-0046 §7).
    /// </summary>
    [Fact]
    public void Cada_linea_es_una_salida_del_albaran_en_la_unidad_base()
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m);

        IReadOnlyList<MovimientoStock> filas = LaReservaDeLaPrueba.Consumir(
            reserva,
            LaReservaDeLaPrueba.Albaran,
            LaReservaDeLaPrueba.Momento,
            LaReservaDeLaPrueba.UnaLinea(3m),
            new LineaDeConsumo(LaReservaDeLaPrueba.OtraUbicacion, 2m, null, null));

        filas.Select(fila => fila.UbicacionId).ShouldBe(
            [LaReservaDeLaPrueba.Ubicacion, LaReservaDeLaPrueba.OtraUbicacion]);
        filas.Select(fila => fila.CantidadEnUnidadBase).ShouldBe([-3m, -2m]);
        filas.Select(fila => fila.Valor.Cantidad).ShouldBe([-7.5m, -5m]);
        reserva.Consumos.ShouldHaveSingleItem().Cantidad.ShouldBe(5m, "un consumo por albarán, con lo que suman sus líneas");

        foreach (MovimientoStock fila in filas)
        {
            fila.EmpresaId.ShouldBe(LaReservaDeLaPrueba.Empresa);
            fila.AlmacenId.ShouldBe(LaReservaDeLaPrueba.Almacen);
            fila.ArticuloId.ShouldBe(LaReservaDeLaPrueba.Articulo);
            fila.FechaDeOperacion.ShouldBe(LaReservaDeLaPrueba.Hoy);
            fila.CantidadIntroducida.ShouldBe(fila.CantidadEnUnidadBase);
            fila.UnidadIntroducidaId.ShouldBe(LaReservaDeLaPrueba.Unidad);
            fila.FactorAUnidadBase.ShouldBe(1m);
            fila.CosteUnitario.ShouldBeNull();
            fila.PrecioMedio.Cantidad.ShouldBe(LaReservaDeLaPrueba.PrecioMedio);
            fila.DocumentoOrigenTipo.ShouldBe(TipoDeDocumentoOrigen.Albaran);
            fila.DocumentoOrigenId.ShouldBe(LaReservaDeLaPrueba.Albaran);
            fila.LoteId.ShouldBeNull();
            fila.NumeroDeSerieId.ShouldBeNull();
        }
    }

    /// <summary>
    /// Lo que se valora es la clave de la reserva con lo que sale cada línea, negativo: es una salida,
    /// y se valora al precio medio (ADR-0046 §7).
    /// </summary>
    [Fact]
    public void Se_valora_la_clave_de_la_reserva_con_lo_que_sale()
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m);

        IReadOnlyList<LineaAValorar> aValorar = reserva.LineasAValorar(
            [LaReservaDeLaPrueba.UnaLinea(3m), new LineaDeConsumo(LaReservaDeLaPrueba.OtraUbicacion, 2m, null, null)]);

        aValorar.Select(linea => linea.Cantidad).ShouldBe([-3m, -2m]);
        aValorar.ShouldAllBe(linea =>
            linea.Clave == new ClaveDeValoracion(LaReservaDeLaPrueba.Articulo, LaReservaDeLaPrueba.Almacen)
            && linea.Coste == null
            && linea.ValorQueCompensa == null);
    }

    /// <summary>No se consume más de lo pendiente, y lo pendiente justo sí.</summary>
    [Fact]
    public void No_se_consume_mas_de_lo_pendiente()
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m);

        Should.Throw<InvalidOperationException>(() => LaReservaDeLaPrueba.Consumir(
            reserva,
            LaReservaDeLaPrueba.Albaran,
            LaReservaDeLaPrueba.Momento,
            LaReservaDeLaPrueba.UnaLinea(6m),
            LaReservaDeLaPrueba.UnaLinea(4.000001m)));

        reserva.Consumos.ShouldBeEmpty();
        reserva.Pendiente.ShouldBe(10m);

        LaReservaDeLaPrueba.Consumir(
            reserva,
            LaReservaDeLaPrueba.Albaran,
            LaReservaDeLaPrueba.Momento,
            LaReservaDeLaPrueba.UnaLinea(6m),
            LaReservaDeLaPrueba.UnaLinea(4m));

        reserva.Pendiente.ShouldBe(0m);
    }

    /// <summary>
    /// Un albarán consume una reserva una sola vez: el segundo intento con el mismo documento es un
    /// reintento o un error, y no una salida más. Otro albarán sí.
    /// </summary>
    [Fact]
    public void Un_albaran_consume_una_reserva_una_sola_vez()
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m);
        LaReservaDeLaPrueba.Consumir(
            reserva, LaReservaDeLaPrueba.Albaran, LaReservaDeLaPrueba.Momento, LaReservaDeLaPrueba.UnaLinea(2m));

        Should.Throw<InvalidOperationException>(() => LaReservaDeLaPrueba.Consumir(
            reserva, LaReservaDeLaPrueba.Albaran, LaReservaDeLaPrueba.Momento, LaReservaDeLaPrueba.UnaLinea(2m)));

        reserva.Consumida.ShouldBe(2m);

        LaReservaDeLaPrueba.Consumir(
            reserva, LaReservaDeLaPrueba.OtroAlbaran, LaReservaDeLaPrueba.Momento, LaReservaDeLaPrueba.UnaLinea(2m));

        reserva.Consumida.ShouldBe(4m);
    }

    /// <summary>Una reserva consumida entera no se vuelve a consumir.</summary>
    [Fact]
    public void Una_reserva_consumida_no_se_vuelve_a_consumir()
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m);
        LaReservaDeLaPrueba.Consumir(
            reserva, LaReservaDeLaPrueba.Albaran, LaReservaDeLaPrueba.Momento, LaReservaDeLaPrueba.UnaLinea(10m));

        Should.Throw<InvalidOperationException>(() => LaReservaDeLaPrueba.Consumir(
            reserva, LaReservaDeLaPrueba.OtroAlbaran, LaReservaDeLaPrueba.Momento, LaReservaDeLaPrueba.UnaLinea(1m)));

        reserva.Consumos.Count.ShouldBe(1);
    }

    /// <summary>
    /// Una reserva caducada no se consume, aunque siga guardada activa. La pareja, un microsegundo
    /// antes.
    /// </summary>
    [Fact]
    public void Una_reserva_caducada_no_se_consume()
    {
        DateTimeOffset caducaEl = LaReservaDeLaPrueba.Momento.AddHours(1);
        Reserva caducada = LaReservaDeLaPrueba.UnaReservaDe(10m, caducaEl);
        Reserva vigente = LaReservaDeLaPrueba.UnaReservaDe(10m, caducaEl);

        Should.Throw<InvalidOperationException>(() => LaReservaDeLaPrueba.Consumir(
            caducada, LaReservaDeLaPrueba.Albaran, caducaEl, LaReservaDeLaPrueba.UnaLinea(1m)));
        LaReservaDeLaPrueba.Consumir(
            vigente, LaReservaDeLaPrueba.Albaran, caducaEl.AddTicks(-10), LaReservaDeLaPrueba.UnaLinea(1m));

        caducada.Consumos.ShouldBeEmpty();
        vigente.Consumida.ShouldBe(1m);
    }

    /// <summary>
    /// Solo la consume un albarán: el ajuste y la transferencia son documentos de Inventario, y
    /// ninguno sale contra una reserva (ADR-0059 §7).
    /// </summary>
    /// <param name="tipo">El documento que no la consume.</param>
    [Theory]
    [InlineData(TipoDeDocumentoOrigen.Ajuste)]
    [InlineData(TipoDeDocumentoOrigen.Transferencia)]
    public void Solo_la_consume_un_albaran(TipoDeDocumentoOrigen tipo)
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m);
        LineaDeConsumo[] lineas = [LaReservaDeLaPrueba.UnaLinea(1m)];

        Should.Throw<ArgumentException>(() => reserva.Consumir(
            tipo,
            LaReservaDeLaPrueba.Albaran,
            LaReservaDeLaPrueba.Hoy,
            lineas,
            LaReservaDeLaPrueba.AlPrecioMedio(lineas),
            LotesYSeriesResueltos.Ninguno,
            LaReservaDeLaPrueba.Divisa,
            LaReservaDeLaPrueba.Momento)).ParamName.ShouldBe("documentoTipo");

        reserva.Consumos.ShouldBeEmpty();
    }

    /// <summary>Un consumo sin documento no se puede atar al libro, y la doble flecha no lo cerraría.</summary>
    [Fact]
    public void Un_consumo_sin_documento_no_existe()
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m);

        Should.Throw<ArgumentException>(() => LaReservaDeLaPrueba.Consumir(
            reserva, Guid.Empty, LaReservaDeLaPrueba.Momento, LaReservaDeLaPrueba.UnaLinea(1m)))
            .ParamName.ShouldBe("documentoId");
    }

    /// <summary>Un consumo sin líneas no saca nada, y dejaría un consumo sin su fila del libro.</summary>
    [Fact]
    public void Un_consumo_sin_lineas_no_existe()
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m);

        Should.Throw<ArgumentException>(() => LaReservaDeLaPrueba.Consumir(
            reserva, LaReservaDeLaPrueba.Albaran, LaReservaDeLaPrueba.Momento)).ParamName.ShouldBe("lineas");
    }

    /// <summary>La valoración va una por línea y en la divisa del consumo, o no se consume nada.</summary>
    /// <param name="queFalla">Lo que no casa.</param>
    [Theory]
    [InlineData("una de menos")]
    [InlineData("otra divisa")]
    public void La_valoracion_va_una_por_linea_y_en_su_divisa(string queFalla)
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m);
        LineaDeConsumo[] lineas = [LaReservaDeLaPrueba.UnaLinea(1m), LaReservaDeLaPrueba.UnaLinea(2m)];
        IReadOnlyList<LineaValorada> valoracion = queFalla == "una de menos"
            ? [.. LaReservaDeLaPrueba.AlPrecioMedio(lineas).Take(1)]
            : [.. lineas.Select(linea => new LineaValorada(Importe.De(-linea.Cantidad, "USD"), PrecioUnitario.De(1m, "USD")))];

        Should.Throw<ArgumentException>(() => reserva.Consumir(
            TipoDeDocumentoOrigen.Albaran,
            LaReservaDeLaPrueba.Albaran,
            LaReservaDeLaPrueba.Hoy,
            lineas,
            valoracion,
            LotesYSeriesResueltos.Ninguno,
            LaReservaDeLaPrueba.Divisa,
            LaReservaDeLaPrueba.Momento)).ParamName.ShouldBe("valoracion");

        reserva.Consumos.ShouldBeEmpty();
    }

    /// <summary>
    /// Un lote que no llega resuelto no deja consumir, y el que llega va a su fila del libro.
    /// </summary>
    [Fact]
    public void El_lote_de_cada_linea_llega_resuelto()
    {
        var lote = Guid.Parse("0f6a1c1e-0000-4000-8000-0000000000b9");
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m);
        LineaDeConsumo[] lineas = [new(LaReservaDeLaPrueba.Ubicacion, 2m, " L-26 ", null)];

        Should.Throw<ArgumentException>(() => reserva.Consumir(
            TipoDeDocumentoOrigen.Albaran,
            LaReservaDeLaPrueba.Albaran,
            LaReservaDeLaPrueba.Hoy,
            lineas,
            LaReservaDeLaPrueba.AlPrecioMedio(lineas),
            LotesYSeriesResueltos.Ninguno,
            LaReservaDeLaPrueba.Divisa,
            LaReservaDeLaPrueba.Momento)).ParamName.ShouldBe("resueltos");

        reserva.Consumos.ShouldBeEmpty();

        IReadOnlyList<MovimientoStock> filas = reserva.Consumir(
            TipoDeDocumentoOrigen.Albaran,
            LaReservaDeLaPrueba.Albaran,
            LaReservaDeLaPrueba.Hoy,
            lineas,
            LaReservaDeLaPrueba.AlPrecioMedio(lineas),
            new LotesYSeriesResueltos(
                new Dictionary<CodigoDeUnArticulo, Guid> { [new(LaReservaDeLaPrueba.Articulo, "L-26")] = lote },
                new Dictionary<CodigoDeUnArticulo, Guid>()),
            LaReservaDeLaPrueba.Divisa,
            LaReservaDeLaPrueba.Momento);

        filas.ShouldHaveSingleItem().LoteId.ShouldBe(lote);
    }

    /// <summary>Una serie sale una vez por consumo, como en cualquier documento (ADR-0048 §3).</summary>
    [Fact]
    public void Una_serie_sale_una_vez_por_consumo()
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m);
        var serie = Guid.Parse("0f6a1c1e-0000-4000-8000-0000000000b8");
        LotesYSeriesResueltos resueltos = new(
            new Dictionary<CodigoDeUnArticulo, Guid>(),
            new Dictionary<CodigoDeUnArticulo, Guid> { [new(LaReservaDeLaPrueba.Articulo, "S-1")] = serie });
        LineaDeConsumo[] repetida =
        [
            new(LaReservaDeLaPrueba.Ubicacion, 1m, null, "S-1"),
            new(LaReservaDeLaPrueba.OtraUbicacion, 1m, null, "S-1"),
        ];

        Should.Throw<ArgumentException>(() => reserva.Consumir(
            TipoDeDocumentoOrigen.Albaran,
            LaReservaDeLaPrueba.Albaran,
            LaReservaDeLaPrueba.Hoy,
            repetida,
            LaReservaDeLaPrueba.AlPrecioMedio(repetida),
            resueltos,
            LaReservaDeLaPrueba.Divisa,
            LaReservaDeLaPrueba.Momento)).ParamName.ShouldBe("lineas");

        LineaDeConsumo[] una = [repetida[0]];

        reserva.Consumir(
            TipoDeDocumentoOrigen.Albaran,
            LaReservaDeLaPrueba.Albaran,
            LaReservaDeLaPrueba.Hoy,
            una,
            LaReservaDeLaPrueba.AlPrecioMedio(una),
            resueltos,
            LaReservaDeLaPrueba.Divisa,
            LaReservaDeLaPrueba.Momento).ShouldHaveSingleItem().NumeroDeSerieId.ShouldBe(serie);
    }

    /// <summary>
    /// Una línea de consumo saca una cantidad positiva de un hueco, con seis decimales como mucho, y
    /// con lote o con serie, no con los dos; con serie, una unidad. Los códigos se leen como GS1.
    /// </summary>
    [Fact]
    public void Una_linea_de_consumo_tiene_forma()
    {
        Guid hueco = LaReservaDeLaPrueba.Ubicacion;

        Should.Throw<ArgumentException>(() => new LineaDeConsumo(Guid.Empty, 1m, null, null))
            .ParamName.ShouldBe("ubicacionId");
        Should.Throw<ArgumentOutOfRangeException>(() => new LineaDeConsumo(hueco, 0m, null, null))
            .ParamName.ShouldBe("cantidad");
        Should.Throw<ArgumentOutOfRangeException>(() => new LineaDeConsumo(hueco, -1m, null, null))
            .ParamName.ShouldBe("cantidad");
        Should.Throw<ArgumentOutOfRangeException>(() => new LineaDeConsumo(hueco, 1.0000001m, null, null))
            .ParamName.ShouldBe("cantidad");
        Should.Throw<ArgumentException>(() => new LineaDeConsumo(hueco, 1m, "L-1", "S-1"))
            .ParamName.ShouldBe("numeroDeSerie");
        Should.Throw<ArgumentException>(() => new LineaDeConsumo(hueco, 2m, null, "S-1"))
            .ParamName.ShouldBe("numeroDeSerie");
        Should.Throw<ArgumentException>(() => new LineaDeConsumo(hueco, 1m, "con espacio", null))
            .ParamName.ShouldBe("codigoDeLote");
        Should.Throw<ArgumentException>(() => new LineaDeConsumo(hueco, 1m, null, "#1"))
            .ParamName.ShouldBe("numeroDeSerie");

        new LineaDeConsumo(hueco, 1.000001m, null, null).Cantidad.ShouldBe(1.000001m);
        new LineaDeConsumo(hueco, 2m, " L-1 ", null).CodigoDeLote.ShouldBe("L-1");
        new LineaDeConsumo(hueco, 1m, null, " S-1 ").NumeroDeSerie.ShouldBe("S-1");
    }
}
