using Bastion.BuildingBlocks.Domain.Fechas;
using Shouldly;

namespace Bastion.BuildingBlocks.UnitTests.Fechas;

/// <summary>
/// El día de negocio es el día de la zona en ese instante, se escriba el instante con el
/// desplazamiento que se escriba (ADR-0079). Los casos van sobre la madrugada, que es donde el día
/// UTC y el de aquí no coinciden, y sobre los dos cambios de hora, que es donde un día no dura
/// veinticuatro horas.
/// </summary>
/// <remarks>
/// Cada caso dice su zona: ninguno hereda la de la máquina, que en la del autor es Madrid y en el
/// <i>runner</i> es UTC, así que un caso que la heredara saldría verde en una y rojo en la otra.
/// </remarks>
public sealed class DiaDeNegocioTests
{
    /// <summary>Las doce y media de la noche de fin de año en la península: en UTC, las 23:30.</summary>
    private static readonly DateTimeOffset s_finDeAnioEnMadrid = new(2026, 12, 31, 23, 30, 0, TimeSpan.Zero);

    [Fact]
    public void A_las_doce_y_media_del_uno_de_enero_en_Madrid_la_fecha_es_el_uno_de_enero()
    {
        // El criterio del 3.5: en UTC todavía es el 31 de diciembre, y hasta el 3.5 eso era hoy.
        DiaDeNegocio.De(s_finDeAnioEnMadrid, ZonaHoraria.Madrid).ShouldBe(new DateOnly(2027, 1, 1));
    }

    [Fact]
    public void En_ese_instante_en_Canarias_todavia_es_el_treinta_y_uno()
    {
        // Una hora por detrás todo el año: allí son las 23:30, y el mismo instante es otro día.
        DiaDeNegocio.De(s_finDeAnioEnMadrid, ZonaHoraria.Canarias).ShouldBe(new DateOnly(2026, 12, 31));
    }

    [Fact]
    public void Ceuta_dice_el_dia_de_la_peninsula()
    {
        DiaDeNegocio.De(s_finDeAnioEnMadrid, ZonaHoraria.Ceuta).ShouldBe(new DateOnly(2027, 1, 1));
    }

    [Fact]
    public void En_verano_la_peninsula_cambia_de_dia_a_las_diez_de_la_noche_UTC_y_Canarias_a_las_once()
    {
        // En verano Madrid va a +02:00 y Canarias a +01:00: sus medianoches del 15 de julio son las
        // 22:00 y las 23:00 UTC del 14. En la hora de en medio, cada zona está en un día distinto.
        DateTimeOffset enMadrid = new(2027, 7, 14, 22, 0, 0, TimeSpan.Zero);
        DateTimeOffset enCanarias = new(2027, 7, 14, 23, 0, 0, TimeSpan.Zero);

        DiaDeNegocio.De(enMadrid.AddTicks(-1), ZonaHoraria.Madrid).ShouldBe(new DateOnly(2027, 7, 14));
        DiaDeNegocio.De(enMadrid, ZonaHoraria.Madrid).ShouldBe(new DateOnly(2027, 7, 15));
        DiaDeNegocio.De(enCanarias.AddTicks(-1), ZonaHoraria.Canarias).ShouldBe(new DateOnly(2027, 7, 14));
        DiaDeNegocio.De(enCanarias, ZonaHoraria.Canarias).ShouldBe(new DateOnly(2027, 7, 15));
    }

    [Fact]
    public void El_desplazamiento_con_el_que_se_escribe_el_instante_no_cuenta()
    {
        // Las 18:30 del 31 de diciembre en -05:00 son el mismo instante que las 00:30 del 1 de
        // enero en Madrid: lo que cuenta es la zona del sitio, no cómo llegó escrito.
        var instante = new DateTimeOffset(2026, 12, 31, 18, 30, 0, TimeSpan.FromHours(-5));

        DiaDeNegocio.De(instante, ZonaHoraria.Madrid).ShouldBe(new DateOnly(2027, 1, 1));
    }

    [Fact]
    public void El_ultimo_tic_del_dia_en_su_zona_sigue_siendo_ese_dia()
    {
        // La medianoche de Madrid en invierno son las 23:00 UTC del día anterior.
        DateTimeOffset medianoche = new(2027, 2, 28, 23, 0, 0, TimeSpan.Zero);

        DiaDeNegocio.De(medianoche.AddTicks(-1), ZonaHoraria.Madrid).ShouldBe(new DateOnly(2027, 2, 28));
        DiaDeNegocio.De(medianoche, ZonaHoraria.Madrid).ShouldBe(new DateOnly(2027, 3, 1));
    }

