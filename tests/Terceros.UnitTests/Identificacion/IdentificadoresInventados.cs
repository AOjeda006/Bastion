using System.Globalization;

namespace Bastion.Terceros.UnitTests.Identificacion;

/// <summary>
/// Fabrica identificadores fiscales españoles a partir de números inventados, calculando su
/// carácter de control, y fabrica también la versión con el control equivocado.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué se generan y no se pegan.</b> Un NIF real es un dato personal, y una fixture no se
/// queda en el fichero: viaja al artefacto de resultados, al registro de la CI y al historial de
/// git, para siempre y sin plazo de supresión. Los casos de esta batería son mecánicos —una
/// secuencia inventada más el carácter que le toca por algoritmo—, no ejemplos copiados de
/// ninguna parte ni el identificador de nadie, y ninguno lleva un nombre al lado.
/// </para>
/// <para>
/// <b>Y de paso la batería prueba el algoritmo en las dos direcciones.</b> Este generador CALCULA
/// el carácter de control; <c>Nif</c> lo COMPRUEBA. Un test con valores pegados solo ejerce la
/// segunda mitad, y solo en los diez o doce casos que a alguien se le ocurrieron. Aquí, cada
/// identificador que sale de aquí tiene que entrar en <c>Nif</c>, y su gemelo con el control
/// movido una posición tiene que rebotar.
/// </para>
/// <para>
/// <b>Lo que NO hace: adivinar.</b> Las tres tablas de abajo están copiadas de la ley, igual que
/// las de <c>Nif</c>, así que este fichero no es una segunda implementación independiente y no
/// pretende serlo. Lo que aporta es COBERTURA: las tres formas, las veintitrés letras del resto,
/// las veinte iniciales de persona jurídica y las dos clases de control, en vez de una muestra
/// elegida a mano.
/// </para>
/// </remarks>
internal static class IdentificadoresInventados
{
    /// <summary>Letra de control del DNI y del NIE, indexada por el resto entre 23.</summary>
    internal const string LetrasDePersonaFisica = "TRWAGMYFPDXBNJZSQVHLCKE";

    /// <summary>Letra de control de la persona jurídica, indexada por el dígito de control.</summary>
    internal const string LetrasDePersonaJuridica = "JABCDEFGHI";

    /// <summary>Iniciales del NIE. Su posición es el dígito por el que se sustituyen.</summary>
    internal const string InicialesDelNie = "XYZ";

    /// <summary>Iniciales de persona jurídica cuyo control es SIEMPRE una letra.</summary>
    internal const string InicialesDeControlAlfabetico = "KPQRSNW";

    /// <summary>Iniciales de persona jurídica cuyo control es SIEMPRE un dígito.</summary>
    internal const string InicialesDeControlNumerico = "ABEH";

    /// <summary>Iniciales de persona jurídica que admiten las dos formas de control.</summary>
    internal const string InicialesDeControlIndistinto = "CDFGJLMUV";

    /// <summary>Todas las iniciales de persona jurídica que la ley reconoce.</summary>
    internal const string InicialesDePersonaJuridica =
        InicialesDeControlAlfabetico + InicialesDeControlNumerico + InicialesDeControlIndistinto;

    /// <summary>El DNI de un número inventado de ocho cifras, con su letra.</summary>
    /// <param name="numero">Las ocho cifras, sin letra.</param>
    internal static Inventado Dni(int numero)
    {
        string cifras = numero.ToString("D8", CultureInfo.InvariantCulture);
        int resto = numero % 23;

        return new Inventado(
            cifras + LetrasDePersonaFisica[resto],
            cifras + LetrasDePersonaFisica[(resto + 1) % 23],
            $"DNI {cifras}");
    }

