using System.Globalization;
using System.Text.RegularExpressions;
using Bastion.Pruebas.Comun;
using Shouldly;

namespace Bastion.Arquitectura.Tests;

/// <summary>
/// La línea de estado del README dice lo que dicen las casillas del checklist de <c>docs/PLAN.md</c>.
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
/// </remarks>
public sealed partial class ElEstadoDelReadmeEsElDelPlanTests
{
    private const string Readme = "README.md";
    private const string Plan = "docs/PLAN.md";

    [Fact]
    public void El_checklist_del_plan_se_lee()
    {
        List<Casilla> casillas = Casillas();

        casillas.ShouldNotBeEmpty(
            $"no se ha leído ni una casilla de la sección «## Checklist» de {Plan}: o cambió su " +
            "encabezado, o el formato de las casillas, y la línea del README se compararía con nada");

        casillas.Select(casilla => casilla.Fase).ShouldContain(
            0, "el lector no ve las casillas de la fase 0, que están ahí desde el principio");
    }

    [Fact]
    public void La_linea_de_estado_del_readme_es_la_del_checklist()
    {
        List<Casilla> casillas = Casillas();
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

    /// <summary>Las casillas de la sección <c>## Checklist</c>, hasta el siguiente <c>##</c>.</summary>
    private static List<Casilla> Casillas() =>
    [
        .. from linea in SeccionDelChecklist()
           let casilla = CasillaDelPlan().Match(linea)
           where casilla.Success
           select new Casilla(
               int.Parse(casilla.Groups["fase"].Value, CultureInfo.InvariantCulture),
               casilla.Groups["marca"].Value == "x"),
    ];

    private static Dictionary<int, string> Encabezados() =>
        (from linea in SeccionDelChecklist()
         let encabezado = EncabezadoDeFase().Match(linea)
         where encabezado.Success
         select (
             Fase: int.Parse(encabezado.Groups["fase"].Value, CultureInfo.InvariantCulture),
             Nombre: encabezado.Groups["nombre"].Value.Trim()))
        .ToDictionary(par => par.Fase, par => par.Nombre);

    private static IEnumerable<string> SeccionDelChecklist() =>
        Lineas(Plan)
            .SkipWhile(linea => linea.Trim() != "## Checklist")
            .Skip(1)
            .TakeWhile(linea => !linea.StartsWith("## ", StringComparison.Ordinal));

    private static string[] Lineas(string relativa)
    {
        string ruta = Path.Combine(RaizDelRepositorio.Ruta(), relativa);

        File.Exists(ruta).ShouldBeTrue($"no existe {relativa}");

        return [.. File.ReadAllText(ruta).Split('\n').Select(linea => linea.TrimEnd('\r'))];
    }

    [GeneratedRegex(@"^- \[(?<marca>[ x])\] \*\*(?<fase>\d+)\.\d+ · ")]
    private static partial Regex CasillaDelPlan();

    [GeneratedRegex(@"^### Fase (?<fase>\d+) · (?<nombre>[^(]+)")]
    private static partial Regex EncabezadoDeFase();

    /// <summary>Una casilla del checklist.</summary>
    /// <param name="Fase">El número de fase del ítem: el 1 de <c>1.12</c>.</param>
    /// <param name="Hecha">Si está marcada.</param>
    private sealed record Casilla(int Fase, bool Hecha);
}
