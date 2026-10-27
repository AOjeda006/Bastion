using Bastion.BuildingBlocks.Application.Importacion;
using Shouldly;

namespace Bastion.BuildingBlocks.UnitTests.Importacion;

/// <summary>
/// La regla del apóstrofo en los dos sentidos: lo que la exportación pone, la importación lo quita,
/// y nada más (ADR-0075).
/// </summary>
public sealed class ApostrofoDelDialectoTests
{
    /// <summary>
    /// Los caracteres con los que se construye el universo de la vuelta: los siete disparadores, una
    /// letra y una cifra, que no lo son.
    /// </summary>
    private static readonly char[] s_alfabeto = ['=', '+', '-', '@', '\t', '\r', '\'', 'a', '1'];

    [Theory]
    [InlineData("=SUMA(A1)", "'=SUMA(A1)")]
    [InlineData("+34 600", "'+34 600")]
    [InlineData("-3", "'-3")]
    [InlineData("@Home", "'@Home")]
    [InlineData("\tdentro", "'\tdentro")]
    [InlineData("\rdentro", "'\rdentro")]
    [InlineData("'=ya", "''=ya")]
    [InlineData("'hola", "''hola")]
    [InlineData("'", "''")]
    public void Lo_que_empieza_por_uno_de_los_siete_sale_con_un_apostrofo_y_vuelve(string texto, string escrito)
    {
        ApostrofoDelDialecto.Poner(texto).ShouldBe(escrito);
        ApostrofoDelDialecto.Quitar(escrito).ShouldBe(texto);
    }

    /// <summary>La pareja del de arriba: lo que no empieza por uno de los siete sale tal cual.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("a=b")]
    [InlineData("x-")]
    [InlineData("Sin nada raro")]
    public void Lo_demas_sale_y_vuelve_tal_cual(string texto)
    {
        ApostrofoDelDialecto.Poner(texto).ShouldBe(texto);
        ApostrofoDelDialecto.Quitar(texto).ShouldBe(texto);
    }

    /// <summary>
    /// Un apóstrofo que no lo sigue uno de los siete no lo puso la exportación: lo escribió alguien,
    /// y es suyo.
    /// </summary>
    [Theory]
    [InlineData("'0012")]
    [InlineData("'hola")]
    [InlineData("'")]
    public void Un_apostrofo_que_no_puso_la_exportacion_se_queda(string campo) =>
        ApostrofoDelDialecto.Quitar(campo).ShouldBe(campo);

    /// <summary>
    /// La vuelta, en todo un universo: cada texto de hasta cuatro caracteres del alfabeto sale y
    /// vuelve igual. Es lo que la regla promete, y lo que un disparador de menos rompería.
    /// </summary>
    [Fact]
    public void Todo_texto_sale_y_vuelve_igual()
    {
        List<string> universo = [string.Empty];

        for (int largo = 1; largo <= 4; largo++)
        {
            universo.AddRange(
                universo.Where(texto => texto.Length == largo - 1)
                    .SelectMany(texto => s_alfabeto.Select(caracter => texto + caracter))
                    .ToList());
        }

        // EL ARNÉS: el universo tiene lo que se cree que tiene, de 1 + 9 + 81 + 729 + 6561.
        universo.Count.ShouldBe(7381);

        List<string> rotos =
        [
            .. universo.Where(texto => ApostrofoDelDialecto.Quitar(ApostrofoDelDialecto.Poner(texto)) != texto),
        ];

        rotos.ShouldBeEmpty(string.Join(", ", rotos.Take(10).Select(texto => $"«{texto}»")));
    }
}