    [Fact]
    public void Hoy_es_el_dia_de_la_zona_en_el_instante_que_da_el_reloj()
    {
        // El reloj se pide en UTC y no en local: la zona local del reloj es la de su máquina, y la
        // de este está puesta a propósito catorce horas por delante para que leerla diera otro día.
        var lejos = TimeZoneInfo.CreateCustomTimeZone("Catorce horas por delante", TimeSpan.FromHours(14), "X", "X");
        var reloj = new RelojParado(s_finDeAnioEnMadrid.AddHours(-2), lejos);

        DiaDeNegocio.Hoy(reloj, ZonaHoraria.Madrid).ShouldBe(new DateOnly(2026, 12, 31));
        DiaDeNegocio.Hoy(new RelojParado(s_finDeAnioEnMadrid, lejos), ZonaHoraria.Madrid)
            .ShouldBe(new DateOnly(2027, 1, 1));
    }

    [Fact]
    public void Sin_reloj_o_sin_zona_lanza()
    {
        var reloj = new RelojParado(s_finDeAnioEnMadrid, TimeZoneInfo.Utc);

        Should.Throw<ArgumentNullException>(() => DiaDeNegocio.Hoy(null!, ZonaHoraria.Madrid))
            .ParamName.ShouldBe("reloj");
        Should.Throw<ArgumentNullException>(() => DiaDeNegocio.Hoy(reloj, null!))
            .ParamName.ShouldBe("zona");
        Should.Throw<ArgumentNullException>(() => DiaDeNegocio.Inicio(new DateOnly(2027, 1, 1), null!))
            .ParamName.ShouldBe("zona");
    }

    /// <summary>
    /// Los dos cambios de hora de 2026: el día del de primavera dura veintitrés horas, y el del de
    /// otoño, veinticinco, en las tres zonas. Su final es el principio del día siguiente.
    /// </summary>
    /// <param name="iana">La zona.</param>
    /// <param name="dia">El día, en ISO.</param>
    /// <param name="inicioUtc">A qué hora UTC empieza.</param>
    /// <param name="horas">Cuántas horas dura.</param>
    [Theory]
    [InlineData("Europe/Madrid", "2026-03-29", "2026-03-28T23:00:00Z", 23)]
    [InlineData("Europe/Madrid", "2026-10-25", "2026-10-24T22:00:00Z", 25)]
    [InlineData("Atlantic/Canary", "2026-03-29", "2026-03-29T00:00:00Z", 23)]
    [InlineData("Atlantic/Canary", "2026-10-25", "2026-10-24T23:00:00Z", 25)]
    [InlineData("Africa/Ceuta", "2026-03-29", "2026-03-28T23:00:00Z", 23)]
    [InlineData("Africa/Ceuta", "2026-10-25", "2026-10-24T22:00:00Z", 25)]
    public void Un_dia_de_cambio_de_hora_no_dura_veinticuatro_horas(
        string iana, string dia, string inicioUtc, int horas)
    {
        var zona = ZonaHoraria.De(iana);
        var elDia = DateOnly.Parse(dia, System.Globalization.CultureInfo.InvariantCulture);

        DateTimeOffset inicio = DiaDeNegocio.Inicio(elDia, zona);

        inicio.ShouldBe(DateTimeOffset.Parse(inicioUtc, System.Globalization.CultureInfo.InvariantCulture));
        inicio.Offset.ShouldBe(TimeSpan.Zero);
        (DiaDeNegocio.Inicio(elDia.AddDays(1), zona) - inicio).ShouldBe(TimeSpan.FromHours(horas));
    }

    /// <summary>
    /// En las tres zonas, de 2000 a 2100, la medianoche existe y es una sola: el instante en que
    /// empieza un día es de ese día, y el tic de antes, del anterior.
    /// </summary>
    /// <remarks>
    /// Si una zona cambiara la hora a medianoche, una medianoche no existiría —y la conversión
    /// lanzaría— o existiría dos veces, y el principio del día sería la segunda: el tic de antes
    /// seguiría siendo del mismo día. Las dos cosas las ve este caso, sin mirar las reglas.
    /// </remarks>
    /// <param name="iana">La zona.</param>
    [Theory]
    [InlineData("Europe/Madrid")]
    [InlineData("Atlantic/Canary")]
    [InlineData("Africa/Ceuta")]
    public void La_medianoche_existe_y_es_una_sola(string iana)
    {
        var zona = ZonaHoraria.De(iana);
        int dias = 0;

        for (var dia = new DateOnly(2000, 1, 1); dia < new DateOnly(2101, 1, 1); dia = dia.AddDays(1))
        {
            DateTimeOffset inicio = DiaDeNegocio.Inicio(dia, zona);

            if (DiaDeNegocio.De(inicio, zona) != dia || DiaDeNegocio.De(inicio.AddTicks(-1), zona) != dia.AddDays(-1))
            {
                throw new ShouldAssertException($"En {iana}, el {dia:O} no empieza a su medianoche.");
            }

            dias++;
        }

        dias.ShouldBe(36_890);
    }

