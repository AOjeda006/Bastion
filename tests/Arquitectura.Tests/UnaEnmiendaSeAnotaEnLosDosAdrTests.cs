using System.Text;
using System.Text.RegularExpressions;
using Bastion.Pruebas.Comun;
using Shouldly;

namespace Bastion.Arquitectura.Tests;

/// <summary>
/// Lo que un ADR dice que enmienda o amplía, el otro lo anota: en su <c>Estado</c> y en el punto que
/// cambia.
/// </summary>
/// <remarks>
/// <para>
/// <b>Esta regla nace de haberla incumplido.</b> La revisión del proyecto antes de la fase 3 encontró
/// que el ADR-0039 enmendaba el §5 del ADR-0007, y el ADR-0040 dos frases del ADR-0013, y que ni el
/// 0007 ni el 0013 lo decían. Al pasar el guion de aquella revisión a este caso salieron tres más,
/// todas ampliaciones: la del 0035 al 0021, la del 0043 al 0040 y la del 0016 al 0011, esta última
/// escrita en su viñeta <c>Relacionado</c>. Y otras tres, anotadas en el <c>Estado</c>, no tenían
/// nota en el punto: la del 0042 al 0041 y las dos del 0063, al 0030 y al 0036. Quien abre el ADR
/// viejo lee una decisión que ya no está en pie, y nada se ponía rojo, porque son ficheros de texto.
/// </para>
/// <para>
/// <b>Qué se lee.</b> La cabecera de un ADR va del título al primer <c>## </c>, y se lee por
/// viñetas de primer nivel, con sus líneas de continuación. Una <b>declaración</b> es una viñeta que
/// empieza por «Enmienda» o «Amplía», con una etiqueta delante o sin ella. Lo que enmienda son los ADR
/// de su primera cláusula, hasta los dos puntos o el primer punto y seguido, que es donde las
/// viñetas ponen el objeto del verbo y no los ADR que solo citan. Una <b>anotación</b> es la viñeta
/// del <c>Estado</c> del enmendado: dice «enmendado» o «ampliado» y nombra a cada autor con
/// «por el ADR-NNNN». Una <b>nota</b> es la misma frase fuera de la cabecera, en el punto que cambia.
/// </para>
/// <para>
/// <b>Las cinco afirmaciones del ADR-0024</b>, porque se comparan dos fuentes. Las dos listas no
/// están vacías, y la pareja, una enmienda anotada en los dos lados, está en las dos. Lo declarado
/// está anotado, y lo anotado está declarado. Cada declaración apunta a un ADR que existe y nombra
/// lo que enmienda, y cada estado que se dice enmendado nombra a quién. Y nada del universo queda
/// sin clasificar: una viñeta que nombra una enmienda y no la declara está en la lista cerrada, con
/// su motivo. El caso sintético es el que rompe el arnés sin tocar el sujeto: si la comparación
/// deja de ver una enmienda sin anotar, se pone rojo él solo.
/// </para>
/// <para>
/// <b>Lo que no mira.</b> Que la nota esté en el punto justo: se pide que esté en el cuerpo, y el
/// punto lo decide quien la escribe. Y los otros verbos de relación —sustituye, precisa, cumple,
/// cierra, desarrolla, aplica—, que no cambian lo que el otro decide.
/// </para>
/// </remarks>
public sealed partial class UnaEnmiendaSeAnotaEnLosDosAdrTests
{
    private const string Carpeta = "docs/adr";

    /// <summary>Una enmienda anotada en los dos lados y con su nota: la que tiene que ver la regla.</summary>
    private static readonly Enmienda s_pareja = new("0070", "0067");

