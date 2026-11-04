using System.Globalization;
using System.Text.RegularExpressions;
using Bastion.Pruebas.Comun;
using Shouldly;

namespace Bastion.Arquitectura.Tests;

/// <summary>
/// La línea de estado del README dice lo que dicen las casillas del checklist de <c>docs/PLAN.md</c>,
/// cada fase cerrada tiene su historia con el checklist entero, y lo largo de cada ítem cerrado de la
/// vigente está en la suya.
/// </summary>
/// <remarks>
/// <para>
/// El README es la primera página que abre quien llega, y era el único documento que nada vigilaba:
/// al cerrar la fase 1 seguía diciendo «fase 0 (Cimientos), sin empezar», dos fases tarde, y ninguna
/// regla se enteró. El PLAN es la fuente de verdad del estado; el README lo resume en una línea, y
/// esa línea se deriva de las casillas o sale roja.
/// </para>
/// <para>
/// <b>Qué se deriva.</b> La fase es la más alta que tiene casillas en el checklist; su nombre, el de
/// su encabezado <c>### Fase N · Nombre</c>; las cuentas, sus casillas marcadas y las totales; y
/// «cerrada» si están todas, «en curso» si no. Lo demás del README es prosa y envejece como prosa.
/// </para>
/// <para>
/// <b>Desde el ítem 3.1, el PLAN solo lleva la fase vigente.</b> La historia de cada fase cerrada se
/// movió a <c>docs/historia/fase-N.md</c>, con su <c>## Checklist</c>. Así que las casillas del PLAN
/// son de una sola fase, y las fases cerradas con historia son exactamente las de antes de la
/// vigente, cada una con sus casillas, todas de su fase y todas marcadas. Una fase cerrada que se
/// quedara en el PLAN lo volvería a hacer crecer, y una historia que faltara sería una fase sin su
/// registro.
/// </para>
/// <para>
/// <b>Y desde el epílogo del 3.4, tampoco lo largo de sus ítems cerrados.</b> La puerta de cada uno,
/// lo que se decidió al escribirlo y su entrada de <c>Estado actual</c> se mueven a la historia de la
/// vigente en el commit que marca su casilla. Esa historia lleva algo de cada ítem cerrado y nada de
/// uno abierto, y solo <c>## Decisiones tomadas</c> y <c>## Estado actual</c>: el checklist y las
/// notas siguen en el PLAN hasta que la fase se cierre. Un bloque se reconoce por su encabezado, «La
/// puerta del N.M, contestada» o «Tomadas por el agente de desarrollo — ítem N.M», o por el «El N.M,
/// hecho el» con que empieza su entrada. La prohibición del PLAN tiene su pareja en la historia: cada
/// ítem cerrado tiene que tener ahí algún bloque, así que un reconocedor que no viera ninguno pondría
/// la regla roja, y no verde.
/// </para>
/// </remarks>
public sealed partial class ElEstadoDelReadmeEsElDelPlanTests
{
    private const string Readme = "README.md";
    private const string Plan = "docs/PLAN.md";
    private const string Historia = "docs/historia";

    [Fact]
    public void El_checklist_del_plan_se_lee()
    {
        List<Casilla> casillas = Casillas(Plan);

        casillas.ShouldNotBeEmpty(
            $"no se ha leído ni una casilla de la sección «## Checklist» de {Plan}: o cambió su " +
            "encabezado, o el formato de las casillas, y la línea del README se compararía con nada");

        Casillas(HistoriaDe(0)).Select(casilla => casilla.Fase).ShouldContain(
            0, $"el lector no ve las casillas de la fase 0 en {HistoriaDe(0)}, que están ahí desde el 3.1");
    }

    [Fact]
    public void El_plan_lleva_las_casillas_de_una_sola_fase()
    {
        List<int> fases = [.. Casillas(Plan).Select(casilla => casilla.Fase).Distinct().Order()];

        fases.Count.ShouldBe(
            1,
            $"{Plan} lleva casillas de las fases {string.Join(", ", fases)}: el PLAN solo lleva la " +
            $"vigente, y la historia de una fase cerrada va entera a {Historia}/fase-N.md");
    }

    [Fact]
    public void Cada_fase_cerrada_tiene_su_historia_con_el_checklist_entero()
    {
        int vigente = Casillas(Plan).Max(casilla => casilla.Fase);
        string carpeta = Path.Combine(RaizDelRepositorio.Ruta(), Historia);

        Directory.Exists(carpeta).ShouldBeTrue($"no existe {Historia}/");

        List<string> nombres =
        [
            .. Directory.EnumerateFiles(carpeta).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal),
        ];
        List<string> problemas =
        [
            .. from nombre in nombres
               where !FicheroDeHistoria().IsMatch(nombre)
               select $"{Historia}/{nombre}: no es «fase-N.md», y la historia no lleva otra cosa",
        ];

