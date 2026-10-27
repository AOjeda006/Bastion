using System.Globalization;
using System.Text;
using Bastion.BuildingBlocks.Application.Exportacion;
using Bastion.BuildingBlocks.Application.Importacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Shouldly;

namespace Bastion.BuildingBlocks.UnitTests.Exportacion;

/// <summary>
/// El fichero que sale de una exportación: el dialecto de la importación (ADR-0034 §5), con su BOM
/// y su CRLF, la regla del apóstrofo solo en el texto, y una página escrita antes de pedir la
/// siguiente (ADR-0073).
/// </summary>
/// <remarks>
/// <b>La prueba de que el dialecto es el de la importación la da el lector de la importación</b>:
/// lo que escribe el escritor, <see cref="LectorCsv"/> lo parte en los mismos campos y
/// <see cref="CamposCsv"/> lee los mismos números. Si alguien cambia uno de los dos lados, el
/// fichero exportado deja de volver a entrar, y es aquí donde se pone rojo.
/// </remarks>
public sealed class EscritorCsvTests
{
    private static readonly HojaDeExportacion<Cosa> s_hoja = new(
        "cosas",
        [
            new("codigo", cosa => CampoCsv.Texto(cosa.Codigo)),
            new("importe", cosa => CampoCsv.Numero(cosa.Importe)),
            new("activa", cosa => CampoCsv.SiNo(cosa.Activa)),
            new("alta", cosa => CampoCsv.Fecha(cosa.Alta)),
        ]);

    [Fact]
    public async Task El_fichero_es_el_bom_la_cabecera_y_una_linea_por_elemento_con_crlf()
    {
        byte[] fichero = await EscribirAsync(
            [new Cosa("A-1", 1.5m, true, new DateOnly(2026, 10, 27))],
            [new Cosa("B", -2m, false, null)]);

        fichero[..3].ShouldBe(EscritorCsv.Bom.ToArray(), "el BOM de UTF-8, que es el que Excel necesita");
        Encoding.UTF8.GetString(fichero[3..]).ShouldBe(
            "codigo;importe;activa;alta\r\n" +
            "A-1;1,5;sí;2026-10-27\r\n" +
            "B;-2;no;\r\n");
    }

    [Fact]
    public async Task Sin_elementos_el_fichero_es_el_bom_y_la_cabecera()
    {
        byte[] fichero = await EscribirAsync([]);

        Encoding.UTF8.GetString(fichero[3..]).ShouldBe("codigo;importe;activa;alta\r\n");
    }

    [Theory]
    [InlineData("1234.5600", "1234,56")]
    [InlineData("-12.5", "-12,5")]
    [InlineData("1000000", "1000000")]
    [InlineData("0.000001", "0,000001")]
    [InlineData("0.0000", "0")]
    [InlineData("-0.0", "0")]
    [InlineData("12.0", "12")]
    public void Un_numero_sale_con_coma_decimal_sin_miles_y_sin_ceros_a_la_derecha(string numero, string escrito) =>
        EscritorCsv.Escribir(CampoCsv.Numero(decimal.Parse(numero, CultureInfo.InvariantCulture))).ShouldBe(escrito);

    [Theory]
    [InlineData("=SUMA(A1)", "'=SUMA(A1)")]
    [InlineData("+34 600", "'+34 600")]
    [InlineData("-3", "'-3")]
    [InlineData("@usuario", "'@usuario")]
    [InlineData("\tdentro", "'\tdentro")]
    [InlineData("\rdentro", "\"'\rdentro\"")]
    public void Un_texto_que_parece_una_formula_sale_con_un_apostrofo_delante(string texto, string escrito) =>
        EscritorCsv.Escribir(CampoCsv.Texto(texto)).ShouldBe(escrito);

    /// <summary>
    /// Las parejas del de arriba: el mismo carácter en otro sitio, y lo que no es texto, que lo
    /// escribe Bastion y no lleva apóstrofo aunque empiece igual.
    /// </summary>
    [Fact]
    public void El_apostrofo_solo_lo_lleva_el_texto_que_empieza_por_uno_de_los_seis()
    {
        EscritorCsv.Escribir(CampoCsv.Texto("a=b")).ShouldBe("a=b");
        EscritorCsv.Escribir(CampoCsv.Texto("x-")).ShouldBe("x-");
        EscritorCsv.Escribir(CampoCsv.Texto(string.Empty)).ShouldBe(string.Empty);
        EscritorCsv.Escribir(CampoCsv.Texto(null)).ShouldBe(string.Empty);
        EscritorCsv.Escribir(CampoCsv.Numero(-3m)).ShouldBe("-3", "un número negativo lo escribe Bastion");
        EscritorCsv.Escribir(CampoCsv.Numero(-3L)).ShouldBe("-3");
    }

    [Theory]
    [InlineData("a;b", "\"a;b\"")]
    [InlineData("dice \"hola\"", "\"dice \"\"hola\"\"\"")]
    [InlineData("dos\r\nlíneas", "\"dos\r\nlíneas\"")]
    [InlineData("una\nlínea", "\"una\nlínea\"")]
    [InlineData("sin nada raro", "sin nada raro")]
    public void Un_campo_va_entre_comillas_si_lleva_separador_comilla_o_salto(string texto, string escrito) =>
        EscritorCsv.Escribir(CampoCsv.Texto(texto)).ShouldBe(escrito);