    /// <summary>
    /// Las viñetas de cabecera que nombran una enmienda sin declararla, por su ADR y su principio. Se
    /// comparan enteras, en los dos sentidos: una que deje de estar es tan roja como una nueva.
    /// </summary>
    private static readonly IReadOnlyList<Mencion> s_menciones =
    [
        new("0037", "Cumple una promesa del ADR-0016", "dice que no enmienda nada del ADR-0016"),
        new("0055", "No enmienda ninguno", "lo dice, para que nadie busque a quién enmienda"),
        new("0065", "No enmienda ningún ADR", "lo dice, y nombra los que desarrolla"),
        new("0068", "Cumple lo que el ADR-0065", "dice que al ADR-0065 no lo enmienda"),
        new("0069", "Sale del ítem 2.19", "«la enmienda» es la que iba a ir al PLAN"),
    ];

    [Fact]
    public void El_barrido_ve_las_enmiendas_y_la_pareja_anotada_en_los_dos_lados()
    {
        Analisis analisis = Analizar(DelRepositorio());

        // Un mínimo y no «no vacío»: el proyecto pasa de treinta, así que un barrido que vea tres
        // está roto aunque no esté vacío.
        analisis.Declaradas.Count.ShouldBeGreaterThan(
            20,
            $"solo se han leído {analisis.Declaradas.Count} enmiendas declaradas en {Carpeta}. O el " +
            "patrón de la declaración ha dejado de casar, o se lee mal la cabecera, y las dos cosas " +
            "dejan esta regla en verde sin haber mirado nada");
        analisis.Anotadas.Count.ShouldBeGreaterThan(
            20, $"solo se han leído {analisis.Anotadas.Count} anotaciones en los Estado de {Carpeta}");

        analisis.Declaradas.ShouldContain(s_pareja, $"no se ve la declaración de {s_pareja}");
        analisis.Anotadas.ShouldContain(s_pareja, $"no se ve la anotación de {s_pareja}");
        analisis.SinNota.ShouldNotContain(s_pareja, $"no se ve la nota de {s_pareja} en el punto");
    }

    [Fact]
    public void Una_enmienda_que_el_enmendado_no_anota_se_ve()
    {
        Enmienda enmienda = new("0002", "0001");
        Enmienda ampliacion = new("0003", "0001");
        string declara = Sintetico(
            "0002", "aceptado", "- **Enmienda el [ADR-0001](adr-0001-uno.md)**, en su §1.", "Otra cosa.");
        string amplia = Sintetico(
            "0003", "aceptado", "- **Relacionado:** amplía el ADR-0001: un segundo caso.", "Otra más.");

        Analisis roja = Analizar(new Dictionary<string, string>
        {
            ["0001"] = Sintetico("0001", "aceptado", "", "Lo que se decidió."),
            ["0002"] = declara,
            ["0003"] = amplia,
        });

        roja.SinAnotar.ShouldBe([enmienda, ampliacion], "una enmienda sin anotar tiene que verse");
        roja.SinNota.ShouldBe([enmienda, ampliacion], "una enmienda sin nota tiene que verse");

        Analisis verde = Analizar(new Dictionary<string, string>
        {
            ["0001"] = Sintetico(
                "0001",
                "aceptado. **Enmendado por el [ADR-0002](adr-0002-dos.md)** en el §1, y\n" +
                "  **ampliado por el ADR-0003**.",
                "",
                "> **Enmendado por el ADR-0002 (2026-01-02).** Cambia.\n>\n" +
                "> **Ampliado por el [ADR-0003](adr-0003-tres.md) (2026-01-03).** Crece.\n\n" +
                "Lo que se decidió."),
            ["0002"] = declara,
            ["0003"] = amplia,
        });

        verde.Declaradas.ShouldBe([enmienda, ampliacion], ignoreOrder: true);
        verde.SinAnotar.ShouldBeEmpty();
        verde.AnotadasSinDeclarar.ShouldBeEmpty();
        verde.SinNota.ShouldBeEmpty();
    }

    [Fact]
    public void Cada_enmienda_apunta_a_un_ADR_que_existe() =>
        Analizar(DelRepositorio()).Inexistentes.ShouldBeEmpty(
            "hay ADR que declaran enmendar uno que no existe en la carpeta. O el número está mal, " +
            "o el enmendado se renumeró sin que quien lo enmienda se enterase");

