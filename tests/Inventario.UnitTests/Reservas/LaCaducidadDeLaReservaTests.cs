using Bastion.Inventario.Domain.Reservas;
using Shouldly;

namespace Bastion.Inventario.UnitTests.Reservas;

/// <summary>
/// La caducidad se aplica al leer y se escribe al pasar (ADR-0059 §4): una reserva guardada
/// <c>Activa</c> cuyo <c>caduca_el</c> no es posterior a ahora se lee <c>Liberada</c>, y la
/// escritura que pasa por su clave la deja guardada así, con la fecha de su caducidad.
/// </summary>
public sealed class LaCaducidadDeLaReservaTests
{
    private static readonly DateTimeOffset s_caducaEl = LaReservaDeLaPrueba.Momento.AddDays(7);

    /// <summary>Sin caducidad aparta lo pendiente hasta que alguien la consuma o la libere.</summary>
    [Fact]
    public void Sin_caducidad_sigue_activa()
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m);
        DateTimeOffset dentroDeDiezAnos = LaReservaDeLaPrueba.Momento.AddYears(10);

        reserva.EstadoEn(dentroDeDiezAnos).ShouldBe(EstadoDeReserva.Activa);
        reserva.ApartaEn(dentroDeDiezAnos).ShouldBe(10m);
        reserva.LiberarSiHaCaducado(dentroDeDiezAnos).ShouldBeFalse();
    }

    /// <summary>
    /// La frontera es el instante de la caducidad: un microsegundo antes aparta, y en ese mismo
    /// instante ya no.
    /// </summary>
    /// <param name="microsegundos">Cuánto después de la caducidad se lee.</param>
    /// <param name="activa">Si se lee activa.</param>
    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public void Caduca_en_el_instante_de_su_caducidad(int microsegundos, bool activa)
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m, s_caducaEl);
        DateTimeOffset ahora = s_caducaEl.AddTicks(microsegundos * 10L);

        reserva.EstadoEn(ahora).ShouldBe(activa ? EstadoDeReserva.Activa : EstadoDeReserva.Liberada);
        reserva.ApartaEn(ahora).ShouldBe(activa ? 10m : 0m);
        reserva.HaCaducadoEn(ahora).ShouldBe(!activa);
    }

    /// <summary>
    /// Leerla caducada no la cambia: lo que se escribe al pasar lo escribe
    /// <see cref="Reserva.LiberarSiHaCaducado"/>, y nadie más.
    /// </summary>
    [Fact]
    public void Leerla_caducada_no_la_escribe()
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m, s_caducaEl);

        reserva.EstadoEn(s_caducaEl).ShouldBe(EstadoDeReserva.Liberada);

        reserva.Causa.ShouldBeNull();
        reserva.LiberadaEl.ShouldBeNull();
        reserva.EstadoEn(s_caducaEl.AddTicks(-10)).ShouldBe(EstadoDeReserva.Activa, "sigue guardada activa");
    }

    /// <summary>
    /// Liberarla al pasar le pone la causa y, como fecha, la de su caducidad: no la de la escritura
    /// que la encontró, que puede llegar días después (ADR-0059 §4).
    /// </summary>
    [Fact]
    public void Al_pasar_se_libera_con_la_fecha_de_su_caducidad()
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m, s_caducaEl);
        DateTimeOffset tresDiasDespues = s_caducaEl.AddDays(3);

        reserva.LiberarSiHaCaducado(tresDiasDespues).ShouldBeTrue();

        reserva.EstadoEn(tresDiasDespues).ShouldBe(EstadoDeReserva.Liberada);
        reserva.EstadoEn(LaReservaDeLaPrueba.Momento).ShouldBe(
            EstadoDeReserva.Liberada, "ya está guardada así, y se lea cuando se lea");
        reserva.Causa.ShouldBe(CausaDeLiberacion.Caducidad);
        reserva.LiberadaEl.ShouldBe(s_caducaEl);
        reserva.Motivo.ShouldBeNull("una caducidad no se justifica");
        reserva.HaCaducadoEn(tresDiasDespues).ShouldBeTrue();
    }

    /// <summary>La pareja: antes de caducar, pasar por su clave no la toca.</summary>
    [Fact]
    public void Antes_de_caducar_pasar_no_la_toca()
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m, s_caducaEl);

        reserva.LiberarSiHaCaducado(s_caducaEl.AddTicks(-10)).ShouldBeFalse();

        reserva.EstadoEn(s_caducaEl.AddTicks(-10)).ShouldBe(EstadoDeReserva.Activa);
        reserva.Causa.ShouldBeNull();
        reserva.LiberadaEl.ShouldBeNull();
    }

    /// <summary>Pasar dos veces no la vuelve a liberar: la segunda no encuentra nada que hacer.</summary>
    [Fact]
    public void Pasar_dos_veces_la_libera_una()
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m, s_caducaEl);
        reserva.LiberarSiHaCaducado(s_caducaEl);

        reserva.LiberarSiHaCaducado(s_caducaEl.AddDays(1)).ShouldBeFalse();

        reserva.LiberadaEl.ShouldBe(s_caducaEl);
    }

    /// <summary>
    /// Una consumida o liberada a mano no caduca, aunque su caducidad pase: ya no aparta nada.
    /// </summary>
    [Fact]
    public void Una_consumida_o_liberada_a_mano_no_caduca()
    {
        Reserva consumida = LaReservaDeLaPrueba.UnaReservaDe(10m, s_caducaEl);
        LaReservaDeLaPrueba.Consumir(
            consumida, LaReservaDeLaPrueba.Albaran, LaReservaDeLaPrueba.Momento, LaReservaDeLaPrueba.UnaLinea(10m));
        Reserva liberada = LaReservaDeLaPrueba.UnaReservaDe(10m, s_caducaEl);
        liberada.Liberar("El cliente anuló la línea", LaReservaDeLaPrueba.Momento);

        consumida.LiberarSiHaCaducado(s_caducaEl).ShouldBeFalse();
        liberada.LiberarSiHaCaducado(s_caducaEl).ShouldBeFalse();

        consumida.EstadoEn(s_caducaEl).ShouldBe(EstadoDeReserva.Consumida);
        consumida.HaCaducadoEn(s_caducaEl).ShouldBeFalse();
        liberada.Causa.ShouldBe(CausaDeLiberacion.AMano);
        liberada.HaCaducadoEn(s_caducaEl).ShouldBeFalse();
    }

    /// <summary>
    /// Consumida en parte, aparta lo que le queda mientras no caduque, y al caducar suelta solo eso:
    /// lo consumido ya salió por el libro.
    /// </summary>
    [Fact]
    public void Consumida_en_parte_aparta_lo_que_le_queda()
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m, s_caducaEl);
        LaReservaDeLaPrueba.Consumir(
            reserva, LaReservaDeLaPrueba.Albaran, LaReservaDeLaPrueba.Momento, LaReservaDeLaPrueba.UnaLinea(4m));

        reserva.ApartaEn(LaReservaDeLaPrueba.Momento).ShouldBe(6m);
        reserva.ApartaEn(s_caducaEl).ShouldBe(0m);
        reserva.Consumida.ShouldBe(4m);
    }

    /// <summary>
    /// La caducidad se guarda en UTC y al microsegundo, que es lo que guarda <c>timestamptz</c>. Así
    /// la reserva que vuelve de la base es la que se pidió, y el reintento la reconoce (ADR-0059 §5).
    /// </summary>
    [Fact]
    public void La_caducidad_se_guarda_en_utc_y_al_microsegundo()
    {
        DateTimeOffset enMadrid = new DateTimeOffset(2026, 3, 21, 11, 0, 0, TimeSpan.FromHours(2)).AddTicks(7);

        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(10m, enMadrid);

        reserva.CaducaEl.ShouldBe(new DateTimeOffset(2026, 3, 21, 9, 0, 0, TimeSpan.Zero));
        reserva.CaducaEl!.Value.Offset.ShouldBe(TimeSpan.Zero);
    }

    /// <summary>
    /// La misma petición es la misma reserva: el artículo, el almacén, la cantidad y la caducidad,
    /// esta comparada como se guarda.
    /// </summary>
    [Fact]
    public void La_misma_peticion_es_la_misma_reserva()
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(5m, s_caducaEl);

        reserva.PideLoMismo(
            LaReservaDeLaPrueba.Articulo, LaReservaDeLaPrueba.Almacen, 5.000m, s_caducaEl.AddTicks(3))
            .ShouldBeTrue("los ceros de la derecha y el resto del microsegundo no son otra petición");
        reserva.PideLoMismo(
            LaReservaDeLaPrueba.Articulo,
            LaReservaDeLaPrueba.Almacen,
            5m,
            s_caducaEl.ToOffset(TimeSpan.FromHours(2))).ShouldBeTrue("el mismo instante en otra zona");
    }

    /// <summary>La pareja: cualquier diferencia en lo que se pide es otra petición.</summary>
    /// <param name="queCambia">Lo que cambia.</param>
    [Theory]
    [InlineData("articulo")]
    [InlineData("almacen")]
    [InlineData("cantidad")]
    [InlineData("caducidad")]
    [InlineData("sin caducidad")]
    public void Otra_peticion_no_es_la_misma_reserva(string queCambia)
    {
        Reserva reserva = LaReservaDeLaPrueba.UnaReservaDe(5m, s_caducaEl);

        reserva.PideLoMismo(
            queCambia == "articulo" ? Guid.NewGuid() : LaReservaDeLaPrueba.Articulo,
            queCambia == "almacen" ? Guid.NewGuid() : LaReservaDeLaPrueba.Almacen,
            queCambia == "cantidad" ? 5.000001m : 5m,
            queCambia switch
            {
                "caducidad" => s_caducaEl.AddTicks(10),
                "sin caducidad" => null,
                _ => s_caducaEl,
            }).ShouldBeFalse();
    }
}
