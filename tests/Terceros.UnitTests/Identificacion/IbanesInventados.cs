using System.Globalization;
using Bastion.BuildingBlocks.Domain.Identificacion;

namespace Bastion.Terceros.UnitTests.Identificacion;

/// <summary>
/// Fabrica IBAN a partir de un número de cuenta inventado, calculando su control, y fabrica
/// también las tres formas de romperlo: el control cambiado, la longitud de otro país y un país
/// que no existe.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué se generan y no se pegan, y aquí más que con el NIF.</b> Un IBAN es una cuenta
/// bancaria. Pegado en una <i>fixture</i> no se queda en el fichero: viaja al artefacto de
/// resultados, al registro de la CI y al historial de git, para siempre y sin plazo de supresión —
/// y a diferencia de un NIF, con él se puede intentar cobrar. Los casos de esta batería son
/// mecánicos: una cuenta de relleno más el control que le toca por algoritmo.
/// </para>
/// <para>
/// <b>El espacio inventado son el <c>9</c> y la <c>Z</c>, y nada más.</b> Es la misma decisión que
/// tomó el ítem 1.5 con los identificadores fiscales —números inventados con su control
/// calculado— escrita como una forma que se puede comprobar desde fuera:
/// <c>NingunDatoConFormaDeRealTests</c> barre el repositorio y exige que todo IBAN escrito como
/// literal esté en este espacio. La <c>Z</c> está para que el ramal de letras del mod-97 se ejerza
/// también dentro de la cuenta, y no solo en las dos letras del país.
/// </para>
/// <para>
/// <b>Lo que NO hace: adivinar.</b> El algoritmo de abajo es el mod-97-10 de la ISO 7064, el mismo
/// que comprueba <see cref="Iban"/>, así que este fichero no es una segunda implementación
/// independiente y no pretende serlo. Lo que aporta es COBERTURA: los treinta y siete países de la
/// tabla, las dos clases de contenido y los tres motivos de rechazo, en vez de una muestra elegida
/// a mano.
/// </para>
/// </remarks>
internal static class IbanesInventados
{
    /// <summary>El dígito de relleno del espacio inventado.</summary>
    internal const char DigitoDeRelleno = '9';

    /// <summary>Y su letra, para que el mod-97 vea letras dentro de la cuenta.</summary>
    internal const char LetraDeRelleno = 'Z';

    /// <summary>Un país que no está en ningún registro de IBAN.</summary>
    /// <remarks>
    /// <c>ZZ</c> está reservado por la ISO 3166-1 para uso privado, así que no lo va a estrenar
    /// ningún país mañana y este caso no caduca.
    /// </remarks>
    internal const string PaisInexistente = "ZZ";

    /// <summary>Los países de la tabla, ordenados, que es el alcance de la batería.</summary>
    internal static IReadOnlyList<string> Paises { get; } =
        [.. Iban.LongitudPorPais.Keys.Order(StringComparer.Ordinal)];

    /// <summary>El IBAN de relleno de un país, con su control calculado.</summary>
    /// <param name="pais">Los dos caracteres del país, que tiene que estar en la tabla.</param>
    /// <param name="conLetras">
    /// Si la cuenta alterna dígito y letra. Con <c>false</c> es todo dígitos, que es la forma que
    /// tiene la mayoría de los países de la zona.
    /// </param>
    internal static Inventado Valido(string pais, bool conLetras = false)
    {
        string cuenta = Relleno(Iban.LongitudPorPais[pais] - 4, conLetras);

        return Armar(pais, cuenta, conLetras ? "con letras" : "con dígitos");
    }

    /// <summary>
    /// El mismo IBAN válido, con el control cambiado a un valor que no le corresponde.
    /// </summary>
    /// <remarks>
    /// El control se mueve una posición y se vuelve a escribir con dos cifras, así que el «97»
    /// pasa a «98» y el «02» a «03»: sigue siendo un control con forma de control, que es lo que
    /// separa comprobar el mod-97 de mirar si son dos dígitos.
    /// </remarks>
    /// <param name="pais">Los dos caracteres del país.</param>
    internal static string ConElControlCambiado(string pais)
    {
        Inventado valido = Valido(pais);
        int control = int.Parse(valido.Valor[2..4], CultureInfo.InvariantCulture);

        return pais
            + (((control % 97) + 1) % 100).ToString("D2", CultureInfo.InvariantCulture)
            + valido.Valor[4..];
    }