    [Fact]
    public void Cada_enmienda_nombra_lo_que_enmienda()
    {
        Analisis analisis = Analizar(DelRepositorio());

        analisis.SinObjeto.ShouldBeEmpty(
            "hay viñetas que declaran una enmienda y no nombran ningún ADR antes de los dos puntos " +
            "o del primer punto. El objeto del verbo va ahí; si va más lejos, la regla no lo ve");
        analisis.EstadosSinQuien.ShouldBeEmpty(
            "hay ADR cuyo Estado dice que está enmendado o ampliado y no nombra a nadie con «por el " +
            "ADR-NNNN». Esa anotación no se puede comparar con ninguna declaración");
    }

    [Fact]
    public void El_enmendado_anota_en_su_estado_quien_lo_enmienda() =>
        Analizar(DelRepositorio()).SinAnotar.ShouldBeEmpty(
            "hay enmiendas que el ADR enmendado no anota en su Estado. Quien lo abre lee una " +
            "decisión que ya no está en pie. Se anota «Enmendado por el ADR-NNNN» o «Ampliado por " +
            "el ADR-NNNN» en su Estado, con el punto que cambia");

    [Fact]
    public void El_enmendado_lleva_la_nota_en_el_punto_que_cambia() =>
        Analizar(DelRepositorio()).SinNota.ShouldBeEmpty(
            "hay enmiendas sin nota en el cuerpo del ADR enmendado. Se pone al principio del punto " +
            "que cambia: «> **Enmendado por el ADR-NNNN (fecha).**», y lo que cambia");

    [Fact]
    public void Lo_que_anota_un_estado_lo_declara_quien_enmienda() =>
        Analizar(DelRepositorio()).AnotadasSinDeclarar.ShouldBeEmpty(
            "hay ADR cuyo Estado dice que otro los enmienda, y el otro no lo declara en su " +
            "cabecera con «Enmienda el ADR-NNNN» o «Amplía el ADR-NNNN»");

    [Fact]
    public void Ninguna_mencion_de_una_enmienda_queda_sin_clasificar()
    {
        IReadOnlyList<Vineta> menciones = Analizar(DelRepositorio()).Menciones;

        IReadOnlyList<string> sinClasificar =
        [
            .. from mencion in menciones
               where !s_menciones.Any(declarada => declarada.Casa(mencion))
               select $"ADR-{mencion.Adr}: {mencion.Texto}",
        ];
        IReadOnlyList<string> sinMencion =
        [
            .. from declarada in s_menciones
               where !menciones.Any(declarada.Casa)
               select $"ADR-{declarada.Adr}: «{declarada.Empieza}…», que {declarada.Motivo}",
        ];

        sinClasificar.ShouldBeEmpty(
            "hay viñetas de cabecera que nombran una enmienda o una ampliación y no empiezan por " +
            "«Enmienda» ni por «Amplía». Si declaran una, se reescriben con el verbo delante; si " +
            "no, entran en la lista de esta clase con su motivo");
        sinMencion.ShouldBeEmpty(
            "la lista de menciones que no son enmiendas nombra viñetas que ya no están. Se borran " +
            "de la lista");
    }

    private static Dictionary<string, string> DelRepositorio()
    {
        string carpeta = Path.Combine(RaizDelRepositorio.Ruta(), Carpeta);

        Directory.Exists(carpeta).ShouldBeTrue($"no hay carpeta {Carpeta}");

        return Directory
            .EnumerateFiles(carpeta, "adr-*.md")
            .Select(ruta => (Nombre: NumeroDelFichero().Match(Path.GetFileName(ruta)), Ruta: ruta))
            .Where(fichero => fichero.Nombre.Success)
            .ToDictionary(
                fichero => fichero.Nombre.Groups["numero"].Value,
                fichero => File.ReadAllText(fichero.Ruta),
                StringComparer.Ordinal);
    }

