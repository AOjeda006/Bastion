using Bastion.BuildingBlocks.Domain.Identificacion;
using Shouldly;

namespace Bastion.Terceros.UnitTests.Identificacion;

/// <summary>
/// El IBAN se comprueba en las dos direcciones, y los rechazos van <b>por motivo</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Un negativo genérico no dice nada.</b> «Esta cadena no es un IBAN» lo cumple una
/// implementación que compruebe solo el control, una que compruebe solo la longitud y una que
/// devuelva <c>false</c> siempre. Los tres motivos —control, longitud y país— se prueban por
/// separado y con casos construidos para que <b>solo</b> falle ese: el de la longitud y el del
/// país llevan el control <b>bien calculado</b>, así que el mod-97 los da por buenos y lo único
/// que puede rechazarlos es la comprobación que se está probando.
/// </para>
/// <para>
/// <b>Y las dos direcciones no son simetría por gusto.</b> Una implementación que aceptara todo
/// pasaría la mitad positiva; una que rechazara todo pasaría las tres negativas. Solo las cuatro
/// juntas dicen algo.
/// </para>
/// </remarks>
public sealed class LaBateriaDeIbanTests
{
    /// <summary>
    /// El ancla: si la tabla de países se vaciara, las cuatro baterías de abajo recorrerían la
    /// lista vacía y saldrían verdes sin comprobar ni un IBAN.
    /// </summary>
    [Fact]
    public void La_bateria_recorre_los_paises_de_la_tabla()
    {
        IbanesInventados.Paises.ShouldNotBeEmpty(
            "sin países no se genera ni un caso, y las cuatro baterías de este fichero se " +
            "cumplirían en vacío");

        // Y son los MISMOS que la tabla del dominio, comparados enteros: si alguien añade un país
        // y la batería sigue recorriendo los de antes, el país nuevo no lo prueba nadie.
        IbanesInventados.Paises.ShouldBe(
            [.. Iban.LongitudPorPais.Keys.Order(StringComparer.Ordinal)],
            customMessage: "la batería y la tabla del dominio no recorren los mismos países");

        // Un país cuyo IBAN midiera menos que el país y el control no es un país, es un error de
        // tecleo en la tabla que dejaría casos imposibles de generar.
        IbanesInventados.Paises.ShouldAllBe(pais => Iban.LongitudPorPais[pais] >= 15);
    }

    /// <summary>Todo IBAN de relleno con su control calculado entra, en los dos contenidos.</summary>
    [Fact]
    public void Todo_iban_con_su_control_calculado_entra()
    {
        List<string> rechazados = [];

        foreach (string pais in IbanesInventados.Paises)
        {
            foreach (bool conLetras in (bool[])[false, true])
            {
                IbanesInventados.Inventado caso = IbanesInventados.Valido(pais, conLetras);

                if (!Iban.Intentar(caso.Valor, out Iban? iban) || iban.Pais != pais)
                {
                    rechazados.Add(caso.Nombre);
                }
            }
        }

        rechazados.ShouldBeEmpty(
            "estos IBAN llevan el control que les toca por algoritmo y la longitud de su país, y " +
            "aun así se han rechazado:" + Environment.NewLine + "· " +
            string.Join(Environment.NewLine + "· ", rechazados));
    }

    /// <summary>Motivo 1: el control no cuadra.</summary>
    [Fact]
    public void Un_control_cambiado_se_rechaza()
    {
        List<string> aceptados =
        [
            .. from pais in IbanesInventados.Paises
               let roto = IbanesInventados.ConElControlCambiado(pais)
               where Iban.Intentar(roto, out _)
               select pais,
        ];

        aceptados.ShouldBeEmpty(
            "estos IBAN llevan el control cambiado —sigue siendo un control con forma de control, " +
            "pero no es el suyo— y se han aceptado:" + Environment.NewLine + "· " +
            string.Join(Environment.NewLine + "· ", aceptados));
    }

    /// <summary>
    /// Motivo 2: la longitud es la de otro país, y el control está <b>bien</b>.
    /// </summary>
    /// <remarks>
    /// El caso se demuestra a sí mismo: la MISMA cuenta, con el país al que esa longitud sí le
    /// corresponde, tiene que entrar. Si entrara la primera y no la segunda, lo que rechaza no
    /// sería la longitud sino otra cosa, y el test estaría pasando por la razón equivocada.
    /// </remarks>
    [Fact]
    public void Una_longitud_de_otro_pais_se_rechaza_aunque_el_control_cuadre()
    {
        List<string> fallos = [];

        foreach (string pais in IbanesInventados.Paises)
        {
            string? otro = IbanesInventados.Paises.FirstOrDefault(
                candidato => Iban.LongitudPorPais[candidato] != Iban.LongitudPorPais[pais]);

            // No puede no haberlo —la tabla tiene longitudes distintas—, pero si un día la tabla
            // se quedara con una sola longitud este caso dejaría de probar nada en silencio.
            otro.ShouldNotBeNull(
                $"no hay ningún país con una longitud distinta de la de «{pais}», así que este " +
                "caso no puede construirse y la batería se cumpliría en vacío");

            string mestizo = IbanesInventados.ConLongitudDeOtroPais(pais, otro);

            if (Iban.Intentar(mestizo, out _))
            {
                fallos.Add($"{pais} con la longitud de {otro} se ha aceptado");
            }

            // La otra mitad: esa misma longitud, con su país, entra.
            if (!Iban.Intentar(IbanesInventados.Valido(otro).Valor, out _))
            {
                fallos.Add($"{otro} con su propia longitud se ha rechazado");
            }
        }

        fallos.ShouldBeEmpty(
            "el mod-97 da por bueno un IBAN con la longitud de otro país si el control se " +
            "recalcula, así que la tabla de longitudes es lo único que lo separa de un fichero " +
            "SEPA que el banco rechaza días después:" + Environment.NewLine + "· " +
            string.Join(Environment.NewLine + "· ", fallos));
    }

