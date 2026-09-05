using Bastion.BuildingBlocks.Domain.Identificacion;
using Shouldly;
using static Bastion.Terceros.UnitTests.Identificacion.IdentificadoresInventados;

namespace Bastion.Terceros.UnitTests.Identificacion;

/// <summary>
/// La batería del carácter de control, generada, y recorrida en las dos direcciones: todo lo que
/// el generador calcula bien tiene que entrar, y todo lo que calcula mal a propósito tiene que
/// rebotar.
/// </summary>
/// <remarks>
/// <para>
/// Cada barrido afirma su universo antes de recorrerlo. Un barrido que no encuentra nada sale
/// verde por la peor de las razones —lista vacía, cero comprobaciones— y aquí sería fácil: basta
/// con que el generador devuelva una secuencia perezosa que nadie enumera.
/// </para>
/// <para>
/// No sustituye a <c>NifTests</c>, que sigue en Organización con sus casos escritos a mano. Aquel
/// dice «este valor concreto vale»; este dice «vale toda la familia». Lo que aquel no puede decir
/// —y por eso existe este— es qué pasa con las veinte iniciales de persona jurídica y las
/// veintitrés letras del resto, que a mano son doscientas líneas que nadie escribe.
/// </para>
/// </remarks>
public sealed class LaBateriaGeneradaTests
{
    // Secuencias inventadas y a la vista: repeticiones, cuentas y saltos. Fijas, no aleatorias,
    // porque un caso que solo falla una vez de cada mil ejecuciones es peor que no tenerlo (la
    // batería tiene que fallar igual en esta máquina y en el runner).
    private static readonly int[] s_ochoCifras =
    [
        0, 1, 22, 23, 24, 1_000_000, 11_111_111, 12_345_678, 22_222_222, 45_454_545,
        70_000_001, 87_654_321, 98_765_432, 99_999_998, 99_999_999,
    ];

    private static readonly int[] s_sieteCifras =
    [
        0, 1, 7, 1_111_111, 1_234_567, 4_545_454, 7_000_001, 8_765_432, 9_999_998, 9_999_999,
    ];

    [Fact]
    public void Todo_identificador_generado_con_su_caracter_de_control_se_acepta()
    {
        IReadOnlyList<Inventado> casos = Casos();

        casos.Count.ShouldBe(
            (s_ochoCifras.Length * 23) + (InicialesDelNie.Length * s_sieteCifras.Length)
                + (InicialesDePersonaJuridica.Length * s_sieteCifras.Length),
            "el generador no ha producido la batería entera, así que este barrido estaría " +
            "recorriendo menos de lo que dice");

        List<string> rechazados =
        [
            .. from caso in casos
               where !Nif.Intentar(caso.Valido, out _)
               select $"{caso.Nombre}: «{caso.Valido}»",
        ];

        rechazados.ShouldBeEmpty(
            "estos identificadores llevan el carácter de control que les toca y el validador los " +
            "ha rechazado. O el algoritmo de `Nif` está mal, o lo está el de este generador, y " +
            "el fallo dice cuál mirando la forma:" + Environment.NewLine +
            string.Join(Environment.NewLine, rechazados.Take(20)));
    }

    [Fact]
    public void Todo_identificador_generado_con_el_control_movido_se_rechaza()
    {
        IReadOnlyList<Inventado> casos = Casos();

        casos.ShouldNotBeEmpty("sin casos, este barrido no comprueba nada");

        List<string> aceptados =
        [
            .. from caso in casos
               where Nif.Intentar(caso.ConElControlCambiado, out _)
               select $"{caso.Nombre}: «{caso.ConElControlCambiado}»",
        ];

        aceptados.ShouldBeEmpty(
            "estos identificadores llevan el carácter de control movido una posición y el " +
            "validador los ha aceptado, o sea que no está comprobando nada:" + Environment.NewLine +
            string.Join(Environment.NewLine, aceptados.Take(20)));
    }

    /// <summary>
    /// El control escrito en la clase que la inicial no admite: el valor correcto, la forma
    /// equivocada.
    /// </summary>
    /// <remarks>
    /// Es la comprobación que separa una implementación que MIRA LA INICIAL de una que acepta las
    /// dos formas para todo el mundo, y la segunda pasa los dos barridos de arriba enteros. Solo
    /// se le puede pedir a las once iniciales estrictas: las otras nueve admiten las dos formas
    /// de verdad, y exigirles una sería inventarse la ley.
    /// </remarks>
    [Fact]
    public void El_control_de_la_clase_equivocada_se_rechaza()
    {
        string estrictas = InicialesDeControlAlfabetico + InicialesDeControlNumerico;

        estrictas.Length.ShouldBe(11, "once iniciales estrictas: siete alfabéticas y cuatro numéricas");

        List<string> aceptados =
        [
            .. from inicial in estrictas
               from numero in s_sieteCifras
               let valor = ConLaClaseDeControlCambiada(inicial, numero)
               where Nif.Intentar(valor, out _)
               select valor,
        ];

        aceptados.ShouldBeEmpty(
            "el carácter de control de estos vale lo que tiene que valer, pero está escrito en la " +
            "clase que su inicial no admite, y el validador los ha aceptado:" + Environment.NewLine +
            string.Join(Environment.NewLine, aceptados.Take(20)));
    }