        List<int> conHistoria =
        [
            .. from nombre in nombres
               let fichero = FicheroDeHistoria().Match(nombre)
               where fichero.Success
               select int.Parse(fichero.Groups["fase"].Value, CultureInfo.InvariantCulture) into fase
               orderby fase
               select fase,
        ];
        List<int> debidas = [.. Enumerable.Range(0, vigente)];

        // La historia de la vigente, si la hay, es la de sus ítems cerrados, y la mira el caso de abajo.
        List<int> cerradas = [.. conHistoria.Where(fase => fase != vigente)];

        if (!cerradas.SequenceEqual(debidas))
        {
            problemas.Add(
                $"las fases cerradas con historia son [{string.Join(", ", cerradas)}] y tienen que ser las de " +
                $"antes de la vigente, la {vigente.ToString(CultureInfo.InvariantCulture)}: " +
                $"[{string.Join(", ", debidas)}]");
        }

        foreach (int fase in cerradas)
        {
            string ruta = HistoriaDe(fase);
            List<Casilla> casillas = Casillas(ruta);

            if (casillas.Count == 0)
            {
                problemas.Add($"{ruta}: no se lee ninguna casilla en su «## Checklist»");
            }

            problemas.AddRange(
                from casilla in casillas
                where casilla.Fase != fase
                select $"{ruta}: lleva una casilla de la fase {casilla.Fase.ToString(CultureInfo.InvariantCulture)}");

            int sinMarcar = casillas.Count(casilla => !casilla.Hecha);

            if (sinMarcar > 0)
            {
                problemas.Add(
                    $"{ruta}: {sinMarcar.ToString(CultureInfo.InvariantCulture)} casillas sin marcar, " +
                    "y una fase con historia está cerrada");
            }
        }

