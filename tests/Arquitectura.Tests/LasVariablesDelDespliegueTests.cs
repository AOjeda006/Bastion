using System.Reflection;
using System.Text.RegularExpressions;
using Bastion.Api.Arranque;
using Bastion.Pruebas.Comun;
using Shouldly;

namespace Bastion.Arquitectura.Tests;

/// <summary>
/// Las variables del despliegue: las que documenta <c>deploy/.env.example</c>, las que interpola
/// el <c>compose</c>, las que lee la semilla y las que escribe la CI son la misma lista.
/// </summary>
/// <remarks>
/// <para>
/// El ejemplo es el único sitio donde un operador puede mirar qué tiene que poner. Estaba
/// completo, y nada lo decía: una variable nueva en el <c>compose</c> sin su línea en el ejemplo
/// sale verde, y el despliegue que la necesita arranca sin ella —cerrado, si es de la semilla, o
/// sin arrancar, si lleva <c>:?</c>—, con el registro como única pista.
/// </para>
/// <para>
/// Las fuentes se comparan de dos en dos y enteras. El ejemplo y el <c>compose</c>, en los dos
/// sentidos: de menos es una variable sin documentar, de más una documentada que ya no lee nadie.
/// Las de la semilla, contra las constantes de <see cref="SemillaDeArranque"/>, que es el código
/// que de verdad las lee. Y las que escribe la CI, contenidas en el ejemplo: la CI se fabrica su
/// propio fichero, y una variable que solo existe ahí es una que el operador no tiene.
/// </para>
/// </remarks>
public sealed partial class LasVariablesDelDespliegueTests
{
    private const string Ejemplo = "deploy/.env.example";
    private const string Compose = "deploy/docker-compose.yml";
    private const string Flujo = ".github/workflows/ci.yml";
    private const string PrefijoDeLaSemilla = "BASTION_SEMILLA_";

    [Fact]
    public void Las_cuatro_fuentes_se_leen()
    {
        DelEjemplo().ShouldNotBeEmpty($"no se ha leído ni una variable de {Ejemplo}");
        DelCompose().ShouldNotBeEmpty($"no se ha leído ni una interpolación de {Compose}");
        DeLaSemilla().ShouldNotBeEmpty($"no se ha leído ni una constante Variable* de {nameof(SemillaDeArranque)}");
        DeLaCi().ShouldNotBeEmpty(
            $"no se ha leído ni una variable del paso «Preparar deploy/.env» de {Flujo}: o el paso " +
            "cambió de nombre, o ya no escribe el fichero con echo");
    }

    [Fact]
    public void El_ejemplo_documenta_lo_que_interpola_el_compose_y_nada_mas()
    {
        SortedSet<string> ejemplo = DelEjemplo();
        SortedSet<string> compose = DelCompose();

        ejemplo.ShouldBe(
            compose,
            $"{Ejemplo} y {Compose} no nombran las mismas variables. Sin documentar: " +
            $"[{string.Join(", ", compose.Except(ejemplo, StringComparer.Ordinal))}]. Documentadas " +
            $"y sin nadie que las lea: [{string.Join(", ", ejemplo.Except(compose, StringComparer.Ordinal))}].");
    }

    [Fact]
    public void Las_de_la_semilla_son_las_que_lee_su_codigo()
    {
        SortedSet<string> codigo = DeLaSemilla();
        SortedSet<string> compose = new(DelCompose().Where(DeSemilla), StringComparer.Ordinal);
        SortedSet<string> ejemplo = new(DelEjemplo().Where(DeSemilla), StringComparer.Ordinal);

        compose.ShouldBe(codigo, $"las {PrefijoDeLaSemilla}* de {Compose} no son las constantes de {nameof(SemillaDeArranque)}");
        ejemplo.ShouldBe(codigo, $"las {PrefijoDeLaSemilla}* de {Ejemplo} no son las constantes de {nameof(SemillaDeArranque)}");
    }

