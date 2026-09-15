using System.Text;
using Bastion.BuildingBlocks.Application.Importacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Shouldly;

namespace Bastion.BuildingBlocks.UnitTests.Importacion;

/// <summary>
/// El dialecto del ADR-0034 §5, caso por caso: lo que es de él se lee, y lo que no, se rechaza con
/// su nombre.
/// </summary>
/// <remarks>
/// Los ficheros de estos casos están escritos a mano, y por eso aquí no se comprueba que el dialecto
/// sea el que exporta Excel: eso lo hacen los ficheros exportados de verdad, en el carril de
/// integración. Lo que se comprueba aquí es que el lector hace lo que el ADR dice que hace.
/// </remarks>
public sealed class LectorCsvTests
{
    private const string Cabecera = "nombre;importe;activo";

    private static readonly string[] s_cabecera = ["nombre", "importe", "activo"];

    [Fact]
    public void Un_fichero_bien_formado_se_parte_en_filas_y_campos_con_su_linea()
    {
        HojaCsv hoja = Leer($"{Cabecera}\r\nuno;1,5;sí\r\ndos;2;no\r\n");

        hoja.Filas.Select(fila => fila.Linea).ShouldBe([2, 3]);
        hoja.Filas[0].Campos.ShouldBe(["uno", "1,5", "sí"]);
        hoja.Filas[1].Campos.ShouldBe(["dos", "2", "no"]);
        hoja.Filas.ShouldAllBe(fila => fila.CuadraConLaCabecera && fila.ComillasMalColocadas.Count == 0);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void CRLF_y_LF_se_leen_igual_y_la_ultima_fila_no_necesita_fin_de_linea(string fin)
    {
        HojaCsv hoja = Leer($"{Cabecera}{fin}uno;1;sí{fin}dos;2;no");

        hoja.Filas.Select(fila => fila.Campos[0]).ShouldBe(["uno", "dos"]);
    }

    /// <summary>La línea es la de la hoja de cálculo, y aquí se ve por qué no es la del editor.</summary>
    [Fact]
    public void Un_salto_de_linea_entre_comillas_no_suma_linea_y_las_filas_vacias_si()
    {
        HojaCsv hoja = Leer(
            $"{Cabecera}\r\n" +
            "\"primera\r\ncon dos renglones\";1;sí\r\n" +
            ";;\r\n" +
            "\r\n" +
            "tercera;3;no\r\n");

        hoja.Filas.Select(fila => fila.Linea).ShouldBe(
            [2, 5],
            "la tercera fila con datos está en la 5 de la hoja: la 3 y la 4 son vacías, y el salto de " +
            "línea de la 2 es del campo");
        hoja.Filas[0].Campos[0].ShouldBe("primera\r\ncon dos renglones");
    }

    [Fact]
    public void Entre_comillas_caben_el_separador_y_la_comilla_doble()
    {
        HojaCsv hoja = Leer($"{Cabecera}\r\n\"Pérez; hermanos \"\"los de siempre\"\"\";\"1\";\"\"\r\n");

        hoja.Filas[0].Campos.ShouldBe(["Pérez; hermanos \"los de siempre\"", "1", string.Empty]);
        hoja.Filas[0].ComillasMalColocadas.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("ab\"c;1;sí", 0)]
    [InlineData("\"abc\"x;1;sí", 0)]
    [InlineData("abc;1\";sí", 1)]
    public void Una_comilla_fuera_de_sitio_es_de_la_fila_y_no_pierde_donde_empieza_el_campo_siguiente(
        string fila, int campo)
    {
        HojaCsv hoja = Leer($"{Cabecera}\r\n{fila}\r\nbuena;2;no\r\n");

        hoja.Filas[0].ComillasMalColocadas.ShouldBe([campo]);
        hoja.Filas[0].Campos.Count.ShouldBe(3);
        hoja.Filas[1].Campos.ShouldBe(["buena", "2", "no"]);
    }

    [Theory]
    [InlineData("uno;1")]
    [InlineData("uno;1;sí;de más")]
    public void Una_fila_con_otro_numero_de_campos_se_lee_y_se_marca(string fila)
    {
        HojaCsv hoja = Leer($"{Cabecera}\r\n{fila}\r\n");

        hoja.Filas.ShouldHaveSingleItem().CuadraConLaCabecera.ShouldBeFalse();
    }

    [Fact]
    public void Un_retorno_de_carro_suelto_fuera_de_comillas_rechaza_el_fichero()
    {
        ErrorDe($"{Cabecera}\r\nuno;1;sí\rdos;2;no\r\n").Codigo
            .ShouldBe(ErroresDeImportacion.CodigoDeFinDeLineaNoAdmitido);
    }

    [Fact]
    public void Un_retorno_de_carro_suelto_entre_comillas_es_del_campo()
    {
        Leer($"{Cabecera}\r\n\"uno\rdos\";1;sí\r\n").Filas[0].Campos[0].ShouldBe("uno\rdos");
    }

    [Fact]
    public void Unas_comillas_que_no_se_cierran_rechazan_el_fichero()
    {
        ErrorDe($"{Cabecera}\r\nuno;1;sí\r\n\"dos;2;no\r\ntres;3;no\r\n").Codigo
            .ShouldBe(ErroresDeImportacion.CodigoDeComillasSinCerrar);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nombre;importe")]
    [InlineData("nombre;importe;activo;otra")]
    [InlineData("Nombre;Importe;Activo")]
    [InlineData("importe;nombre;activo")]
    [InlineData("\"nombre\"x;importe;activo")]
    public void Una_primera_fila_que_no_es_la_cabecera_tal_cual_rechaza_el_fichero(string primera)
    {
        ErrorDe($"{primera}\r\nuno;1;sí\r\n").Codigo.ShouldBe(ErroresDeImportacion.CodigoDeCabeceraNoValida);
    }

    [Theory]
    [InlineData(',')]
    [InlineData('\t')]
    public void Una_cabecera_que_casaria_con_otro_separador_se_rechaza_diciendolo(char otro)
    {
        string cabecera = string.Join(otro, s_cabecera);

        ErrorDe($"{cabecera}\r\nuno{otro}1{otro}sí\r\n").Codigo
            .ShouldBe(ErroresDeImportacion.CodigoDeSeparadorNoAdmitido);
    }

    /// <summary>
    /// Lo que el ADR avisa: un fichero de una sola columna no casa con ningún separador, así que no se
    /// le puede decir que use otro. Es una cabecera que no vale, y se llama así.
    /// </summary>
    [Fact]
    public void Un_fichero_de_una_sola_columna_es_una_cabecera_que_no_vale_y_no_un_separador()
    {
        ErrorDe("nombre\r\nuno\r\ndos\r\n").Codigo.ShouldBe(ErroresDeImportacion.CodigoDeCabeceraNoValida);
    }

    [Fact]
    public void En_UTF8_con_BOM_y_sin_el_se_lee_lo_mismo()
    {
        byte[] sinBom = Encoding.UTF8.GetBytes($"{Cabecera}\r\nPeña;1;sí\r\n");
        byte[] conBom = [0xEF, 0xBB, 0xBF, .. sinBom];

        Leer(sinBom).Filas[0].Campos.ShouldBe(["Peña", "1", "sí"]);
        Leer(conBom).Filas[0].Campos.ShouldBe(["Peña", "1", "sí"]);
    }

    [Fact]
    public void Lo_que_no_es_UTF8_valido_se_lee_en_Windows_1252()
    {
        // «Peña» y «sí» en Windows-1252: 0xF1 y 0xED van seguidos de una letra, y eso no es UTF-8.
        byte[] ansi = [.. Encoding.ASCII.GetBytes($"{Cabecera}\r\nPe"), 0xF1, .. "a;1;s"u8, 0xED, .. "\r\n"u8];

        Leer(ansi).Filas[0].Campos.ShouldBe(["Peña", "1", "sí"]);
    }

    /// <summary>
    /// El fallo del detector, escrito para que se vea: no es un caso que se quiera así, es el precio
    /// de detectar, y está aquí para que nadie crea que no existe.
    /// </summary>
    [Fact]
    public void El_detector_falla_con_un_Windows_1252_que_por_casualidad_es_UTF8_valido_y_lo_lee_como_UTF8()
    {
        // «Ã±» en Windows-1252 son los bytes 0xC3 0xB1, que en UTF-8 son una «ñ».
        byte[] ansi = [.. Encoding.ASCII.GetBytes($"{Cabecera}\r\nPe"), 0xC3, 0xB1, .. "a;1;no\r\n"u8];

        Leer(ansi).Filas[0].Campos[0].ShouldBe("Peña");
    }

    [Fact]
    public void Con_BOM_de_UTF8_y_bytes_que_no_lo_son_no_se_busca_otra_lectura()
    {
        byte[] mentira = [0xEF, 0xBB, 0xBF, .. Encoding.ASCII.GetBytes($"{Cabecera}\r\nPe"), 0xF1, .. "a;1;no\r\n"u8];

        ErrorDe(mentira).Codigo.ShouldBe(ErroresDeImportacion.CodigoDeCodificacionNoAdmitida);
    }

    public static TheoryData<string, byte[]> CodificacionesAjenas => new()
    {
        { "UTF-16 LE con BOM", [0xFF, 0xFE, .. Encoding.Unicode.GetBytes($"{Cabecera}\r\n")] },
        { "UTF-16 BE con BOM", [0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes($"{Cabecera}\r\n")] },
        { "UTF-32 LE con BOM", [0xFF, 0xFE, 0x00, 0x00, .. Encoding.UTF32.GetBytes($"{Cabecera}\r\n")] },
        { "UTF-32 BE con BOM", [0x00, 0x00, 0xFE, 0xFF, .. new UTF32Encoding(true, false).GetBytes($"{Cabecera}\r\n")] },
        { "UTF-16 LE sin BOM", Encoding.Unicode.GetBytes($"{Cabecera}\r\n") },
        { "un byte nulo en mitad", [.. Encoding.ASCII.GetBytes($"{Cabecera}\r\nuno;1"), 0x00, .. ";sí\r\n"u8] },
    };

    [Theory]
    [MemberData(nameof(CodificacionesAjenas))]
    public void Lo_que_no_escribe_una_hoja_en_espanol_se_rechaza_como_codificacion_no_admitida(
        string caso, byte[] bytes)
    {
        ErrorDe(bytes).Codigo.ShouldBe(ErroresDeImportacion.CodigoDeCodificacionNoAdmitida, caso);
    }

    [Fact]
    public void El_tope_de_filas_cuenta_las_vacias_y_admite_justo_el_tope()
    {
        const int Tope = 4;

        HojaCsv justo = Leer($"{Cabecera}\r\nuno;1;sí\r\n;;\r\n\r\ncuatro;4;no\r\n", Tope);
        ErrorDeOperacion sobra = ErrorDe($"{Cabecera}\r\nuno;1;sí\r\n;;\r\n\r\ncuatro;4;no\r\n;;\r\n", Tope);

        justo.Filas.Count.ShouldBe(2);
        sobra.Codigo.ShouldBe(ErroresDeImportacion.CodigoDeDemasiadasFilas);
        sobra.Tipo.ShouldBe(TipoDeError.DemasiadoGrande);
    }

    [Fact]
    public void Ningun_error_de_fichero_repite_lo_que_traia_el_fichero()
    {
        const string Canario = "CANARIO-DEL-LECTOR";

        string[] ficheros =
        [
            $"{Canario};importe;activo\r\n",
            $"{Canario},importe,activo\r\n",
            $"{Cabecera}\r\n{Canario}\r",
            $"{Cabecera}\r\n\"{Canario};1;sí\r\n",
        ];

        foreach (string fichero in ficheros)
        {
            ErrorDeOperacion error = ErrorDe(fichero);
            error.Mensaje.ShouldNotContain(Canario);
            error.Codigo.ShouldNotContain(Canario);
        }
    }

    private static HojaCsv Leer(string texto, int tope = 100) => Leer(Encoding.UTF8.GetBytes(texto), tope);

    private static HojaCsv Leer(byte[] bytes, int tope = 100)
    {
        Resultado<HojaCsv> hoja = LectorCsv.Leer(bytes, s_cabecera, tope);

        hoja.EsCorrecto.ShouldBeTrue(hoja.Error?.Codigo);
        return hoja.Valor;
    }

    private static ErrorDeOperacion ErrorDe(string texto, int tope = 100) => ErrorDe(Encoding.UTF8.GetBytes(texto), tope);

    private static ErrorDeOperacion ErrorDe(byte[] bytes, int tope = 100)
    {
        Resultado<HojaCsv> hoja = LectorCsv.Leer(bytes, s_cabecera, tope);

        hoja.EsCorrecto.ShouldBeFalse();
        return hoja.Error!;
    }
}