        problemas.ShouldBeEmpty(
            $"la historia de {Historia}/ no es la de las fases cerradas: cada fase de antes de la " +
            "vigente tiene su fichero, con sus casillas, todas marcadas");
    }

    [Fact]
    public void Lo_largo_de_cada_item_cerrado_esta_en_la_historia_y_no_en_el_plan()
    {
        List<Casilla> casillas = Casillas(Plan);
        int vigente = casillas.Max(casilla => casilla.Fase);
        string fase = vigente.ToString(CultureInfo.InvariantCulture);
        string historia = HistoriaDe(vigente);
        List<int> cerrados = [.. casillas.Where(casilla => casilla.Hecha).Select(casilla => casilla.Item).Order()];
        bool conHistoria = File.Exists(Path.Combine(RaizDelRepositorio.Ruta(), historia));
        List<Bloque> enLaHistoria = conHistoria ? Bloques(historia) : [];

        List<string> problemas =
        [
            .. from bloque in Bloques(Plan)
               where bloque.Fase == vigente && cerrados.Contains(bloque.Item)
               select $"{Plan}: «{bloque.Linea}» es de un ítem cerrado, y va a {historia} en su commit de cierre",
            .. from bloque in enLaHistoria
               where bloque.Fase != vigente || !cerrados.Contains(bloque.Item)
               select $"{historia}: «{bloque.Linea}» no es de un ítem cerrado de la fase vigente",
            .. from item in cerrados
               where !enLaHistoria.Any(bloque => bloque.Fase == vigente && bloque.Item == item)
               select $"{historia}: no lleva ningún bloque del {fase}." +
                      $"{item.ToString(CultureInfo.InvariantCulture)}, que tiene la casilla marcada",
        ];

        if (conHistoria)
        {
            problemas.AddRange(
                from linea in Lineas(historia)
                where linea.StartsWith("## ", StringComparison.Ordinal) &&
                      linea is not "## Decisiones tomadas" and not "## Estado actual"
                select $"{historia}: lleva «{linea}», y el checklist y las notas de la vigente siguen en {Plan}");
        }

        problemas.ShouldBeEmpty(
            "lo largo de cada ítem cerrado de la fase vigente —su puerta, lo que se decidió al escribirlo y su " +
            "entrada de «Estado actual»— va a su historia en el commit que marca su casilla, y solo eso");
    }

    [Fact]
    public void La_linea_de_estado_del_readme_es_la_del_checklist()
    {
        List<Casilla> casillas = Casillas(Plan);
        int fase = casillas.Max(casilla => casilla.Fase);
        int hechas = casillas.Count(casilla => casilla.Fase == fase && casilla.Hecha);
        int total = casillas.Count(casilla => casilla.Fase == fase);

        string? nombre = Encabezados().GetValueOrDefault(fase);

        nombre.ShouldNotBeNull(
            $"la fase {fase.ToString(CultureInfo.InvariantCulture)} tiene casillas en {Plan} y no " +
            "tiene encabezado «### Fase N · Nombre» del que sacar su nombre");

        string debida =
            $"> **Estado: fase {fase.ToString(CultureInfo.InvariantCulture)} ({nombre}), " +
            $"{(hechas == total ? "cerrada" : "en curso")}: {hechas.ToString(CultureInfo.InvariantCulture)} de " +
            $"{total.ToString(CultureInfo.InvariantCulture)} ítems.**";

        List<string> lineas = [.. Lineas(Readme).Where(linea => linea.StartsWith("> **Estado", StringComparison.Ordinal))];

        lineas.Count.ShouldBe(1, $"{Readme} tiene que tener una línea de estado, y solo una");

        lineas[0].StartsWith(debida, StringComparison.Ordinal).ShouldBeTrue(
            $"la línea de estado de {Readme} no es la que dan las casillas de {Plan}.\n" +
            $"  Dice:  {lineas[0]}\n  Debe empezar por: {debida}");
    }

    private static string HistoriaDe(int fase) =>
        $"{Historia}/fase-{fase.ToString(CultureInfo.InvariantCulture)}.md";

    /// <summary>Las casillas de la sección <c>## Checklist</c> de un fichero, hasta el siguiente <c>##</c>.</summary>
    private static List<Casilla> Casillas(string relativa) =>
    [
        .. from linea in SeccionDelChecklist(relativa)
           let casilla = CasillaDelPlan().Match(linea)
           where casilla.Success
           select new Casilla(
               int.Parse(casilla.Groups["fase"].Value, CultureInfo.InvariantCulture),
               int.Parse(casilla.Groups["item"].Value, CultureInfo.InvariantCulture),
               casilla.Groups["marca"].Value == "x"),
    ];

    /// <summary>Los bloques de lo largo de un ítem que lleva un fichero: sus encabezados y su entrada.</summary>
    private static List<Bloque> Bloques(string relativa) =>
    [
        .. from linea in Lineas(relativa)
           let bloque = LoLargoDeUnItem().Match(linea)
           where bloque.Success
           select new Bloque(
               int.Parse(bloque.Groups["fase"].Value, CultureInfo.InvariantCulture),
               int.Parse(bloque.Groups["item"].Value, CultureInfo.InvariantCulture),
               linea),
    ];

    private static Dictionary<int, string> Encabezados() =>
        (from linea in SeccionDelChecklist(Plan)
         let encabezado = EncabezadoDeFase().Match(linea)
         where encabezado.Success
         select (
             Fase: int.Parse(encabezado.Groups["fase"].Value, CultureInfo.InvariantCulture),
             Nombre: encabezado.Groups["nombre"].Value.Trim()))
        .ToDictionary(par => par.Fase, par => par.Nombre);

    private static IEnumerable<string> SeccionDelChecklist(string relativa) =>
        Lineas(relativa)
            .SkipWhile(linea => linea.Trim() != "## Checklist")
            .Skip(1)
            .TakeWhile(linea => !linea.StartsWith("## ", StringComparison.Ordinal));

    private static string[] Lineas(string relativa)
    {
        string ruta = Path.Combine(RaizDelRepositorio.Ruta(), relativa);

        File.Exists(ruta).ShouldBeTrue($"no existe {relativa}");

        return [.. File.ReadAllText(ruta).Split('\n').Select(linea => linea.TrimEnd('\r'))];
    }

    [GeneratedRegex(@"^- \[(?<marca>[ x])\] \*\*(?<fase>\d+)\.(?<item>\d+) · ")]
    private static partial Regex CasillaDelPlan();

    [GeneratedRegex(@"^### Fase (?<fase>\d+) · (?<nombre>[^(]+)")]
    private static partial Regex EncabezadoDeFase();

    [GeneratedRegex(@"^fase-(?<fase>0|[1-9]\d*)\.md$")]
    private static partial Regex FicheroDeHistoria();

    [GeneratedRegex(
        @"^(?:### La puerta del (?<fase>\d+)\.(?<item>\d+), contestada" +
        @"|### Tomadas por el agente de desarrollo — ítem (?<fase>\d+)\.(?<item>\d+)," +
        @"|\*\*El (?<fase>\d+)\.(?<item>\d+), hecho el )")]
    private static partial Regex LoLargoDeUnItem();

    /// <summary>Una casilla del checklist.</summary>
    /// <param name="Fase">El número de fase del ítem: el 1 de <c>1.12</c>.</param>
    /// <param name="Item">Su número dentro de la fase: el 12 de <c>1.12</c>.</param>
    /// <param name="Hecha">Si está marcada.</param>
    private sealed record Casilla(int Fase, int Item, bool Hecha);

    /// <summary>Un bloque de lo largo de un ítem: un encabezado de sus decisiones, o su entrada.</summary>
    /// <param name="Fase">La fase del ítem.</param>
    /// <param name="Item">Su número dentro de la fase.</param>
    /// <param name="Linea">La línea que lo empieza, para nombrarlo en el mensaje.</param>
    private sealed record Bloque(int Fase, int Item, string Linea);
}
