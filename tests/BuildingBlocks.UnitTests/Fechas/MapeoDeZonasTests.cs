using System.Text.Json;
using Bastion.BuildingBlocks.Application.Fechas;
using Bastion.BuildingBlocks.Contracts.Fechas;
using Bastion.BuildingBlocks.Domain.Fechas;
using Shouldly;

namespace Bastion.BuildingBlocks.UnitTests.Fechas;

/// <summary>
/// La lista de zonas del contrato es la del dominio: la misma, en los dos sentidos, y con el
/// identificador que publica el documento de la API.
/// </summary>
/// <remarks>
/// Son dos listas escritas en dos proyectos, y comparar enteras solo vale si las dos pueden dar la
/// misma (ADR-0024). Aquí pueden: las dos son cerradas y ninguna infradetecta. Así que se afirma
/// que ninguna está vacía, que cada una cabe en la otra y que cada miembro del contrato se publica
/// con el identificador de la zona que nombra.
/// </remarks>
public sealed class MapeoDeZonasTests
{
    private static readonly ZonaHorariaDto[] s_delContrato = Enum.GetValues<ZonaHorariaDto>();

    [Fact]
    public void Ninguna_de_las_dos_listas_esta_vacia()
    {
        s_delContrato.ShouldNotBeEmpty();
        ZonaHoraria.Admitidas.ShouldNotBeEmpty();
    }

    [Fact]
    public void Cada_zona_del_contrato_se_publica_con_el_identificador_de_la_del_dominio_que_nombra()
    {
        foreach (ZonaHorariaDto zona in s_delContrato)
        {
            ZonaHoraria delDominio = zona.AlDominio();

            ZonaHoraria.Admitidas.ShouldContain(delDominio);
            JsonSerializer.Serialize(zona).ShouldBe($"\"{delDominio.Iana}\"", $"el miembro {zona}");
            JsonSerializer.Deserialize<ZonaHorariaDto>($"\"{delDominio.Iana}\"").ShouldBe(zona);
        }
    }

    [Fact]
    public void Cada_zona_admitida_va_y_vuelve_por_el_contrato()
    {
        foreach (ZonaHoraria zona in ZonaHoraria.Admitidas)
        {
            zona.AlContrato().AlDominio().ShouldBeSameAs(zona);
        }
    }

    [Fact]
    public void Las_dos_listas_son_la_misma_sin_repetidos()
    {
        ZonaHoraria[] traducidas = [.. s_delContrato.Select(zona => zona.AlDominio())];

        traducidas.ShouldBeUnique();
        traducidas.ShouldBe(ZonaHoraria.Admitidas, ignoreOrder: true);
    }

    [Theory]
    [InlineData("\"Europe/London\"")]
    [InlineData("\"europe/madrid\"")]
    [InlineData("\"Madrid\"")]
    [InlineData("0")]
    [InlineData("7")]
    public void El_contrato_no_lee_una_zona_que_no_publica(string json)
    {
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<ZonaHorariaDto>(json));
    }
}
