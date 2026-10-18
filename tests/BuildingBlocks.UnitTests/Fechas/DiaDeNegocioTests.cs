using Bastion.BuildingBlocks.Domain.Fechas;
using Shouldly;

namespace Bastion.BuildingBlocks.UnitTests.Fechas;

/// <summary>
/// El día de negocio es el día UTC del instante, se escriba el instante con el desplazamiento que
/// se escriba. Los casos van sobre la madrugada española, que es donde el día UTC y el de aquí no
/// coinciden, y es justo lo que la nota de la zona del PLAN dice que cuesta.
/// </summary>
public sealed class DiaDeNegocioTests
{
    [Fact]
    public void Las_doce_y_media_del_uno_de_marzo_en_Madrid_es_todavia_el_28_de_febrero()
    {
        // 00:30 en invierno, +01:00: en UTC son las 23:30 del día anterior. Leído con su propio
        // desplazamiento saldría el 1 de marzo, y el corte pediría un mes que no ha cerrado.
        var instante = new DateTimeOffset(2027, 3, 1, 0, 30, 0, TimeSpan.FromHours(1));

        DiaDeNegocio.De(instante).ShouldBe(new DateOnly(2027, 2, 28));
    }

    [Fact]
    public void La_una_y_media_de_una_noche_de_verano_en_Madrid_es_todavia_el_dia_anterior()
    {
        // +02:00 en verano: la ventana en la que aquí ya es mañana dura dos horas, no una.
        var instante = new DateTimeOffset(2027, 7, 15, 1, 30, 0, TimeSpan.FromHours(2));

        DiaDeNegocio.De(instante).ShouldBe(new DateOnly(2027, 7, 14));
    }

    [Fact]
    public void Un_instante_al_oeste_de_Greenwich_puede_ser_ya_el_dia_siguiente()
    {
        // La otra dirección: las 20:00 del 31 de diciembre en -05:00 son la 01:00 del 1 de enero.
        var instante = new DateTimeOffset(2027, 12, 31, 20, 0, 0, TimeSpan.FromHours(-5));

        DiaDeNegocio.De(instante).ShouldBe(new DateOnly(2028, 1, 1));
    }

    [Fact]
    public void El_ultimo_tic_del_dia_UTC_sigue_siendo_ese_dia()
    {
        DateTimeOffset instante =
            new DateTimeOffset(2027, 2, 28, 23, 59, 59, TimeSpan.Zero).AddTicks(9_999_999);

        DiaDeNegocio.De(instante).ShouldBe(new DateOnly(2027, 2, 28));
        DiaDeNegocio.De(instante.AddTicks(1)).ShouldBe(new DateOnly(2027, 3, 1));
    }

    [Fact]
    public void Hoy_es_el_dia_de_negocio_del_instante_que_da_el_reloj()
    {
        // El reloj se pide en UTC y no en local: la zona local del reloj es la de su máquina, y la
        // de este está puesta a propósito en Madrid para que leerla diera otro día.
        var madridEnInvierno = TimeZoneInfo.CreateCustomTimeZone(
            "Madrid en invierno", TimeSpan.FromHours(1), "CET", "CET");
        var reloj = new RelojParado(
            new DateTimeOffset(2027, 2, 28, 23, 30, 0, TimeSpan.Zero), madridEnInvierno);

        DiaDeNegocio.Hoy(reloj).ShouldBe(new DateOnly(2027, 2, 28));
    }

    [Fact]
    public void Hoy_sin_reloj_lanza()
    {
        Should.Throw<ArgumentNullException>(() => DiaDeNegocio.Hoy(null!))
            .ParamName.ShouldBe("reloj");
    }

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
