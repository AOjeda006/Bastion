using Bastion.Catalogo.Domain.Catalogo;
using Shouldly;

namespace Bastion.Catalogo.UnitTests.Catalogo;

/// <summary>
/// El GTIN como objeto de valor: cómo se normaliza, cuándo cuadra su dígito de control y qué
/// prefijos no son de un artículo (ADR-0051 §1 y §4, y el ADR-0052 §2, que enmienda la tabla).
/// </summary>
/// <remarks>
/// <para>
/// <b>Los números de los casos están calculados, no copiados.</b> Cada uno lleva el dígito de control
/// que le toca por la §7.9.1 de las <i>GS1 General Specifications</i>, salvo los que prueban
/// justo eso. Un caso de prefijo con el control mal se rechazaría por el control, y diría verde sin
/// haber llegado a leer el prefijo.
/// </para>
/// <para>
/// <b>Cada fila de la tabla de prefijos va con un vecino admitido</b>, el número de al lado que sí es
/// de un artículo. Sin él, una línea que rechazara de más saldría verde: el caso que comprueba el
/// rechazo no ve lo que se rechaza sin motivo.
/// </para>
/// </remarks>
public sealed class ElGtinTests
{
    // ------------------------------------------------------------------------ la normalización

    [Theory]
    [InlineData("001234567895", "00001234567895")]
    [InlineData("036000291452", "00036000291452")]
    [InlineData("96385074", "00000096385074")]
    [InlineData("4006381333931", "04006381333931")]
    [InlineData("10012345678902", "10012345678902")]
    public void Cada_largo_admitido_se_guarda_en_catorce_con_ceros_a_la_izquierda(
        string texto, string catorce)
    {
        LecturaDeGtin lectura = Gtin.Leer(texto);

        lectura.EsGtin.ShouldBeTrue($"«{texto}» es un GTIN con su control bien");
        lectura.Gtin.Valor.ShouldBe(
            catorce, "los ceros de delante son parte del prefijo y no se pierden");
    }

    [Fact]
    public void El_mismo_GTIN_con_doce_y_con_trece_digitos_da_la_misma_cadena()
    {
        var doce = Gtin.De("036000291452");
        var trece = Gtin.De("0036000291452");

        trece.ShouldBe(doce, "el índice único compara la forma de catorce, y tiene que ser una sola");
        trece.Valor.ShouldBe(doce.Valor);
        trece.ToString().ShouldBe("00036000291452", "interpolado en un texto, también es la forma de catorce");
    }

    [Fact]
    public void Se_recortan_los_espacios_de_los_extremos_y_nada_mas()
    {
        Gtin.Leer("  4006381333931 \t").Gtin.Valor.ShouldBe("04006381333931");
        Gtin.Leer(" 4006381333931").Gtin.Valor.ShouldBe("04006381333931");
        Gtin.Leer("4006381333931\r\n").Gtin.Valor.ShouldBe(
            "04006381333931", "el lector que hace de teclado termina cada lectura con un Intro");

        Gtin.Leer("4006381 333931").Motivo.ShouldBe(
            MotivoDeRechazoDelGtin.NoSonDigitos, "un espacio por dentro se rechaza, no se quita");
        Gtin.Leer("400-6381333931").Motivo.ShouldBe(MotivoDeRechazoDelGtin.NoSonDigitos);
    }

    [Theory]
    [InlineData("40063813339A1")]
    [InlineData("ABCDEFGHIJKLM")]
    [InlineData("12-34")]
    [InlineData("ABC")]
    [InlineData("٤٠٠٦٣٨١٣٣٣٩٣١")]
    [InlineData("４００６３８１３３３９３１")]
    public void Lo_que_no_son_digitos_ASCII_se_rechaza_aunque_sean_cifras_de_otra_escritura(string texto)
    {
        // «12-34» y «ABC» tampoco tienen un largo admitido: lo que no son cifras se dice antes que
        // el largo, que en un texto así no significa nada. Los dos últimos son el 4006381333931 en
        // cifras arábigas orientales y de ancho completo.
        // `char.IsDigit` los daría por buenos, y un GTIN que se guarda como texto tiene que ser
        // el mismo texto que imprime el lector.
        Gtin.Leer(texto).Motivo.ShouldBe(MotivoDeRechazoDelGtin.NoSonDigitos);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("123456")]
    [InlineData("1234567")]
    [InlineData("123456789")]
    [InlineData("1234567890")]
    [InlineData("36000291452")]
    [InlineData("100123456789023")]
    [InlineData("0110012345678902")]
    [InlineData("001234567000000017")]
    public void Un_largo_que_no_es_8_12_13_ni_14_se_rechaza(string? texto)
    {
        // El de 11 es el UPC del 036000291452 sin su cero de delante. No se le pone: once dígitos
        // no son un GTIN-12 al que le falta uno, son un número que alguien tiene que mirar. Los dos
        // últimos son lo que da un lector sin analizar el GS1-128: el AI 01 delante de un GTIN-14,
        // y un SSCC de 18, con su control bien. Ninguno se recorta a catorce.
        Gtin.Leer(texto).Motivo.ShouldBe(MotivoDeRechazoDelGtin.LargoNoAdmitido);
    }