    /// <summary>El NIE de una inicial y un número inventado de siete cifras, con su letra.</summary>
    /// <param name="inicial">X, Y o Z.</param>
    /// <param name="numero">Las siete cifras que van detrás de la inicial.</param>
    internal static Inventado Nie(char inicial, int numero)
    {
        string cifras = numero.ToString("D7", CultureInfo.InvariantCulture);

        // La inicial se SUSTITUYE por su posición y el resultado se trata como un DNI. Es la
        // parte que una implementación ingenua se salta —tratando la X como si no estuviera— y
        // que entonces acepta el mismo número con las tres iniciales.
        int completo = int.Parse(
            InicialesDelNie.IndexOf(inicial, StringComparison.Ordinal).ToString(
                CultureInfo.InvariantCulture) + cifras,
            CultureInfo.InvariantCulture);

        int resto = completo % 23;

        return new Inventado(
            inicial + cifras + LetrasDePersonaFisica[resto],
            inicial + cifras + LetrasDePersonaFisica[(resto + 1) % 23],
            $"NIE {inicial}{cifras}");
    }

    /// <summary>
    /// El identificador de una persona jurídica, en la forma de control que se le pida.
    /// </summary>
    /// <param name="inicial">La letra de la forma jurídica.</param>
    /// <param name="numero">Las siete cifras que van detrás de la inicial.</param>
    /// <param name="comoLetra">
    /// Si el control se escribe como letra. Tiene que ser el que la inicial admita: para las de
    /// <see cref="InicialesDeControlAlfabetico"/> y <see cref="InicialesDeControlNumerico"/> solo
    /// vale una de las dos formas, y esa es justo la diferencia que esta batería explota.
    /// </param>
    internal static Inventado PersonaJuridica(char inicial, int numero, bool comoLetra)
    {
        string cifras = numero.ToString("D7", CultureInfo.InvariantCulture);
        int control = ControlDePersonaJuridica(cifras);
        int siguiente = (control + 1) % 10;

        return new Inventado(
            inicial + cifras + Escrito(control, comoLetra),
            inicial + cifras + Escrito(siguiente, comoLetra),
            $"{(comoLetra ? "letra" : "dígito")} {inicial}{cifras}");
    }

    /// <summary>
    /// El mismo identificador válido, pero con el control escrito en la OTRA clase: el valor
    /// correcto en la forma que esa inicial no admite.
    /// </summary>
    /// <remarks>
    /// Es el caso que separa una implementación que mira la inicial de una que acepta las dos
    /// formas para todo el mundo. La segunda pasa la batería entera sin esto, porque el carácter
    /// de control «vale» lo que tiene que valer: lo que está mal es de qué clase es.
    /// </remarks>
    /// <param name="inicial">La letra de la forma jurídica.</param>
    /// <param name="numero">Las siete cifras que van detrás de la inicial.</param>
    internal static string ConLaClaseDeControlCambiada(char inicial, int numero)
    {
        string cifras = numero.ToString("D7", CultureInfo.InvariantCulture);
        int control = ControlDePersonaJuridica(cifras);
        bool esAlfabetica =
            InicialesDeControlAlfabetico.Contains(inicial, StringComparison.Ordinal);

        return inicial + cifras + Escrito(control, comoLetra: !esAlfabetica);
    }

    private static char Escrito(int control, bool comoLetra) =>
        comoLetra ? LetrasDePersonaJuridica[control] : (char)('0' + control);

    // Disposición adicional sexta de la Ley 37/1988: las posiciones pares (contadas desde cero)
    // se duplican y se suman las cifras del resultado; las impares se suman tal cual.
    private static int ControlDePersonaJuridica(string cifras)
    {
        int suma = 0;

        for (int posicion = 0; posicion < cifras.Length; posicion++)
        {
            int digito = cifras[posicion] - '0';

            if (posicion % 2 == 0)
            {
                int duplicado = digito * 2;
                suma += (duplicado / 10) + (duplicado % 10);
            }
            else
            {
                suma += digito;
            }
        }

        return (10 - (suma % 10)) % 10;
    }

    /// <summary>Un caso de la batería: el válido, su gemelo roto, y cómo se llama en el informe.</summary>
    /// <param name="Valido">El identificador con el carácter de control que le toca.</param>
    /// <param name="ConElControlCambiado">El mismo, con el control movido una posición.</param>
    /// <param name="Nombre">Qué es, para que un fallo diga cuál de los cientos falló.</param>
    internal sealed record Inventado(string Valido, string ConElControlCambiado, string Nombre);
}
