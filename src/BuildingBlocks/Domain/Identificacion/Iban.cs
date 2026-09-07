using System.Diagnostics.CodeAnalysis;

namespace Bastion.BuildingBlocks.Domain.Identificacion;

/// <summary>
/// Número de cuenta bancaria internacional, con su control comprobado de verdad y con la longitud
/// que le toca a su país.
/// </summary>
/// <remarks>
/// <para>
/// Vive en el bloque común y no en Terceros porque lo van a necesitar también Tesorería —los
/// cobros y los pagos— y los mandatos SEPA del §7.2. El §7.2 lo describe como campo de
/// <c>CuentaBancaria</c>; eso dice de quién es el dato, no dónde vive el tipo.
/// </para>
/// <para>
/// <b>Las dos comprobaciones son independientes y las dos hacen falta.</b> El mod-97-10 caza el
/// dígito tecleado de más o cambiado de sitio, que es para lo que se inventó. Lo que <b>no</b> caza
/// es la longitud: una cadena con la longitud de otro país y el control recalculado pasa el mod-97
/// perfectamente, porque el algoritmo no sabe cuánto debería medir lo que está sumando. Un IBAN
/// así llega al fichero SEPA, y allí lo rechaza el banco días después, cuando el pago ya se dio
/// por hecho.
/// </para>
/// <para>
/// <b><see cref="ToString"/> devuelve la forma ENMASCARADA, y eso es una divergencia deliberada
/// con <see cref="Nif"/>.</b> Un IBAN es una cuenta bancaria: si acaba en un registro, en un
/// mensaje de excepción o en el artefacto de resultados de la CI, se queda ahí para siempre. Y a
/// un registro no se llega escribiendo <c>.Valor</c>, se llega interpolando el objeto en una
/// cadena, que es justo lo que llama a <see cref="ToString"/> sin que nadie lo decida. Quien
/// necesite el número entero —el fichero de adeudos, la pantalla que lo enseña— tiene que pedirlo
/// por <see cref="Valor"/>, y eso se ve al leer el código.
/// </para>
/// <para>
/// Dos puertas a propósito (ADR-0004): <see cref="Intentar"/> para el borde, que necesita devolver
/// un error por campo sin excepciones; <see cref="De"/> para cuando el valor ya viene comprobado.
/// </para>
/// </remarks>
public sealed record Iban
{
    /// <summary>Ningún IBAN pasa de 34 posiciones. Lo fija la norma ISO 13616.</summary>
    public const int LongitudMaxima = 34;

    /// <summary>Ni baja de 15, que es la de Noruega.</summary>
    public const int LongitudMinima = 15;

    private Iban(string valor) => Valor = valor;