    [Fact]
    public void Los_demas_tipos_salen_en_la_forma_del_dialecto()
    {
        EscritorCsv.Escribir(CampoCsv.SiNo(true)).ShouldBe("sí");
        EscritorCsv.Escribir(CampoCsv.SiNo(false)).ShouldBe("no");
        EscritorCsv.Escribir(CampoCsv.SiNo(null)).ShouldBe(string.Empty);
        EscritorCsv.Escribir(CampoCsv.Fecha(new DateOnly(2026, 1, 5))).ShouldBe("2026-01-05");
        EscritorCsv.Escribir(CampoCsv.Numero(2026L)).ShouldBe("2026");
        EscritorCsv.Escribir(CampoCsv.Identificador(Guid.Parse("0199a0b1-2c3d-7e4f-8a9b-0c1d2e3f4a5b")))
            .ShouldBe("0199a0b1-2c3d-7e4f-8a9b-0c1d2e3f4a5b");
        EscritorCsv.Escribir(CampoCsv.Valor<DayOfWeek>(DayOfWeek.Monday)).ShouldBe("Monday");
        EscritorCsv.Escribir(CampoCsv.Valor<DayOfWeek>(null)).ShouldBe(string.Empty);
    }

    /// <summary>
    /// Lo que se exporta vuelve a entrar: el lector de la importación lo parte en los mismos campos,
    /// y lee los mismos números y los mismos sí y no.
    /// </summary>
    [Fact]
    public async Task El_lector_de_la_importacion_lee_lo_que_escribe_el_escritor()
    {
        Cosa[] cosas =
        [
            new("con;punto y coma", 1234.5678m, true, new DateOnly(2026, 10, 27)),
            new("con \"comillas\"", -0.5m, false, null),
            new("en dos\r\nlíneas, con eñe y tildes: áéíóú", 0m, true, new DateOnly(2025, 2, 28)),
            new("=FORMULA()", 99999999999999.9999m, false, null),
        ];

        Resultado<HojaCsv> leida = LectorCsv.Leer(
            await EscribirAsync(cosas), [.. s_hoja.Columnas.Select(columna => columna.Nombre)], topeDeFilas: 10);

        leida.EsCorrecto.ShouldBeTrue(leida.Error?.Mensaje);
        leida.Valor.Filas.Count.ShouldBe(cosas.Length);

        for (int i = 0; i < cosas.Length; i++)
        {
            FilaCsv fila = leida.Valor.Filas[i];
            Cosa cosa = cosas[i];

            fila.CuadraConLaCabecera.ShouldBeTrue();
            fila.ComillasMalColocadas.ShouldBeEmpty();
            fila.Campos[0].ShouldBe(cosa.Codigo!.StartsWith('=') ? "'" + cosa.Codigo : cosa.Codigo);

            CamposCsv.IntentarLeerImporte(fila.Campos[1], out decimal importe).ShouldBeTrue(fila.Campos[1]);
            importe.ShouldBe(cosa.Importe!.Value);

            CamposCsv.IntentarLeerSiNo(fila.Campos[2], out bool activa).ShouldBeTrue(fila.Campos[2]);
            activa.ShouldBe(cosa.Activa!.Value);

            fila.Campos[3].ShouldBe(cosa.Alta?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty);
        }
    }

    /// <summary>
    /// En flujo: cuando se pide la página siguiente, la anterior ya está en la corriente. Si el
    /// escritor juntara las páginas antes de escribir, la segunda se pediría con la corriente vacía.
    /// </summary>
    [Fact]
    public async Task Cada_pagina_esta_en_la_corriente_antes_de_pedir_la_siguiente()
    {
        using MemoryStream destino = new();
        List<string> vistoAlPedir = [];

        await EscritorCsv.EscribirAsync(destino, s_hoja, Paginas(), CancellationToken.None);

        vistoAlPedir.Count.ShouldBe(2, "el arnés: se han pedido la segunda y la tercera");
        vistoAlPedir[0].ShouldEndWith("primera;1;sí;\r\n");
        vistoAlPedir[1].ShouldEndWith("segunda;2;no;\r\n");

        async IAsyncEnumerable<IReadOnlyList<Cosa>> Paginas()
        {
            yield return [new Cosa("primera", 1m, true, null)];

            vistoAlPedir.Add(Encoding.UTF8.GetString(destino.ToArray()));
            await Task.Yield();
            yield return [new Cosa("segunda", 2m, false, null)];

            vistoAlPedir.Add(Encoding.UTF8.GetString(destino.ToArray()));
            yield return [];
        }
    }

    private static async Task<byte[]> EscribirAsync(params IReadOnlyList<Cosa>[] paginas)
    {
        using MemoryStream destino = new();

        await EscritorCsv.EscribirAsync(destino, s_hoja, Paginas(paginas), CancellationToken.None);

        return destino.ToArray();
    }

    private static async IAsyncEnumerable<IReadOnlyList<Cosa>> Paginas(IReadOnlyList<Cosa>[] paginas)
    {
        foreach (IReadOnlyList<Cosa> pagina in paginas)
        {
            await Task.Yield();
            yield return pagina;
        }
    }

    private sealed record Cosa(string? Codigo, decimal? Importe, bool? Activa, DateOnly? Alta);
}
