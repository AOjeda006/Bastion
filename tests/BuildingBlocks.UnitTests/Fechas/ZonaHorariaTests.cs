using Bastion.BuildingBlocks.Domain.Fechas;
using Shouldly;

namespace Bastion.BuildingBlocks.UnitTests.Fechas;

/// <summary>
/// Las zonas horarias que admite el negocio son tres, se leen por su identificador exacto, y esta
/// máquina —la del desarrollador o la del <i>runner</i>— las conoce con sus reglas.
/// </summary>
public sealed class ZonaHorariaTests
{
    [Fact]
    public void Las_admitidas_son_las_tres_de_Espana_y_la_peninsular_la_de_por_omision()
    {
        ZonaHoraria.Admitidas.Select(zona => zona.Iana)
            .ShouldBe(["Europe/Madrid", "Atlantic/Canary", "Africa/Ceuta"]);

        ZonaHoraria.PorOmision.ShouldBeSameAs(ZonaHoraria.Madrid);
    }

    [Theory]
    [InlineData("Europe/Madrid")]
    [InlineData("Atlantic/Canary")]
    [InlineData("Africa/Ceuta")]
    public void Cada_admitida_se_lee_por_su_identificador_y_es_la_misma_instancia(string iana)
    {
        var zona = ZonaHoraria.De(iana);

        zona.Iana.ShouldBe(iana);
        zona.ToString().ShouldBe(iana);
        ZonaHoraria.Admitidas.ShouldContain(admitida => ReferenceEquals(admitida, zona));
    }

    [Theory]
    [InlineData("europe/madrid")]
    [InlineData(" Europe/Madrid")]
    [InlineData("Europe/London")]
    [InlineData("America/Bogota")]
    [InlineData("UTC")]
    [InlineData("Romance Standard Time")]
    [InlineData("")]
    [InlineData(null)]
    public void Lo_que_no_es_una_admitida_exacta_no_se_lee(string? iana)
    {
        ZonaHoraria.Intentar(iana, out ZonaHoraria? zona).ShouldBeFalse();
        zona.ShouldBeNull();
    }

    [Fact]
    public void Una_zona_que_no_se_admite_lanza_y_dice_cuales_se_admiten()
    {
        ArgumentException excepcion = Should.Throw<ArgumentException>(() => ZonaHoraria.De("Europe/London"));

        excepcion.ParamName.ShouldBe("iana");
        excepcion.Message.ShouldContain("Europe/Madrid, Atlantic/Canary, Africa/Ceuta");
    }

    /// <summary>
    /// Esta máquina conoce las tres, y con sus reglas: en el <i>runner</i> las da la base de zonas
    /// de Linux, y en Windows, ICU. Encontrar un nombre no basta, porque una traducción equivocada
    /// también lo encontraría: cada una se pregunta por su desplazamiento en invierno y en verano.
    /// </summary>
    /// <param name="iana">La zona.</param>
    /// <param name="invierno">Su desplazamiento el 15 de enero de 2026, en horas.</param>
    /// <param name="verano">Su desplazamiento el 15 de julio de 2026, en horas.</param>
    [Theory]
    [InlineData("Europe/Madrid", 1, 2)]
    [InlineData("Atlantic/Canary", 0, 1)]
    [InlineData("Africa/Ceuta", 1, 2)]
    public void Esta_maquina_conoce_cada_admitida_con_sus_reglas(string iana, int invierno, int verano)
    {
        var reglas = TimeZoneInfo.FindSystemTimeZoneById(ZonaHoraria.De(iana).Iana);

        reglas.GetUtcOffset(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero))
            .ShouldBe(TimeSpan.FromHours(invierno));
        reglas.GetUtcOffset(new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero))
            .ShouldBe(TimeSpan.FromHours(verano));
    }
}