    private static Analisis Analizar(Dictionary<string, string> textos)
    {
        HashSet<Enmienda> declaradas = [];
        HashSet<Enmienda> anotadas = [];
        List<string> sinObjeto = [];
        List<string> estadosSinQuien = [];
        List<Vineta> menciones = [];
        Dictionary<string, HashSet<string>> notas = new(StringComparer.Ordinal);

        foreach ((string numero, string texto) in textos)
        {
            (IReadOnlyList<string> vinetas, string cuerpo) = Partir(numero, texto);

            notas[numero] = [.. Nota().Matches(cuerpo).Select(nota => nota.Groups["numero"].Value)];

            foreach (string vineta in vinetas)
            {
                if (vineta.StartsWith("Estado:", StringComparison.Ordinal))
                {
                    if (!Anotacion().IsMatch(vineta))
                    {
                        continue;
                    }

                    List<string> autores = Numeros(PorEl(), vineta, numero);

                    if (autores.Count == 0)
                    {
                        estadosSinQuien.Add($"ADR-{numero}: {vineta}");
                    }

                    anotadas.UnionWith(autores.Select(autor => new Enmienda(autor, numero)));
                    continue;
                }

                Match declaracion = Declaracion().Match(vineta);

                if (declaracion.Success)
                {
                    string clausula = FinDeLaClausula().Split(vineta[declaracion.Length..], 2)[0];
                    List<string> enmendados = Numeros(NumeroDeAdr(), clausula, numero);

                    if (enmendados.Count == 0)
                    {
                        sinObjeto.Add($"ADR-{numero}: {vineta}");
                    }

                    declaradas.UnionWith(enmendados.Select(enmendado => new Enmienda(numero, enmendado)));
                }
                else if (NombraUnaEnmienda().IsMatch(vineta))
                {
                    menciones.Add(new Vineta(numero, vineta));
                }
            }
        }

        return new Analisis(
            declaradas,
            anotadas,
            Ordenadas(declaradas.Where(enmienda => !textos.ContainsKey(enmienda.Enmendado))),
            Ordenadas(declaradas.Where(enmienda =>
                notas.TryGetValue(enmienda.Enmendado, out HashSet<string>? autores) &&
                !autores.Contains(enmienda.Autor))),
            sinObjeto,
            estadosSinQuien,
            menciones);
    }

    /// <summary>
    /// Las viñetas de primer nivel de la cabecera, ya sin enlaces ni negritas, y el cuerpo, que
    /// empieza en el primer <c>## </c>.
    /// </summary>
    private static (IReadOnlyList<string> Vinetas, string Cuerpo) Partir(string numero, string texto)
    {
        string[] lineas = [.. texto.Split('\n').Select(linea => linea.TrimEnd('\r'))];
        int titulo = Array.FindIndex(lineas, linea => linea.StartsWith("# ", StringComparison.Ordinal));

        titulo.ShouldBeGreaterThanOrEqualTo(0, $"el ADR-{numero} no tiene título «# »");

        int seccion = Array.FindIndex(
            lineas, titulo + 1, linea => linea.StartsWith("## ", StringComparison.Ordinal));
        int fin = seccion < 0 ? lineas.Length : seccion;

        List<string> vinetas = [];
        StringBuilder? actual = null;

        foreach (string linea in lineas[(titulo + 1)..fin])
        {
            if (linea.StartsWith("- ", StringComparison.Ordinal))
            {
                Cerrar();
                actual = new StringBuilder(linea[2..]);
            }
            else if (actual is not null && linea.StartsWith("  ", StringComparison.Ordinal))
            {
                actual.Append(' ').Append(linea.Trim());
            }
            else
            {
                Cerrar();
            }
        }

        Cerrar();

        return (vinetas, Limpio(string.Join('\n', lineas[fin..])));

        void Cerrar()
        {
            if (actual is not null)
            {
                vinetas.Add(Limpio(actual.ToString()));
                actual = null;
            }
        }
    }

    private static string Limpio(string texto) => Enlace().Replace(texto, "$1").Replace("*", "", StringComparison.Ordinal);

    private static List<string> Numeros(Regex patron, string texto, string propio) =>
    [
        .. patron.Matches(texto)
            .Select(numero => numero.Groups["numero"].Value)
            .Where(numero => numero != propio)
            .Distinct(StringComparer.Ordinal),
    ];