    // ------------------------------------------------------------------- el dígito de control

    [Theory]
    [InlineData("96385075")]
    [InlineData("036000291453")]
    [InlineData("4006381333932")]
    [InlineData("10012345678903")]
    [InlineData("4003681333931")]
    public void Un_digito_de_control_que_no_cuadra_se_rechaza_en_cada_largo(string texto)
    {
        // El último es el 4006381333931 con dos cifras cambiadas de sitio.
        Gtin.Leer(texto).Motivo.ShouldBe(MotivoDeRechazoDelGtin.DigitoDeControl);
    }

    [Fact]
    public void De_un_texto_que_no_es_un_GTIN_lanza()
    {
        Should.Throw<ArgumentException>(() => Gtin.De("4006381333932"));
    }

    [Fact]
    public void La_lectura_no_deja_pedir_lo_que_no_tiene()
    {
        Should.Throw<InvalidOperationException>(() => Gtin.Leer("4006381333932").Gtin);
        Should.Throw<InvalidOperationException>(() => Gtin.Leer("4006381333931").Motivo);
    }

    // -------------------------------------------------------------------------- los prefijos

    [Theory]
    // Circulación restringida, tabla 1-4: 0000000, 02, 04 y del 20 al 29.
    [InlineData("0000000123457", MotivoDeRechazoDelGtin.CirculacionRestringida)]
    [InlineData("0212345678909", MotivoDeRechazoDelGtin.CirculacionRestringida)]
    [InlineData("0412345678903", MotivoDeRechazoDelGtin.CirculacionRestringida)]
    [InlineData("2012345678903", MotivoDeRechazoDelGtin.CirculacionRestringida)]
    [InlineData("2912345678906", MotivoDeRechazoDelGtin.CirculacionRestringida)]
    // Los mismos 02 y 04 escritos como U.P.C. de doce, que es como llegan del lector americano.
    [InlineData("212345678909", MotivoDeRechazoDelGtin.CirculacionRestringida)]
    [InlineData("412345678903", MotivoDeRechazoDelGtin.CirculacionRestringida)]
    // Circulación restringida, §2.1.11.3 y su figura 2-1: los LAC y los RZSC, RCN-12 del U.P.C. 0
    // que se imprimen en UPC-E. Los extremos de cada versión de la figura.
    [InlineData("001000000052", MotivoDeRechazoDelGtin.CirculacionRestringida)]
    [InlineData("007999000097", MotivoDeRechazoDelGtin.CirculacionRestringida)]
    [InlineData("001000001004", MotivoDeRechazoDelGtin.CirculacionRestringida)]
    [InlineData("005000009992", MotivoDeRechazoDelGtin.CirculacionRestringida)]
    // Circulación restringida, tabla 1-5: del 000 al 099 y del 200 al 299 en un GTIN-8.
    [InlineData("01234565", MotivoDeRechazoDelGtin.CirculacionRestringida)]
    [InlineData("09912342", MotivoDeRechazoDelGtin.CirculacionRestringida)]
    [InlineData("20012342", MotivoDeRechazoDelGtin.CirculacionRestringida)]
    [InlineData("29912346", MotivoDeRechazoDelGtin.CirculacionRestringida)]
    // Medida variable: un GTIN-14 con indicador 9 (§2.1.10). Lo de detrás es un 84 de España.
    [InlineData("98412345678908", MotivoDeRechazoDelGtin.MedidaVariable)]
    // Y gana al prefijo de detrás: el 02 y el 980 no se leen, porque no es un GTIN por partes.
    [InlineData("90212345678902", MotivoDeRechazoDelGtin.MedidaVariable)]
    [InlineData("99801234567895", MotivoDeRechazoDelGtin.MedidaVariable)]
    // Cupones y vales: 980, del 981 al 983 y el 99.
    [InlineData("9801234567892", MotivoDeRechazoDelGtin.Cupon)]
    [InlineData("9811234567891", MotivoDeRechazoDelGtin.Cupon)]
    [InlineData("9831234567899", MotivoDeRechazoDelGtin.Cupon)]
    [InlineData("9901234567899", MotivoDeRechazoDelGtin.Cupon)]
    [InlineData("9912345678909", MotivoDeRechazoDelGtin.Cupon)]
    // Sin asignar: 951 y del 984 al 989; en un GTIN-8, del 977 al 999.
    [InlineData("9511234567890", MotivoDeRechazoDelGtin.SinAsignar)]
    [InlineData("9841234567898", MotivoDeRechazoDelGtin.SinAsignar)]
    [InlineData("9891234567893", MotivoDeRechazoDelGtin.SinAsignar)]
    [InlineData("97712343", MotivoDeRechazoDelGtin.SinAsignar)]
    [InlineData("99912345", MotivoDeRechazoDelGtin.SinAsignar)]
    public void Cada_fila_de_la_tabla_de_prefijos_rechaza_con_su_motivo(
        string texto, MotivoDeRechazoDelGtin motivo)
    {
        Gtin.Leer(texto).Motivo.ShouldBe(motivo, $"«{texto}» tiene forma de GTIN y no es de un artículo");
    }