    [Fact]
    public void La_CI_solo_escribe_variables_que_el_ejemplo_documenta()
    {
        List<string> soloEnLaCi = [.. DeLaCi().Except(DelEjemplo(), StringComparer.Ordinal)];

        soloEnLaCi.ShouldBeEmpty(
            $"{Flujo} escribe variables que {Ejemplo} no documenta: " + string.Join(", ", soloEnLaCi));
    }

    [Fact]
    public void Algun_valor_del_ejemplo_lleva_un_espacio_a_proposito()
    {
        // `herramientas/docker.md`: el fichero lo leen dos gramáticas, y un ejemplo sin espacios
        // esconde toda la clase de fallo que viene de cargarlo con la que no es.
        Lineas(Ejemplo)
            .Select(linea => Asignacion().Match(linea))
            .Where(asignacion => asignacion.Success)
            .Any(asignacion => asignacion.Groups["valor"].Value.Contains(' ', StringComparison.Ordinal))
            .ShouldBeTrue($"ningún valor de {Ejemplo} lleva un espacio");
    }

    private static bool DeSemilla(string variable) => variable.StartsWith(PrefijoDeLaSemilla, StringComparison.Ordinal);

    private static SortedSet<string> DelEjemplo() =>
        new(
            from linea in Lineas(Ejemplo)
            let asignacion = Asignacion().Match(linea)
            where asignacion.Success
            select asignacion.Groups["nombre"].Value,
            StringComparer.Ordinal);

    private static SortedSet<string> DelCompose() =>
        new(
            from interpolacion in Interpolacion().Matches(string.Join('\n', Lineas(Compose)))
            select interpolacion.Groups["nombre"].Value,
            StringComparer.Ordinal);

    private static SortedSet<string> DeLaSemilla() =>
        new(
            from campo in typeof(SemillaDeArranque).GetFields(BindingFlags.Public | BindingFlags.Static)
            where campo.IsLiteral && campo.Name.StartsWith("Variable", StringComparison.Ordinal)
            select (string)campo.GetRawConstantValue()!,
            StringComparer.Ordinal);

    /// <summary>
    /// Lo que el paso «Preparar deploy/.env» escribe: las líneas <c>echo "NOMBRE=…"</c> entre su
    /// nombre y el <c>} &gt; deploy/.env</c> que las vuelca.
    /// </summary>
    private static SortedSet<string> DeLaCi()
    {
        string[] lineas = Lineas(Flujo);
        int inicio = Array.FindIndex(lineas, linea => linea.Trim() == "- name: Preparar deploy/.env");

        if (inicio < 0)
        {
            return new SortedSet<string>(StringComparer.Ordinal);
        }

        int fin = Array.FindIndex(lineas, inicio, linea => linea.Trim() == "} > deploy/.env");

        return new(
            from linea in lineas[inicio..(fin < 0 ? inicio : fin)]
            let eco = Eco().Match(linea)
            where eco.Success
            select eco.Groups["nombre"].Value,
            StringComparer.Ordinal);
    }

    private static string[] Lineas(string relativa)
    {
        string ruta = Path.Combine(RaizDelRepositorio.Ruta(), relativa);

        File.Exists(ruta).ShouldBeTrue($"no existe {relativa}");

        return [.. File.ReadAllText(ruta).Split('\n').Select(linea => linea.TrimEnd('\r'))];
    }

    [GeneratedRegex(@"^(?<nombre>[A-Z_][A-Z0-9_]*)=(?<valor>.*)$")]
    private static partial Regex Asignacion();

    [GeneratedRegex(@"(?<!\$)\$\{(?<nombre>[A-Z_][A-Z0-9_]*)")]
    private static partial Regex Interpolacion();

    [GeneratedRegex("""^\s*echo "(?<nombre>[A-Z_][A-Z0-9_]*)=""")]
    private static partial Regex Eco();
}
