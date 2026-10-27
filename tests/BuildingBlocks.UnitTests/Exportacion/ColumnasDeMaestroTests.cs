using Bastion.BuildingBlocks.Application.Exportacion;
using Bastion.BuildingBlocks.Contracts.Maestros;
using Shouldly;

namespace Bastion.BuildingBlocks.UnitTests.Exportacion;

/// <summary>
/// Las dos columnas de un maestro: el código y el nombre si se ha nombrado, el identificador si no,
/// y dos campos vacíos si la fila no apunta a ninguno (ADR-0074).
/// </summary>
public sealed class ColumnasDeMaestroTests
{
    private static readonly Guid s_id = Guid.Parse("0193a7e2-5b1c-7d4e-8f90-123456789abc");

    private static readonly IReadOnlyList<ColumnaCsv<Fila>> s_columnas =
        ColumnasDeMaestro.De<Fila>("unidad_base", fila => fila.Maestro);

    [Fact]
    public void Las_columnas_son_el_codigo_y_el_nombre_con_el_prefijo()
    {
        s_columnas.Select(columna => columna.Nombre).ShouldBe(["unidad_base_codigo", "unidad_base_nombre"]);
    }

    [Fact]
    public void Un_maestro_nombrado_sale_con_su_codigo_y_su_nombre_como_texto()
    {
        Fila fila = new(new MaestroCitado(s_id, new NombreDeMaestro("=KG", "+Kilogramo")));

        Escritos(fila).ShouldBe(
            ["'=KG", "'+Kilogramo"], "el código y el nombre los escribe una persona: llevan la regla del apóstrofo");
    }

    [Fact]
    public void Un_maestro_sin_nombre_sale_con_su_codigo_y_el_nombre_vacio()
    {
        Escritos(new Fila(new MaestroCitado(s_id, new NombreDeMaestro("A-01", null)))).ShouldBe(["A-01", string.Empty]);
    }

    [Fact]
    public void Un_maestro_sin_nombrar_sale_con_su_identificador_y_sin_apostrofo()
    {
        Fila fila = new(new MaestroCitado(s_id, null));

        s_columnas[0].Campo(fila).EsTexto.ShouldBeFalse("el identificador no lo escribió una persona");
        Escritos(fila).ShouldBe(["0193a7e2-5b1c-7d4e-8f90-123456789abc", string.Empty]);
    }

    [Fact]
    public void Una_fila_que_no_apunta_a_ninguno_sale_con_los_dos_campos_vacios()
    {
        Escritos(new Fila(null)).ShouldBe([string.Empty, string.Empty]);
    }

    [Fact]
    public void El_maestro_toma_su_nombre_de_los_que_han_vuelto_y_sin_identificador_no_hay_maestro()
    {
        var otro = Guid.CreateVersion7();
        Dictionary<Guid, NombreDeMaestro> nombres = new() { [s_id] = new NombreDeMaestro("KG", "Kilogramo") };

        MaestroCitado.De(s_id, nombres).ShouldBe(new MaestroCitado(s_id, new NombreDeMaestro("KG", "Kilogramo")));
        MaestroCitado.De(otro, nombres).ShouldBe(new MaestroCitado(otro, null));
        MaestroCitado.De((Guid?)s_id, nombres)
            .ShouldBe(new MaestroCitado(s_id, new NombreDeMaestro("KG", "Kilogramo")));
        MaestroCitado.De((Guid?)null, nombres).ShouldBeNull();
    }

    private static string[] Escritos(Fila fila) =>
        [.. s_columnas.Select(columna => EscritorCsv.Escribir(columna.Campo(fila)))];

    private sealed record Fila(MaestroCitado? Maestro);
}