    [Theory]
    // Los vecinos de cada fila, que son GTIN de artículo.
    [InlineData("0000123456784")]
    [InlineData("0112345678902")]
    [InlineData("0312345678906")]
    [InlineData("1912345678907")]
    [InlineData("3012345678902")]
    [InlineData("8412345678905")]
    [InlineData("9501234567891")]
    [InlineData("9531234567898")]
    [InlineData("9761234567899")]
    [InlineData("10012345")]
    [InlineData("19912349")]
    [InlineData("30012349")]
    [InlineData("97612346")]
    [InlineData("88412345678901")]
    [InlineData("18412345678902")]
    // Los vecinos de los LAC y los RZSC: la undécima en 4, la décima en 1, la tercera en 6 y en 8,
    // la novena en 0 y la cuarta en 1. Cada uno se sale de la figura por una sola posición.
    [InlineData("001000000045")]
    [InlineData("007999000103")]
    [InlineData("006000005007")]
    [InlineData("008000000051")]
    [InlineData("001000000991")]
    [InlineData("001100001003")]
    // El GTIN-8 951, que la tabla 1-5 da para GTIN-8 aunque la 1-4 lo retire en trece. Suelto, y
    // con indicador 1.
    [InlineData("95112343")]
    [InlineData("10000095112340")]
    // El GTIN-8 100, escrito en trece y con indicador 3: su tabla es la 1-5 también ahí.
    [InlineData("0000010012345")]
    [InlineData("30000010012346")]
    // Los que el ADR-0051 §4 admite a propósito: 952 de demostración, 977 ISSN, 978 y 979 ISBN e
    // ISMN, y el 05, que la tabla 1-4 da para empresas aunque la 1-6 diga reservado el U.P.C. 5.
    [InlineData("9521234567899")]
    [InlineData("95212340")]
    [InlineData("9771234567898")]
    [InlineData("9781234567897")]
    [InlineData("9791234567896")]
    [InlineData("0512345678900")]
    [InlineData("512345678900")]
    public void Los_vecinos_de_cada_fila_y_los_admitidos_a_proposito_son_GTIN_de_articulo(string texto)
    {
        LecturaDeGtin lectura = Gtin.Leer(texto);

        lectura.EsGtin.ShouldBeTrue(
            $"«{texto}» es de un artículo, y se rechazó por {(lectura.EsGtin ? "nada" : lectura.Motivo.ToString())}");
    }

    [Theory]
    [InlineData("10212345678906", MotivoDeRechazoDelGtin.CirculacionRestringida)]
    [InlineData("30000097712344", MotivoDeRechazoDelGtin.SinAsignar)]
    [InlineData("10000097712340", MotivoDeRechazoDelGtin.SinAsignar)]
    public void El_prefijo_se_lee_detras_del_indicador_de_un_GTIN_14(string texto, MotivoDeRechazoDelGtin motivo)
    {
        // El primero es un 02 con indicador 1. Los otros dos llevan un GTIN-8 977 detrás de su
        // indicador, que se lee con la tabla 1-5 y no con la 1-4.
        Gtin.Leer(texto).Motivo.ShouldBe(motivo);
    }

    [Theory]
    [InlineData("0000001234565", MotivoDeRechazoDelGtin.CirculacionRestringida)]
    [InlineData("0000097712343", MotivoDeRechazoDelGtin.SinAsignar)]
    public void Un_GTIN_8_escrito_en_trece_se_lee_con_su_tabla(string texto, MotivoDeRechazoDelGtin motivo)
    {
        // La tabla 1-4 reserva del 0000001 al 0000099 para que un GTIN-8 rellenado no choque con
        // nadie. Escrito en trece, el 01234565 y el 97712343 siguen siendo los mismos GTIN-8.
        Gtin.Leer(texto).Motivo.ShouldBe(motivo);
    }

