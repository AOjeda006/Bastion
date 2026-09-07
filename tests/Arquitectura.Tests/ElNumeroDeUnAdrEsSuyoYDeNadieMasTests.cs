using System.Text.RegularExpressions;
using Shouldly;

namespace Bastion.Arquitectura.Tests;

/// <summary>
/// El número de un ADR lo lleva un fichero y solo uno, y es el mismo que dice su título.
/// </summary>
/// <remarks>
/// <para>
/// <b>Esta regla nace de haberla incumplido.</b> El ítem 1.6 escribió dos ADR, y los dos salieron
/// <c>ADR-0031</c>: uno para el agujero del artículo 32 y otro para la clave que declara quién la
/// pone. Nada se puso rojo — son dos ficheros de texto, y ni el compilador ni ningún barrido
/// miraban su numeración—, y la colisión se vio al releer el árbol antes de cerrar.
/// </para>
/// <para>
/// <b>Por qué no es cosmético.</b> El número <i>es</i> la manera de citar un ADR: lo citan los
/// <c>remarks</c> del código, los mensajes de las aserciones, el PLAN y los demás ADR. Con dos
/// ficheros compartiendo número, toda cita anterior pasa a ser ambigua y ninguna herramienta lo
/// nota: quien busque «ADR-0031» encontrará dos decisiones distintas y no sabrá cuál de las dos
/// avalaba lo que estaba leyendo.
/// </para>
/// <para>
/// <b>Y la segunda afirmación es la que hace útil a la primera.</b> Renumerar es renombrar un
/// fichero, y renombrar un fichero deja el título de dentro diciendo el número viejo — que es una
/// colisión otra vez, con el número escondido en la cabecera en vez de en el nombre. Así que se
/// comparan las dos cosas: el número del nombre y el número del título, uno a uno.
/// </para>
/// </remarks>
public sealed class ElNumeroDeUnAdrEsSuyoYDeNadieMasTests
{
    private const string Carpeta = "docs/adr";

    private static readonly Regex s_delNombre =
        new(@"^adr-(?<numero>\d{4})-", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

    private static readonly Regex s_delTitulo =
        new(@"^#\s+ADR-(?<numero>\d{4})\b", RegexOptions.Multiline, TimeSpan.FromSeconds(1));

    /// <summary>Que haya ADR que mirar, porque una carpeta vacía pasa las otras dos.</summary>
    [Fact]
    public void El_barrido_encuentra_los_ADR_del_repositorio()
    {
        string carpeta = Path.Combine(Ensamblados.Raiz(), Carpeta);

        Directory.Exists(carpeta).ShouldBeTrue(
            $"no hay carpeta {Carpeta}: sin ella esta regla no mira nada");

        // Un mínimo y no «no vacío»: el proyecto pasa de treinta, así que un barrido que encuentre
        // tres ficheros está roto aunque no esté vacío.
        Ficheros().Count.ShouldBeGreaterThan(
            20,
            $"solo se han leído {Ficheros().Count} ADR en {Carpeta}. O el patrón del nombre ha " +
            "dejado de casar, o se está mirando la carpeta equivocada — y las dos cosas dejan " +
            "esta regla en verde sin haber comprobado nada");
    }

    /// <summary>Ningún número lo llevan dos ficheros.</summary>
    [Fact]
    public void Ningun_numero_de_ADR_lo_llevan_dos_ficheros()
    {
        IReadOnlyList<string> repetidos =
        [
            .. from fichero in Ficheros()
               group fichero by s_delNombre.Match(fichero).Groups["numero"].Value into mismo
               where mismo.Count() > 1
               orderby mismo.Key, StringComparer.Ordinal
               select $"ADR-{mismo.Key}: {string.Join(", ", mismo.Order(StringComparer.Ordinal))}",
        ];

        repetidos.ShouldBeEmpty(
            "hay números de ADR usados por más de un fichero. El número es la manera de citar un " +
            "ADR desde el código, desde el PLAN y desde los demás ADR, así que un número " +
            "compartido convierte en ambigua toda cita que ya estuviera escrita. Se arregla " +
            "renumerando el más nuevo —fichero y título— y actualizando quien lo cite");
    }

    /// <summary>El número del título es el del nombre del fichero.</summary>
    [Fact]
    public void Cada_ADR_lleva_en_su_titulo_el_numero_de_su_nombre_de_fichero()
    {
        IReadOnlyList<string> discrepan =
        [
            .. from fichero in Ficheros()
               let enElNombre = s_delNombre.Match(fichero).Groups["numero"].Value
               let titulo = s_delTitulo.Match(File.ReadAllText(
                   Path.Combine(Ensamblados.Raiz(), Carpeta, fichero)))
               where !titulo.Success || titulo.Groups["numero"].Value != enElNombre
               orderby fichero, StringComparer.Ordinal
               select titulo.Success
                   ? $"{fichero}: el título dice ADR-{titulo.Groups["numero"].Value}"
                   : $"{fichero}: no se le encuentra ningún título «# ADR-NNNN»",
        ];

        discrepan.ShouldBeEmpty(
            "hay ADR cuyo título no dice el número de su fichero. Pasa al renumerar: se renombra " +
            "el fichero y la cabecera se queda con el número viejo, que es la misma colisión de " +
            "antes con el número escondido dentro");
    }

    private static IReadOnlyList<string> Ficheros() =>
        [.. Directory
            .EnumerateFiles(Path.Combine(Ensamblados.Raiz(), Carpeta), "*.md")
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(nombre => s_delNombre.IsMatch(nombre))
            .Order(StringComparer.Ordinal)];
}