    /// <summary>
    /// Un IBAN con el país de uno, la longitud de otro y el control <b>bien calculado</b>.
    /// </summary>
    /// <remarks>
    /// <b>Este es el caso que separa las dos comprobaciones.</b> Pasa el mod-97 sin despeinarse,
    /// porque el algoritmo no sabe cuánto debería medir lo que está sumando; lo único que lo
    /// rechaza es la tabla de longitudes. Una implementación que solo comprobara el control daría
    /// esto por bueno, y el fichero SEPA se lo llevaría al banco.
    /// </remarks>
    /// <param name="pais">El país que se escribe en el IBAN.</param>
    /// <param name="otro">El país cuya longitud se usa. Tiene que ser distinta de la del primero.</param>
    internal static string ConLongitudDeOtroPais(string pais, string otro)
    {
        int longitud = Iban.LongitudPorPais[otro];

        if (longitud == Iban.LongitudPorPais[pais])
        {
            throw new ArgumentException(
                $"«{pais}» y «{otro}» miden lo mismo, así que este caso no probaría nada.",
                nameof(otro));
        }

        return Armar(pais, Relleno(longitud - 4, conLetras: false), "longitud ajena").Valor;
    }

    /// <summary>Un IBAN de un país que no existe, con el control bien calculado.</summary>
    /// <remarks>
    /// El otro caso que no caza el mod-97: el control cuadra, así que lo único que lo rechaza es
    /// que <see cref="PaisInexistente"/> no esté en la tabla.
    /// </remarks>
    /// <param name="longitud">Cuánto mide en total, para que sea una longitud plausible.</param>
    internal static string DePaisInexistente(int longitud) =>
        Armar(PaisInexistente, Relleno(longitud - 4, conLetras: false), "país inexistente").Valor;

    /// <summary>
    /// El control que le toca a un país y una cuenta: <c>98 menos el mod-97 con el control a
    /// cero</c>, que es como lo define la ISO 13616.
    /// </summary>
    /// <param name="pais">Los dos caracteres del país.</param>
    /// <param name="cuenta">La cuenta, sin país ni control.</param>
    internal static string Control(string pais, string cuenta) =>
        (98 - Mod97(pais + "00" + cuenta)).ToString("D2", CultureInfo.InvariantCulture);

    private static Inventado Armar(string pais, string cuenta, string forma) =>
        new(pais + Control(pais, cuenta) + cuenta, $"{pais} {forma}");

    private static string Relleno(int cuantos, bool conLetras) =>
        conLetras
            ? string.Concat(Enumerable.Range(0, cuantos).Select(
                posicion => posicion % 2 == 0 ? DigitoDeRelleno : LetraDeRelleno))
            : new string(DigitoDeRelleno, cuantos);

    // El mismo mod-97-10 de la ISO 7064 que comprueba `Iban`: los cuatro primeros caracteres al
    // final, cada letra por su posición en el alfabeto más diez, a trozos porque el número entero
    // no cabe en ningún entero del lenguaje.
    private static int Mod97(string valor)
    {
        int resto = 0;

        foreach (char caracter in valor[4..].Concat(valor[..4]))
        {
            resto = char.IsAsciiDigit(caracter)
                ? ((resto * 10) + (caracter - '0')) % 97
                : ((resto * 100) + (caracter - 'A' + 10)) % 97;
        }

        return resto;
    }

    /// <summary>Un caso de la batería: el IBAN, y cómo se llama en el informe.</summary>
    /// <param name="Valor">El IBAN con el control que le toca.</param>
    /// <param name="Nombre">Qué es, para que un fallo diga cuál de los setenta y cuatro falló.</param>
    internal sealed record Inventado(string Valor, string Nombre);
}