    /// <summary>
    /// Las dos tablas de iniciales —la de este generador y la de <c>Nif</c>— dicen lo mismo, y se
    /// comprueba por el comportamiento porque la de <c>Nif</c> es privada.
    /// </summary>
    /// <remarks>
    /// Sin esto, el generador podría estar dejando fuera una inicial entera y los tres barridos de
    /// arriba seguirían verdes: no comprobarían esa inicial, y tampoco echarían de menos nada. Se
    /// mira en las dos direcciones —las veinte que reconoce y las tres que no— porque una tabla
    /// de más y una de menos son fallos distintos.
    /// </remarks>
    [Fact]
    public void Las_iniciales_que_este_generador_conoce_son_las_que_el_validador_reconoce()
    {
        const string Abecedario = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        List<string> discrepancias = [];

        foreach (char inicial in Abecedario)
        {
            bool laConoceElGenerador =
                InicialesDePersonaJuridica.Contains(inicial, StringComparison.Ordinal)
                || InicialesDelNie.Contains(inicial, StringComparison.Ordinal);

            // Se prueba con las DOS formas de control y con varios números: si el validador
            // acepta esa inicial de alguna manera, es que la reconoce.
            bool laAceptaElValidador = s_sieteCifras.Any(numero =>
                Nif.Intentar(PersonaJuridica(inicial, numero, comoLetra: true).Valido, out _)
                || Nif.Intentar(PersonaJuridica(inicial, numero, comoLetra: false).Valido, out _)
                || (InicialesDelNie.Contains(inicial, StringComparison.Ordinal)
                    && Nif.Intentar(Nie(inicial, numero).Valido, out _)));

            if (laConoceElGenerador != laAceptaElValidador)
            {
                discrepancias.Add(
                    $"«{inicial}»: el generador dice {laConoceElGenerador} y el validador " +
                    $"{laAceptaElValidador}");
            }
        }

        discrepancias.ShouldBeEmpty(
            "las iniciales de este generador y las de `Nif` no son las mismas, así que la " +
            "batería está cubriendo un conjunto distinto del que el validador reconoce:" +
            Environment.NewLine + string.Join(Environment.NewLine, discrepancias));
    }

    /// <summary>
    /// Ningún caso de esta batería está escrito a mano: todos salen de un número y un cálculo.
    /// </summary>
    /// <remarks>
    /// La afirmación del §2 puesta donde se puede desobedecer. Un identificador fiscal real es un
    /// dato personal y una fixture no se borra nunca: queda en el fichero, en el artefacto de
    /// resultados y en el registro de la CI. Que las semillas sean números y no cadenas es lo que
    /// hace imposible pegar uno aquí sin que se note.
    /// </remarks>
    [Fact]
    public void Las_semillas_de_la_bateria_son_numeros_y_no_identificadores_pegados()
    {
        s_ochoCifras.ShouldAllBe(numero => numero >= 0 && numero <= 99_999_999);
        s_sieteCifras.ShouldAllBe(numero => numero >= 0 && numero <= 9_999_999);

        // Y el carácter de control NO viene con la semilla: se calcula. Si alguien sustituyera el
        // generador por una tabla de valores, esta comprobación seguiría verde —no puede verlo—,
        // pero el tipo de las semillas obliga a que el último carácter salga de un cálculo.
        Dni(12_345_678).Valido.ShouldBe(
            "12345678" + LetrasDePersonaFisica[12_345_678 % 23],
            "la letra del DNI es la posición «resto entre 23» de la tabla, no una elección");
    }

    private static IReadOnlyList<Inventado> Casos() =>
    [
        // Los DNI: cada número de la lista recorrido por las veintitrés letras posibles, sumando
        // el resto que haga falta. Así ninguna letra de la tabla se queda sin ejercer, que es lo
        // que una muestra a mano no consigue sin escribir veintitrés líneas.
        .. from numero in s_ochoCifras
           from salto in Enumerable.Range(0, 23)
           select Dni(Ajustado(numero, salto)),

        .. from inicial in InicialesDelNie
           from numero in s_sieteCifras
           select Nie(inicial, numero),

        .. from inicial in InicialesDePersonaJuridica
           from numero in s_sieteCifras
           select PersonaJuridica(
               inicial,
               numero,
               comoLetra: !InicialesDeControlNumerico.Contains(inicial, StringComparison.Ordinal)),
    ];

    // Mueve el número lo justo para que su resto entre 23 sea el que se pide, sin salirse de las
    // ocho cifras.
    private static int Ajustado(int numero, int restoPedido)
    {
        int desplazado = numero + (((restoPedido - (numero % 23)) + 23) % 23);

        return desplazado <= 99_999_999 ? desplazado : desplazado - 23;
    }
}