    [Fact]
    public void El_enumerado_tiene_exactamente_los_siete_motivos()
    {
        // Cada motivo tiene su `type` en la API y su texto en la pantalla. Uno nuevo es una fila
        // más en el ADR-0051 §4, en el catálogo de errores y en los dos diccionarios.
        Enum.GetNames<MotivoDeRechazoDelGtin>().ShouldBe(
            [
                "NoSonDigitos",
                "LargoNoAdmitido",
                "DigitoDeControl",
                "CirculacionRestringida",
                "MedidaVariable",
                "Cupon",
                "SinAsignar",
            ],
            Case.Sensitive);
    }

    // ------------------------------------------------------------------------- el aleatorio

    /// <summary>
    /// Lo que entra por la puerta es texto de fuera: <see cref="Gtin.Leer"/> no lanza nunca, y lo
    /// que da por bueno cumple todo lo que el GTIN promete.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La semilla es fija, para que un rojo se pueda repetir. El oráculo del control es otra cuenta:
    /// de izquierda a derecha sobre los catorce, incluido el propio control, con peso 3 en las
    /// posiciones pares, y la suma tiene que acabar en cero. Si la del objeto de valor y esta
    /// fueran la misma, un error en las dos daría verde.
    /// </para>
    /// <para>
    /// La mitad de las entradas son dígitos con el largo bueno y el control corregido, porque si
    /// todo fuera ruido no se llegaría nunca a leer un prefijo. Al final se exige haber visto todos
    /// los motivos y GTIN válidos: un aleatorio que no ve nada sale verde igual.
    /// </para>
    /// </remarks>
    [Fact]
    public void Ningun_texto_hace_lanzar_a_la_lectura_y_lo_que_admite_cumple_lo_que_promete()
    {
        var aleatorio = new Random(2010);
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        const string Ruido = "0123456789 -A\t٣５";
        int[] largos = [8, 12, 13, 14];

        for (int vuelta = 0; vuelta < 20_000; vuelta++)
        {
            string texto = vuelta % 2 == 0
                ? Ruidoso(aleatorio, Ruido)
                : ConControl(Digitos(aleatorio, largos[aleatorio.Next(largos.Length)]));

            LecturaDeGtin lectura = Should.NotThrow(() => Gtin.Leer(texto), $"«{texto}»");

            if (!lectura.EsGtin)
            {
                vistos.Add(lectura.Motivo.ToString());
                Should.Throw<ArgumentException>(() => Gtin.De(texto), $"«{texto}»");
                continue;
            }

            vistos.Add("valido");
            string valor = lectura.Gtin.Valor;
            valor.Length.ShouldBe(Gtin.Longitud, $"«{texto}»");
            valor.All(char.IsAsciiDigit).ShouldBeTrue($"«{texto}»");
            CuadraElControl(valor).ShouldBeTrue($"«{texto}» se admitió con el control mal");
            valor.EndsWith(texto.Trim(), StringComparison.Ordinal).ShouldBeTrue($"«{texto}»");
            valor[..^texto.Trim().Length].ShouldAllBe(caracter => caracter == '0');
            Gtin.De(valor).ShouldBe(lectura.Gtin, $"«{texto}»: leer lo leído da lo mismo");
        }

        vistos.ShouldBe(
            ["valido", .. Enum.GetNames<MotivoDeRechazoDelGtin>()],
            ignoreOrder: true,
            "el aleatorio tiene que haber pasado por todos los caminos, o no ha probado nada");
    }

    private static bool CuadraElControl(string catorce) =>
        catorce.Select((caracter, posicion) => (caracter - '0') * (posicion % 2 == 0 ? 3 : 1)).Sum() % 10 == 0;

    private static string Digitos(Random aleatorio, int largo) =>
        string.Concat(Enumerable.Range(0, largo).Select(_ => (char)('0' + aleatorio.Next(10))));

    // Cambia el último dígito por el que hace cuadrar la suma: así se llega a leer el prefijo.
    private static string ConControl(string digitos)
    {
        string catorce = digitos.PadLeft(Gtin.Longitud, '0');

        for (char control = '0'; control <= '9'; control++)
        {
            if (CuadraElControl(catorce[..^1] + control))
            {
                return digitos[..^1] + control;
            }
        }

        throw new InvalidOperationException("Siempre hay un dígito que cuadra la suma.");
    }

    private static string Ruidoso(Random aleatorio, string alfabeto) =>
        string.Concat(
            Enumerable.Range(0, aleatorio.Next(17)).Select(_ => alfabeto[aleatorio.Next(alfabeto.Length)]));
}