    /// <summary>
    /// Motivo 3: el país no existe, y el control está <b>bien</b>.
    /// </summary>
    [Fact]
    public void Un_pais_que_no_existe_se_rechaza_aunque_el_control_cuadre()
    {
        List<string> aceptados = [];

        // Se prueba con TODAS las longitudes de la tabla, no con una: un país inexistente cuyo
        // IBAN midiera lo que mide el español es el caso que más se parece a uno bueno.
        foreach (int longitud in Iban.LongitudPorPais.Values.Distinct().Order())
        {
            string forastero = IbanesInventados.DePaisInexistente(longitud);

            if (Iban.Intentar(forastero, out _))
            {
                aceptados.Add($"«{IbanesInventados.PaisInexistente}» con {longitud} posiciones");
            }
        }

        aceptados.ShouldBeEmpty(
            "estos IBAN llevan un país que no está en ningún registro y el control bien " +
            "calculado, así que solo los rechaza la tabla de países:" + Environment.NewLine +
            "· " + string.Join(Environment.NewLine + "· ", aceptados));
    }

    /// <summary>Los espacios con los que se imprime un IBAN no son parte del IBAN.</summary>
    [Theory]
    [InlineData("ES")]
    [InlineData("DE")]
    [InlineData("NO")]
    public void Los_espacios_de_impresion_no_estorban(string pais)
    {
        string valor = IbanesInventados.Valido(pais).Valor;
        string impreso = string.Join(
            ' ', Enumerable.Range(0, (valor.Length + 3) / 4)
                .Select(grupo => valor.Substring(grupo * 4, Math.Min(4, valor.Length - (grupo * 4)))));

        Iban.Intentar(impreso, out Iban? iban).ShouldBeTrue();
        iban!.Valor.ShouldBe(valor);
    }

    /// <summary>Lo que no es ni letra ni dígito no se quita en silencio.</summary>
    /// <remarks>
    /// Un guion en mitad de una cuenta es un valor que alguien tiene que mirar. Quitarlo y seguir
    /// es adivinar por el usuario, y con una cuenta bancaria adivinar sale caro.
    /// </remarks>
    [Fact]
    public void La_basura_no_se_quita_en_silencio()
    {
        string valor = IbanesInventados.Valido("ES").Valor;

        Iban.Intentar(valor[..4] + "-" + valor[4..], out _).ShouldBeFalse();
        Iban.Intentar(valor + ".", out _).ShouldBeFalse();
        Iban.Intentar(null, out _).ShouldBeFalse();
        Iban.Intentar("   ", out _).ShouldBeFalse();
    }

    /// <summary>
    /// <c>ToString</c> enmascara, y esa es la diferencia con <c>Nif</c>.
    /// </summary>
    /// <remarks>
    /// A un registro no se llega escribiendo <c>.Valor</c>: se llega interpolando el objeto en una
    /// cadena. Esta afirmación es la que impide que ese camino publique una cuenta entera.
    /// </remarks>
    [Fact]
    public void Al_convertirlo_a_cadena_no_se_ve_la_cuenta()
    {
        var iban = Iban.De(IbanesInventados.Valido("ES").Valor);

        string interpolado = $"{iban}";

        interpolado.ShouldNotBe(iban.Valor);
        interpolado.Length.ShouldBe(iban.Valor.Length);
        interpolado.ShouldStartWith(iban.Valor[..4]);
        interpolado.ShouldEndWith(iban.Valor[^4..]);
        interpolado[4..^4].ShouldAllBe(caracter => caracter == '*');
    }

    /// <summary>La puerta que lanza lanza, y sin enseñar el valor rechazado.</summary>
    [Fact]
    public void La_puerta_que_lanza_no_publica_lo_que_rechaza()
    {
        string roto = IbanesInventados.ConElControlCambiado("ES");

        ArgumentException error = Should.Throw<ArgumentException>(() => Iban.De(roto));

        error.Message.ShouldNotContain(roto);
    }
}
