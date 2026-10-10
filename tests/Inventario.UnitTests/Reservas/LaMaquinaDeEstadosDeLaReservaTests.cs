using Bastion.Inventario.Domain.Reservas;
using Shouldly;

namespace Bastion.Inventario.UnitTests.Reservas;

/// <summary>
/// Los tres estados de la reserva y lo que cada uno no deja hacer (R1, ADR-0059 §10):
/// <c>Activa → Consumida</c> al consumir lo que queda, y <c>Activa → Liberada</c> a mano o al
/// caducar.
/// </summary>
/// <remarks>
/// <b>Cada prohibición va con su pareja</b>: el mismo paso desde el estado que sí lo admite sale
/// bien en otro caso de este fichero o de los dos de al lado. Sin ella, una reserva que lo
/// rechazara todo pondría verdes todas las prohibiciones.
/// </remarks>
public sealed class LaMaquinaDeEstadosDeLaReservaTests
{
    /// <summary>Nace activa, sin consumos, sin liberación y con todo lo pedido aún por servir.</summary>
    [Fact]
    public void Nace_activa_sin_consumos_ni_liberacion()
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m);

        reserva.EstadoEn(LaReservaDeLaPrueba.Momento).ShouldBe(EstadoDeReserva.Activa);
        reserva.Id.Version.ShouldBe(7);
        reserva.EmpresaId.ShouldBe(LaReservaDeLaPrueba.Empresa);
        reserva.OrigenTipo.ShouldBe(TipoDeOrigenDeReserva.PedidoDeVenta);
        reserva.OrigenId.ShouldBe(LaReservaDeLaPrueba.Pedido);
        reserva.OrigenLinea.ShouldBe(1);
        reserva.ArticuloId.ShouldBe(LaReservaDeLaPrueba.Articulo);
        reserva.AlmacenId.ShouldBe(LaReservaDeLaPrueba.Almacen);
        reserva.UnidadBaseId.ShouldBe(LaReservaDeLaPrueba.Unidad);
        reserva.Cantidad.ShouldBe(10m);
        reserva.Consumida.ShouldBe(0m);
        reserva.Pendiente.ShouldBe(10m);
        reserva.Consumos.ShouldBeEmpty();
        reserva.CaducaEl.ShouldBeNull();
        reserva.Causa.ShouldBeNull();
        reserva.Motivo.ShouldBeNull("solo lleva motivo la que se libera a mano");
        reserva.LiberadaEl.ShouldBeNull();
        reserva.CreadoEn.ShouldBe(LaReservaDeLaPrueba.Momento);
    }

    /// <summary>Una reserva sin empresa no existe (R8): el filtro global no la vería nunca.</summary>
    [Fact]
    public void Una_reserva_sin_empresa_no_existe()
    {
        Should.Throw<ArgumentException>(() => Reserva.Reservar(
            Guid.Empty,
            TipoDeOrigenDeReserva.PedidoDeVenta,
            LaReservaDeLaPrueba.Pedido,
            1,
            LaReservaDeLaPrueba.Articulo,
            LaReservaDeLaPrueba.Almacen,
            10m,
            LaReservaDeLaPrueba.Unidad,
            null,
            LaReservaDeLaPrueba.Momento)).ParamName.ShouldBe("empresaId");
    }

    /// <summary>
    /// Una reserva sin documento de origen no existe: el origen es lo único que la hace idempotente
    /// (ADR-0059 §5), y el albarán la encuentra por él.
    /// </summary>
    [Fact]
    public void Una_reserva_sin_origen_no_existe()
    {
        Should.Throw<ArgumentException>(() => Reserva.Reservar(
            LaReservaDeLaPrueba.Empresa,
            TipoDeOrigenDeReserva.PedidoDeVenta,
            Guid.Empty,
            1,
            LaReservaDeLaPrueba.Articulo,
            LaReservaDeLaPrueba.Almacen,
            10m,
            LaReservaDeLaPrueba.Unidad,
            null,
            LaReservaDeLaPrueba.Momento)).ParamName.ShouldBe("origenId");
    }

    /// <summary>
    /// Un tipo de origen que el enumerado no conoce no se guarda: la columna es texto, y un número
    /// sin nombre se escribiría como un número.
    /// </summary>
    [Fact]
    public void Un_tipo_de_origen_que_no_existe_no_se_reserva()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Reserva.Reservar(
            LaReservaDeLaPrueba.Empresa,
            (TipoDeOrigenDeReserva)99,
            LaReservaDeLaPrueba.Pedido,
            1,
            LaReservaDeLaPrueba.Articulo,
            LaReservaDeLaPrueba.Almacen,
            10m,
            LaReservaDeLaPrueba.Unidad,
            null,
            LaReservaDeLaPrueba.Momento)).ParamName.ShouldBe("origenTipo");
    }

    /// <summary>La línea del origen empieza en uno, como las de cualquier documento.</summary>
    /// <param name="linea">Lo que no vale.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void La_linea_del_origen_es_positiva(int linea)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Reserva.Reservar(
            LaReservaDeLaPrueba.Empresa,
            TipoDeOrigenDeReserva.PedidoDeVenta,
            LaReservaDeLaPrueba.Pedido,
            linea,
            LaReservaDeLaPrueba.Articulo,
            LaReservaDeLaPrueba.Almacen,
            10m,
            LaReservaDeLaPrueba.Unidad,
            null,
            LaReservaDeLaPrueba.Momento)).ParamName.ShouldBe("origenLinea");
    }

    /// <summary>El artículo, el almacén y la unidad base son la clave y la unidad de la reserva.</summary>
    /// <param name="parametro">El que llega vacío.</param>
    [Theory]
    [InlineData("articuloId")]
    [InlineData("almacenId")]
    [InlineData("unidadBaseId")]
    public void Sin_articulo_almacen_o_unidad_no_hay_reserva(string parametro)
    {
        Should.Throw<ArgumentException>(() => Reserva.Reservar(
            LaReservaDeLaPrueba.Empresa,
            TipoDeOrigenDeReserva.PedidoDeVenta,
            LaReservaDeLaPrueba.Pedido,
            1,
            parametro == "articuloId" ? Guid.Empty : LaReservaDeLaPrueba.Articulo,
            parametro == "almacenId" ? Guid.Empty : LaReservaDeLaPrueba.Almacen,
            10m,
            parametro == "unidadBaseId" ? Guid.Empty : LaReservaDeLaPrueba.Unidad,
            null,
            LaReservaDeLaPrueba.Momento)).ParamName.ShouldBe(parametro);
    }

    /// <summary>Se reserva una cantidad positiva: cero no aparta nada, y negativa no significa nada.</summary>
    /// <param name="cantidad">Lo que no vale.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void La_cantidad_es_positiva(int cantidad)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => LaReservaDeLaPrueba.UnaReservaDe(cantidad))
            .ParamName.ShouldBe("cantidad");
    }

    /// <summary>
    /// La cantidad no pasa de los seis decimales de la columna: con siete, la base la redondearía
    /// al guardarla, y lo reservado dejaría de ser lo que se pidió.
    /// </summary>
    [Fact]
    public void La_cantidad_no_pasa_de_seis_decimales()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => LaReservaDeLaPrueba.UnaReservaDe(1.0000001m))
            .ParamName.ShouldBe("cantidad");

        LaReservaDeLaPrueba.UnaReservaDe(1.000001m).Cantidad.ShouldBe(1.000001m);
    }

    /// <summary>
    /// La caducidad es posterior al momento de reservar: una que ya ha pasado nacería liberada.
    /// </summary>
    /// <param name="segundos">Cuánto antes o después del momento caduca.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void La_caducidad_es_posterior_al_momento(int segundos)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => LaReservaDeLaPrueba.UnaReservaDe(
            10m, LaReservaDeLaPrueba.Momento.AddSeconds(segundos))).ParamName.ShouldBe("caducaEl");
    }

    /// <summary>La pareja: un microsegundo después del momento ya vale.</summary>
    [Fact]
    public void Un_microsegundo_despues_del_momento_la_caducidad_vale()
    {
        DateTimeOffset caducaEl = LaReservaDeLaPrueba.Momento.AddTicks(10);

        LaReservaDeLaPrueba.UnaReservaDe(10m, caducaEl).CaducaEl.ShouldBe(caducaEl);
    }

    /// <summary>
    /// La pregunta de la cantidad contesta lo mismo que el alta: es la que hace el caso de uso para
    /// devolver un <c>400</c> en vez de dejar que el dominio lance (ADR-0059 §5, precisión 7).
    /// </summary>
    /// <param name="cantidad">La cantidad, escrita como cadena para que el decimal sea exacto.</param>
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.0000001")]
    [InlineData("1.000001")]
    [InlineData("10")]
    public void La_pregunta_de_la_cantidad_contesta_lo_mismo_que_el_alta(string cantidad)
    {
        decimal valor = decimal.Parse(cantidad, System.Globalization.CultureInfo.InvariantCulture);
        bool nace = Nace(() => LaReservaDeLaPrueba.UnaReservaDe(valor));

        Reserva.EsUnaCantidadValida(valor).ShouldBe(nace);
    }

    /// <summary>
    /// La pregunta de la caducidad contesta lo mismo que el alta, y también medio microsegundo
    /// después del momento, que el alta trunca al microsegundo y rechaza: comparar a secas lo
    /// daría por bueno, y el caso de uso lanzaría en vez de contestar.
    /// </summary>
    /// <param name="ticks">Cuántos ticks después del momento caduca; <c>null</c>, sin caducidad.</param>
    [Theory]
    [InlineData(null)]
    [InlineData(-10)]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(15)]
    public void La_pregunta_de_la_caducidad_contesta_lo_mismo_que_el_alta(int? ticks)
    {
        DateTimeOffset? caducaEl = ticks is { } cuantos ? LaReservaDeLaPrueba.Momento.AddTicks(cuantos) : null;
        bool nace = Nace(() => LaReservaDeLaPrueba.UnaReservaDe(10m, caducaEl));

        Reserva.CaducaDespuesDe(caducaEl, LaReservaDeLaPrueba.Momento).ShouldBe(nace);
    }

    /// <summary>
    /// Liberarla a mano la deja liberada con su causa, su motivo recortado y el momento, y deja de
    /// apartar nada.
    /// </summary>
    [Fact]
    public void Liberar_a_mano_la_deja_liberada_con_su_motivo()
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m);
        DateTimeOffset despues = LaReservaDeLaPrueba.Momento.AddHours(1);

        reserva.Liberar("  El cliente anuló la línea  ", despues);

        reserva.EstadoEn(despues).ShouldBe(EstadoDeReserva.Liberada);
        reserva.Causa.ShouldBe(CausaDeLiberacion.AMano);
        reserva.Motivo.ShouldBe("El cliente anuló la línea");
        reserva.LiberadaEl.ShouldBe(despues);
        reserva.ApartaEn(despues).ShouldBe(0m);
        reserva.HaCaducadoEn(despues).ShouldBeFalse("se liberó a mano, no caducó");
    }

    /// <summary>Liberar a mano pide un motivo escrito, como el ajuste.</summary>
    /// <param name="motivo">Lo que no vale.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Liberar_a_mano_pide_un_motivo(string? motivo)
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m);

        Should.Throw<ArgumentException>(() => reserva.Liberar(motivo!, LaReservaDeLaPrueba.Momento))
            .ParamName.ShouldBe("motivo");

        reserva.EstadoEn(LaReservaDeLaPrueba.Momento).ShouldBe(EstadoDeReserva.Activa);
        reserva.Causa.ShouldBeNull();
    }

    /// <summary>El motivo no pasa de su largo, y con justo ese largo vale.</summary>
    [Fact]
    public void El_motivo_no_pasa_de_su_largo()
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m);

        Should.Throw<ArgumentException>(() => reserva.Liberar(
            new string('x', Reserva.LargoDelMotivo + 1), LaReservaDeLaPrueba.Momento)).ParamName.ShouldBe("motivo");

        reserva.Liberar(new string('x', Reserva.LargoDelMotivo), LaReservaDeLaPrueba.Momento);

        reserva.Motivo!.Length.ShouldBe(Reserva.LargoDelMotivo);
    }

    /// <summary>Una reserva liberada no se vuelve a liberar.</summary>
    [Fact]
    public void Una_reserva_liberada_no_se_vuelve_a_liberar()
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m);
        reserva.Liberar("El cliente anuló la línea", LaReservaDeLaPrueba.Momento);

        Should.Throw<InvalidOperationException>(() => reserva.Liberar(
            "Otra vez", LaReservaDeLaPrueba.Momento.AddHours(1)));

        reserva.Motivo.ShouldBe("El cliente anuló la línea");
        reserva.LiberadaEl.ShouldBe(LaReservaDeLaPrueba.Momento);
    }

    /// <summary>Una reserva liberada no se consume: lo que apartaba ya no es suyo.</summary>
    [Fact]
    public void Una_reserva_liberada_no_se_consume()
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m);
        reserva.Liberar("El cliente anuló la línea", LaReservaDeLaPrueba.Momento);

        Should.Throw<InvalidOperationException>(() => LaReservaDeLaPrueba.Consumir(
            reserva, LaReservaDeLaPrueba.Albaran, LaReservaDeLaPrueba.Momento, LaReservaDeLaPrueba.UnaLinea(1m)));

        reserva.Consumos.ShouldBeEmpty();
    }

    /// <summary>Una reserva consumida entera no se libera: no le queda nada que soltar.</summary>
    [Fact]
    public void Una_reserva_consumida_no_se_libera()
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m);
        LaReservaDeLaPrueba.Consumir(
            reserva, LaReservaDeLaPrueba.Albaran, LaReservaDeLaPrueba.Momento, LaReservaDeLaPrueba.UnaLinea(10m));

        Should.Throw<InvalidOperationException>(() => reserva.Liberar(
            "El cliente anuló la línea", LaReservaDeLaPrueba.Momento));

        reserva.EstadoEn(LaReservaDeLaPrueba.Momento).ShouldBe(EstadoDeReserva.Consumida);
        reserva.Causa.ShouldBeNull();
    }

    /// <summary>
    /// Liberar una reserva consumida en parte suelta lo que queda y conserva lo que ya salió: el
    /// consumo es historia del libro.
    /// </summary>
    [Fact]
    public void Liberar_una_consumida_en_parte_conserva_sus_consumos()
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m);
        LaReservaDeLaPrueba.Consumir(
            reserva, LaReservaDeLaPrueba.Albaran, LaReservaDeLaPrueba.Momento, LaReservaDeLaPrueba.UnaLinea(3m));

        reserva.Liberar("El cliente no quiere el resto", LaReservaDeLaPrueba.Momento);

        reserva.EstadoEn(LaReservaDeLaPrueba.Momento).ShouldBe(EstadoDeReserva.Liberada);
        reserva.Consumida.ShouldBe(3m);
        reserva.Consumos.Count.ShouldBe(1);
        reserva.ApartaEn(LaReservaDeLaPrueba.Momento).ShouldBe(0m);
    }

    /// <summary>
    /// Una reserva caducada no se libera a mano: ya está liberada, por su causa. La pareja, un
    /// minuto antes de caducar.
    /// </summary>
    [Fact]
    public void Una_reserva_caducada_no_se_libera_a_mano()
    {
        DateTimeOffset caducaEl = LaReservaDeLaPrueba.Momento.AddHours(1);
        Reserva caducada = LaReservaDeLaPrueba.UnaReservaDe(10m, caducaEl);
        Reserva vigente = LaReservaDeLaPrueba.UnaReservaDe(10m, caducaEl);

        Should.Throw<InvalidOperationException>(() => caducada.Liberar("Tarde", caducaEl));
        vigente.Liberar("A tiempo", caducaEl.AddMinutes(-1));

        caducada.Causa.ShouldBeNull();
        vigente.Causa.ShouldBe(CausaDeLiberacion.AMano);
    }

    // NACE O NO: el alta lanza con su ParamName, y aquí solo importa si lanza.
    private static bool Nace(Func<Reserva> alta)
    {
        try
        {
            alta();
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