    private static List<Enmienda> Ordenadas(IEnumerable<Enmienda> enmiendas) =>
    [
        .. enmiendas
            .OrderBy(enmienda => enmienda.Autor, StringComparer.Ordinal)
            .ThenBy(enmienda => enmienda.Enmendado, StringComparer.Ordinal),
    ];

    private static string Sintetico(string numero, string estado, string cabecera, string cuerpo) =>
        $"---\ntipo: referencia\n---\n\n# ADR-{numero}: Sintético\n\n- **Estado:** {estado}\n" +
        $"- **Fecha:** 2026-01-01\n{cabecera}\n\n## Contexto\n\n{cuerpo}\n";

    [GeneratedRegex(@"^adr-(?<numero>\d{4})-")]
    private static partial Regex NumeroDelFichero();

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex Enlace();

    /// <summary>El verbo delante, con una etiqueta como <c>Relacionado:</c> o sin ella.</summary>
    [GeneratedRegex(@"^(?:\p{L}+:\s+)?(?:Enmienda|Amplía)", RegexOptions.IgnoreCase)]
    private static partial Regex Declaracion();

    [GeneratedRegex(@":|\.(?:\s|$)")]
    private static partial Regex FinDeLaClausula();

    [GeneratedRegex(@"ADR-(?<numero>\d{4})")]
    private static partial Regex NumeroDeAdr();

    [GeneratedRegex(@"enmendad|ampliad", RegexOptions.IgnoreCase)]
    private static partial Regex Anotacion();

    [GeneratedRegex(@"\bpor el ADR-(?<numero>\d{4})")]
    private static partial Regex PorEl();

    [GeneratedRegex(@"(?:Enmendad|Ampliad)[oa]s? por el ADR-(?<numero>\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex Nota();

    [GeneratedRegex(@"\b(?:enmiend|amplí)\w*", RegexOptions.IgnoreCase)]
    private static partial Regex NombraUnaEnmienda();

    /// <summary>Que el ADR <paramref name="Autor"/> enmienda o amplía el <paramref name="Enmendado"/>.</summary>
    /// <param name="Autor">El número del que enmienda.</param>
    /// <param name="Enmendado">El número del enmendado.</param>
    private sealed record Enmienda(string Autor, string Enmendado)
    {
        public override string ToString() => $"ADR-{Autor} → ADR-{Enmendado}";
    }

    /// <summary>Una viñeta de cabecera, ya limpia, y el ADR que la lleva.</summary>
    /// <param name="Adr">El número del ADR.</param>
    /// <param name="Texto">La viñeta.</param>
    private sealed record Vineta(string Adr, string Texto);

    /// <summary>Una viñeta que nombra una enmienda y no la declara, con el motivo.</summary>
    /// <param name="Adr">El número del ADR que la lleva.</param>
    /// <param name="Empieza">Cómo empieza la viñeta.</param>
    /// <param name="Motivo">Por qué no es una declaración.</param>
    private sealed record Mencion(string Adr, string Empieza, string Motivo)
    {
        public bool Casa(Vineta vineta) =>
            vineta.Adr == Adr && vineta.Texto.StartsWith(Empieza, StringComparison.Ordinal);
    }

    /// <summary>Lo que se leyó de la carpeta, en las dos fuentes, y lo que no cuadra.</summary>
    private sealed record Analisis(
        IReadOnlySet<Enmienda> Declaradas,
        IReadOnlySet<Enmienda> Anotadas,
        IReadOnlyList<Enmienda> Inexistentes,
        IReadOnlyList<Enmienda> SinNota,
        IReadOnlyList<string> SinObjeto,
        IReadOnlyList<string> EstadosSinQuien,
        IReadOnlyList<Vineta> Menciones)
    {
        public IReadOnlyList<Enmienda> SinAnotar => Ordenadas(Declaradas.Except(Anotadas));

        public IReadOnlyList<Enmienda> AnotadasSinDeclarar => Ordenadas(Anotadas.Except(Declaradas));
    }
}
