using System.Diagnostics.CodeAnalysis;

namespace Bastion.BuildingBlocks.Domain.Identificacion;

/// <summary>
/// Número de identificación fiscal español, con su carácter de control comprobado de verdad.
/// </summary>
/// <remarks>
/// <para>
/// Cubre las tres formas, porque las tres pueden ser el NIF de una empresa: <b>persona
/// jurídica</b> (lo que se llamaba CIF), <b>DNI</b> y <b>NIE</b>. Que un empresario individual
/// tribute con su DNI no es un detalle: es la razón por la que la ficha de una empresa puede
/// contener datos personales, y por la que el artículo 32 de la LOPDGDD le alcanza.
/// </para>
/// <para>
/// Vive en el bloque común, y no en un módulo, porque lo van a necesitar Organización,
/// Terceros, Facturación y Contabilidad. El §7.2 del plan maestro lo describe como campo de
/// <c>Tercero</c>; eso dice de quién es el dato, no dónde vive el tipo.
/// </para>
/// <para>
/// Dos puertas a propósito (ADR-0004): <see cref="Intentar"/> para el borde, que necesita
/// devolver un error POR CAMPO sin excepciones; <see cref="De"/> para cuando el valor ya viene
/// comprobado, donde un fallo es un error de programación.
/// </para>
/// </remarks>
public sealed record Nif
{
    /// <summary>Un NIF ocupa exactamente nueve posiciones. No es una estimación.</summary>
    public const int Longitud = 9;

    // Letra de control del DNI y del NIE: la posición es el resto de dividir el número entre 23.
    private const string LetrasDeControlDePersonaFisica = "TRWAGMYFPDXBNJZSQVHLCKE";

    // Letra de control de las personas jurídicas que la llevan alfabética.
    private const string LetrasDeControlDePersonaJuridica = "JABCDEFGHI";

    // Iniciales del NIE. Su posición en esta cadena es el dígito por el que se sustituyen.
    private const string InicialesDelNie = "XYZ";

    // Personas jurídicas cuyo control es SIEMPRE una letra (entidades sin ánimo de lucro,
    // organismos públicos, corporaciones locales y no residentes, entre otras).
    private const string InicialesDeControlAlfabetico = "KPQRSNW";

    // Personas jurídicas cuyo control es SIEMPRE un dígito.
    private const string InicialesDeControlNumerico = "ABEH";

    // El resto de iniciales de persona jurídica admite las dos formas de control.
    private const string InicialesDePersonaJuridica = "ABCDEFGHJKLMNPQRSUVW";

    private Nif(string valor) => Valor = valor;

    /// <summary>El identificador normalizado: nueve posiciones, en mayúsculas.</summary>
    public string Valor { get; }

    /// <summary>Construye el NIF, o lanza si el valor no es válido.</summary>
    public static Nif De(string valor)
    {
        if (!Intentar(valor, out Nif? nif))
        {
            throw new ArgumentException(
                $"«{valor}» no es un NIF válido: el carácter de control no cuadra.",
                nameof(valor));
        }

        return nif;
    }

    /// <summary>Intenta construir el NIF; devuelve <c>false</c> en vez de lanzar.</summary>
    public static bool Intentar(string? valor, [NotNullWhen(true)] out Nif? nif)
    {
        nif = null;

        string? normalizado = Normalizar(valor);
        if (normalizado is null || !EsValido(normalizado))
        {
            return false;
        }

        nif = new Nif(normalizado);
        return true;
    }

    /// <inheritdoc/>
    public override string ToString() => Valor;

    // Espacios, puntos y guiones son ruido de teclado, no parte del identificador.
    private static string? Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        string limpio = string.Concat(valor.Where(char.IsAsciiLetterOrDigit)).ToUpperInvariant();

        // Si al limpiar se ha caído algo que no era ni letra ni dígito, el valor traía basura
        // y no se acepta: quitar en silencio un carácter raro es adivinar por el usuario.
        int significativos = valor.Count(caracter =>
            !char.IsWhiteSpace(caracter) && caracter is not ('-' or '.'));

        return limpio.Length == Longitud && limpio.Length == significativos ? limpio : null;
    }

    private static bool EsValido(string valor)
    {
        char inicial = valor[0];

        if (char.IsAsciiDigit(inicial))
        {
            return EsPersonaFisicaValida(valor, valor[..8]);
        }

        if (InicialesDelNie.Contains(inicial, StringComparison.Ordinal))
        {
            string numero = InicialesDelNie.IndexOf(inicial, StringComparison.Ordinal)
                + valor[1..8];
            return EsPersonaFisicaValida(valor, numero);
        }

        return InicialesDePersonaJuridica.Contains(inicial, StringComparison.Ordinal)
            && EsPersonaJuridicaValida(valor);
    }

    private static bool EsPersonaFisicaValida(string valor, string numero)
    {
        if (!numero.All(char.IsAsciiDigit) || !char.IsAsciiLetterUpper(valor[8]))
        {
            return false;
        }

        int resto = int.Parse(numero, System.Globalization.CultureInfo.InvariantCulture) % 23;
        return valor[8] == LetrasDeControlDePersonaFisica[resto];
    }

    private static bool EsPersonaJuridicaValida(string valor)
    {
        string digitos = valor[1..8];
        if (!digitos.All(char.IsAsciiDigit))
        {
            return false;
        }

        // Las posiciones pares se suman tal cual; las impares se duplican y se suman las cifras
        // del resultado (algoritmo de la disposición adicional sexta de la Ley 37/1988).
        int suma = 0;
        for (int posicion = 0; posicion < digitos.Length; posicion++)
        {
            int digito = digitos[posicion] - '0';

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

        int control = (10 - (suma % 10)) % 10;
        char ultimo = valor[8];
        char inicial = valor[0];

        if (InicialesDeControlAlfabetico.Contains(inicial, StringComparison.Ordinal))
        {
            return ultimo == LetrasDeControlDePersonaJuridica[control];
        }

        if (InicialesDeControlNumerico.Contains(inicial, StringComparison.Ordinal))
        {
            return ultimo == (char)('0' + control);
        }

        // El resto admite las dos formas: hay entidades con control numérico y otras con letra
        // bajo la misma inicial, así que rechazar una de las dos daría falsos negativos.
        return ultimo == (char)('0' + control)
            || ultimo == LetrasDeControlDePersonaJuridica[control];
    }
}