    [Fact]
    public void Entre_varias_zonas_el_mas_atrasado_y_el_mas_adelantado()
    {
        ZonaHoraria[] zonas = [ZonaHoraria.Madrid, ZonaHoraria.Canarias];

        DiaDeNegocio.ElMasAtrasado(s_finDeAnioEnMadrid, zonas).ShouldBe(new DateOnly(2026, 12, 31));
        DiaDeNegocio.ElMasAdelantado(s_finDeAnioEnMadrid, zonas).ShouldBe(new DateOnly(2027, 1, 1));

        // Con una sola, los dos son el suyo.
        DiaDeNegocio.ElMasAtrasado(s_finDeAnioEnMadrid, [ZonaHoraria.Madrid]).ShouldBe(new DateOnly(2027, 1, 1));
        DiaDeNegocio.ElMasAdelantado(s_finDeAnioEnMadrid, [ZonaHoraria.Canarias])
            .ShouldBe(new DateOnly(2026, 12, 31));
    }

    /// <summary>
    /// Un día no va antes que otro si no acaba antes de que el otro empiece: en una zona es comparar
    /// los dos días, y entre dos, el anterior en el calendario puede compartir una hora con el otro.
    /// Las filas van sobre los dos cambios de hora de 2026, donde el final de un día no es su
    /// principio más veinticuatro horas.
    /// </summary>
    /// <param name="dia">El día que no puede ir antes, en ISO.</param>
    /// <param name="zona">Su zona.</param>
    /// <param name="otro">El otro, en ISO.</param>
    /// <param name="zonaDelOtro">La suya.</param>
    /// <param name="noVaAntes">Si no acaba antes de que empiece el otro.</param>
    [Theory]
    // En Madrid, el 29 de marzo dura veintitrés horas y acaba a las 22:00 UTC, justo cuando empieza
    // el 30: no comparten ni un instante, y va antes.
    [InlineData("2026-03-29", "Europe/Madrid", "2026-03-30", "Europe/Madrid", false)]
    [InlineData("2026-03-30", "Europe/Madrid", "2026-03-30", "Europe/Madrid", true)]
    // El 25 de octubre en Canarias dura veinticinco horas, de las 23:00 UTC del 24 a las 00:00 del
    // 26, y su última hora es la primera del 26 en Madrid. El 24, no.
    [InlineData("2026-10-25", "Atlantic/Canary", "2026-10-26", "Europe/Madrid", true)]
    [InlineData("2026-10-24", "Atlantic/Canary", "2026-10-26", "Europe/Madrid", false)]
    // Al revés, el día de Madrid empieza y acaba una hora antes que el mismo día en Canarias: el 28
    // de Madrid acaba antes de que empiece el 29 de Canarias, y el 29 de Madrid no.
    [InlineData("2026-03-28", "Europe/Madrid", "2026-03-29", "Atlantic/Canary", false)]
    [InlineData("2026-03-29", "Europe/Madrid", "2026-03-29", "Atlantic/Canary", true)]
    public void Un_dia_no_va_antes_que_otro_si_no_acaba_antes_de_que_el_otro_empiece(
        string dia, string zona, string otro, string zonaDelOtro, bool noVaAntes)
    {
        DiaDeNegocio.NoAcabaAntesDe(Dia(dia), ZonaHoraria.De(zona), Dia(otro), ZonaHoraria.De(zonaDelOtro))
            .ShouldBe(noVaAntes);
    }

    [Fact]
    public void Sin_zona_no_se_sabe_si_un_dia_va_antes()
    {
        var dia = new DateOnly(2027, 1, 1);

        Should.Throw<ArgumentNullException>(() => DiaDeNegocio.NoAcabaAntesDe(dia, null!, dia, ZonaHoraria.Madrid))
            .ParamName.ShouldBe("zona");
        Should.Throw<ArgumentNullException>(() => DiaDeNegocio.NoAcabaAntesDe(dia, ZonaHoraria.Madrid, dia, null!))
            .ParamName.ShouldBe("zonaDelOtro");
    }

    [Fact]
    public void Sin_ninguna_zona_no_hay_dia_que_elegir()
    {
        Should.Throw<ArgumentException>(() => DiaDeNegocio.ElMasAtrasado(s_finDeAnioEnMadrid, []))
            .ParamName.ShouldBe("zonas");
        Should.Throw<ArgumentException>(() => DiaDeNegocio.ElMasAdelantado(s_finDeAnioEnMadrid, []))
            .ParamName.ShouldBe("zonas");
    }

    private static DateOnly Dia(string iso) => DateOnly.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Un reloj parado, con su zona local. <c>TimeProvider</c> es de la biblioteca base, no una
    /// dependencia del dominio común: el doble son estas líneas, no un paquete.
    /// </summary>
    private sealed class RelojParado(DateTimeOffset instante, TimeZoneInfo zonaLocal) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => zonaLocal;

        public override DateTimeOffset GetUtcNow() => instante;
    }
}