    /// <summary>
    /// Cuánto mide el IBAN de cada país, incluidos los dos caracteres del país y los dos del
    /// control.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es la zona SEPA, no el registro entero de la ISO.</b> El registro trae más de ochenta
    /// países, y aceptarlos todos haría creer que esta instalación puede domiciliar un adeudo
    /// contra cualquiera de ellos, que es falso: fuera de SEPA hace falta una transferencia
    /// internacional, con su comisión y su plazo, y eso no es un recibo. Añadir un país es cambiar
    /// esta tabla, y es un cambio de datos con su motivo, no una casualidad.
    /// </para>
    /// <para>
    /// Los territorios de ultramar no llevan código propio: Guadalupe, Martinica o la Reunión usan
    /// el IBAN francés, y por eso no aparecen aquí.
    /// </para>
    /// </remarks>
    public static IReadOnlyDictionary<string, int> LongitudPorPais { get; } =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["AD"] = 24,
            ["AT"] = 20,
            ["BE"] = 16,
            ["BG"] = 22,
            ["CH"] = 21,
            ["CY"] = 28,
            ["CZ"] = 24,
            ["DE"] = 22,
            ["DK"] = 18,
            ["EE"] = 20,
            ["ES"] = 24,
            ["FI"] = 18,
            ["FR"] = 27,
            ["GB"] = 22,
            ["GI"] = 23,
            ["GR"] = 27,
            ["HR"] = 21,
            ["HU"] = 28,
            ["IE"] = 22,
            ["IS"] = 26,
            ["IT"] = 27,
            ["LI"] = 21,
            ["LT"] = 20,
            ["LU"] = 20,
            ["LV"] = 21,
            ["MC"] = 27,
            ["MT"] = 31,
            ["NL"] = 18,
            ["NO"] = 15,
            ["PL"] = 28,
            ["PT"] = 25,
            ["RO"] = 24,
            ["SE"] = 24,
            ["SI"] = 19,
            ["SK"] = 24,
            ["SM"] = 27,
            ["VA"] = 22,
        };

    /// <summary>El IBAN normalizado: sin espacios, en mayúsculas.</summary>
    public string Valor { get; }

    /// <summary>Los dos caracteres del país.</summary>
    public string Pais => Valor[..2];

    /// <summary>Construye el IBAN, o lanza si el valor no es válido.</summary>
    /// <remarks>
    /// El mensaje NO lleva el valor rechazado, a diferencia del de <see cref="Nif"/>. Un IBAN mal
    /// tecleado suele ser un IBAN bien tecleado al que le falta un carácter — o sea, casi una
    /// cuenta de verdad—, y las excepciones acaban en el registro.
    /// </remarks>
    /// <param name="valor">El IBAN, con o sin los espacios con los que se imprime.</param>
    public static Iban De(string valor)
    {
        if (!Intentar(valor, out Iban? iban))
        {
            throw new ArgumentException(
                "El valor no es un IBAN válido: o el país no está admitido, o la longitud no es " +
                "la de ese país, o el control no cuadra.",
                nameof(valor));
        }

        return iban;
    }

    /// <summary>Intenta construir el IBAN; devuelve <c>false</c> en vez de lanzar.</summary>
    /// <param name="valor">El IBAN, con o sin los espacios con los que se imprime.</param>
    /// <param name="iban">El IBAN construido, si el valor era válido.</param>
    public static bool Intentar(string? valor, [NotNullWhen(true)] out Iban? iban)
    {
        iban = null;

        string? normalizado = Normalizar(valor);
        if (normalizado is null || !EsValido(normalizado))
        {
            return false;
        }

        iban = new Iban(normalizado);
        return true;
    }

    /// <summary>La forma que se enseña: el país, el control y los cuatro últimos.</summary>
    /// <remarks>Ver la nota de la clase. Para el número entero, <see cref="Valor"/>.</remarks>
    public override string ToString() =>
        Valor[..4] + new string('*', Valor.Length - 8) + Valor[^4..];

    // Los espacios son la forma en que se IMPRIME un IBAN —en grupos de cuatro—, así que llegan
    // pegados desde cualquier copiar y pegar. El resto de la basura no se quita en silencio: un
    // guion en mitad de una cuenta es un valor que alguien tiene que mirar, no que adivinar.
    private static string? Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        string limpio = string.Concat(valor.Where(caracter => !char.IsWhiteSpace(caracter)))
            .ToUpperInvariant();

        return limpio.All(char.IsAsciiLetterOrDigit) ? limpio : null;
    }

    private static bool EsValido(string valor)
    {
        if (valor.Length is < LongitudMinima or > LongitudMaxima)
        {
            return false;
        }

        // El país y el control ocupan sitios fijos y tienen clase fija: dos letras y dos dígitos.
        // Sin esto, un valor que empezara por cifras entraría a buscar su país en la tabla y
        // saldría por «país no admitido», que es un motivo distinto del que de verdad tiene.
        if (!char.IsAsciiLetterUpper(valor[0]) || !char.IsAsciiLetterUpper(valor[1])
            || !char.IsAsciiDigit(valor[2]) || !char.IsAsciiDigit(valor[3]))
        {
            return false;
        }

        // La longitud del país, ANTES del mod-97 y como comprobación aparte: una cadena con la
        // longitud de otro país y el control recalculado pasa el mod-97 sin despeinarse.
        if (!LongitudPorPais.TryGetValue(valor[..2], out int longitud) || valor.Length != longitud)
        {
            return false;
        }

        return Mod97(valor) == 1;
    }

    // ISO 7064 mod-97-10: los cuatro primeros caracteres se llevan al final, cada letra se
    // sustituye por su posición en el alfabeto más diez, y el número que sale tiene que dar 1 al
    // dividirlo entre 97. Se calcula a trozos porque el número entero tiene hasta 68 cifras y no
    // cabe en ningún entero del lenguaje.
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
}
