using Bastion.BuildingBlocks.Application.Fechas;
using Bastion.BuildingBlocks.Domain.Fechas;
using Bastion.Pruebas.Comun;
using Shouldly;

namespace Bastion.BuildingBlocks.UnitTests.Fechas;

/// <summary>
/// «Hoy» en la empresa (ADR-0079): el día en la zona de la empresa de quien pregunta, y el más
/// adelantado entre la suya y las de sus almacenes, que es el que dice si una fecha es futura en
/// alguno.
/// </summary>
/// <remarks>
/// Todo va a las 23:30 UTC del 31 de diciembre, que en Madrid es ya el 1 de enero y en Canarias
/// todavía el 31: en ese instante, preguntar a la zona equivocada da otro día, y otro año.
/// </remarks>
public sealed class HoyEnLaEmpresaTests
{
    private static readonly DateTimeOffset s_finDeAnio = new(2026, 12, 31, 23, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Hoy_es_el_dia_en_la_zona_de_la_empresa_de_quien_pregunta()
    {
        ZonasFijas enCanarias = new(ZonaHoraria.Canarias);
        ZonasFijas enMadrid = new(ZonaHoraria.Madrid);

        (await HoyDeLaPrueba.En(enCanarias, Reloj()).DeLaEmpresaAsync(CancellationToken.None))
            .ShouldBe(new DateOnly(2026, 12, 31));
        (await HoyDeLaPrueba.En(enMadrid, Reloj()).DeLaEmpresaAsync(CancellationToken.None))
            .ShouldBe(new DateOnly(2027, 1, 1));

        enCanarias.EmpresasPreguntadas.ShouldBe([HoyDeLaPrueba.Empresa], "la empresa es la de quien pregunta");
        enCanarias.AlmacenesPreguntados.ShouldBeEmpty();
    }

    [Fact]
    public async Task El_mas_adelantado_cuenta_las_zonas_de_los_almacenes()
    {
        // La empresa está en Canarias, y uno de sus almacenes, en la península.
        ZonasFijas zonas = new(
            ZonaHoraria.Canarias, new Dictionary<Guid, ZonaHoraria> { [Guid.NewGuid()] = ZonaHoraria.Madrid });
        IHoyEnLaEmpresa hoy = HoyDeLaPrueba.En(zonas, Reloj());

        (await hoy.DeLaEmpresaAsync(CancellationToken.None)).ShouldBe(new DateOnly(2026, 12, 31));
        (await hoy.ElMasAdelantadoAsync(CancellationToken.None)).ShouldBe(
            new DateOnly(2027, 1, 1), "en el almacén de la península ya es el día siguiente");

        zonas.EmpresasPreguntadas.ShouldBe([HoyDeLaPrueba.Empresa, HoyDeLaPrueba.Empresa]);
    }

    [Fact]
    public async Task Con_una_sola_zona_el_mas_adelantado_es_el_de_la_empresa()
    {
        IHoyEnLaEmpresa hoy = HoyDeLaPrueba.En(ZonaHoraria.Canarias, Reloj());

        (await hoy.ElMasAdelantadoAsync(CancellationToken.None)).ShouldBe(new DateOnly(2026, 12, 31));
    }

    /// <summary>
    /// Un reloj parado a fin de año, con la zona local catorce horas por delante: un «hoy» que la
    /// leyera daría el 1 de enero en todas las filas.
    /// </summary>
    private static RelojParado Reloj() => new(s_finDeAnio);

    private sealed class RelojParado(DateTimeOffset instante) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone { get; } =
            TimeZoneInfo.CreateCustomTimeZone("Catorce horas por delante", TimeSpan.FromHours(14), "X", "X");

        public override DateTimeOffset GetUtcNow() => instante;
    }
}
