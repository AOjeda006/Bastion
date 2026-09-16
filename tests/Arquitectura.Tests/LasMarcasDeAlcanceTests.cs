using System.Text.Json;
using System.Text.RegularExpressions;
using Bastion.Pruebas.Comun;
using Shouldly;

namespace Bastion.Arquitectura.Tests;

/// <summary>
/// Los casos marcados como no alcanzables por la API, leídos: cuáles son, por qué, y si ese porqué
/// sigue siendo verdad.
/// </summary>
/// <remarks>
/// <para>
/// <b>Una marca que nadie lee caduca en silencio, y caduca hacia el lado malo.</b> El ítem 1.9
/// marcó dos casos con el rasgo <c>Alcance = NoAlcanzablePorLaApi</c>: montan a mano un estado que
/// las validaciones de escritura impiden construir. El día que alguien relaje una de esas
/// validaciones, el camino pasa a estar vivo y la marca sigue diciendo que no lo está, así que
/// quien la lea deja de buscar cobertura justo donde empieza a hacer falta. Hasta el 1.11 ningún
/// código leía el rasgo: el filtro de los carriles es por <c>Category</c>.
/// </para>
/// <para>
/// <b>Tres afirmaciones.</b> Las marcas que hay son las declaradas, comparadas enteras y en los
/// dos sentidos. Cada declarada lleva su motivo y las <b>condiciones</b> que lo sostienen, y todas
/// se cumplen hoy: si una deja de cumplirse, el rojo nombra la marca que se ha quedado sin motivo.
/// Y la lectura puede dispararse, con un canario para el lector y otro para cada condición.
/// </para>
/// <para>
/// <b>Por el texto de todos los proyectos de pruebas, y no por reflexión.</b> Este ensamblado no
/// referencia los de pruebas ni debe, y una regla que solo viera los que alguien enumeró dejaría
/// fuera justo el proyecto nuevo. Lo que el lector no sabe leer —el rasgo en una clase, con otro
/// valor, con la clave en una constante— no se lee a medias: sale rojo como marca ilegible. Este
/// fichero se excluye a sí mismo, porque lleva la clave escrita en sus canarios.
/// </para>
/// </remarks>
public sealed class LasMarcasDeAlcanceTests
{
    private const string RutaDelOpenApi = "docs/api/openapi.json";

    /// <summary>
    /// Las condiciones que hacen inalcanzable por la API una tabla de tramos que no empiece en
    /// cero. Las cuatro juntas, porque basta que falle una para construir ese estado.
    /// </summary>
    private static readonly Condicion[] s_tramosQueEmpiezanEnCero =
    [
        new(
            "el primer tramo de cada destino empieza en cero, y lo afirma su caso",
            evidencia => evidencia.Casos.Contains(
                "CrearLineaTarifaTests.El_primer_tramo_de_un_destino_tiene_que_empezar_en_cero")
                ? null
                : "ya no existe CrearLineaTarifaTests.El_primer_tramo_de_un_destino_tiene_que_empezar_en_cero"),
        new(
            "la cantidad por la que se pregunta no puede ser negativa",
            evidencia => MinimoDeLaCantidadPedida(evidencia.OpenApi) == 0m
                ? null
                : "el parámetro «cantidad» de GET /tarifas/{codigo}/precio ya no tiene mínimo 0"),
        new(
            "el tramo de una línea no se modifica",
            evidencia => evidencia.OpenApi.GetProperty("components").GetProperty("schemas")
                .GetProperty("ModificarLineaTarifaDto").GetProperty("properties")
                .TryGetProperty("cantidadDesde", out _)
                ? "ModificarLineaTarifaDto ha ganado «cantidadDesde»"
                : null),
        new(
            "las líneas de una tarifa no se borran",
            evidencia => evidencia.OpenApi.GetProperty("paths").EnumerateObject()
                .Any(ruta => ruta.Name.StartsWith("/api/v1/catalogo/tarifas", StringComparison.Ordinal)
                    && ruta.Value.TryGetProperty("delete", out _))
                ? "hay un DELETE bajo /api/v1/catalogo/tarifas"
                : null),
    ];

    /// <summary>Las marcas que existen a propósito, con su motivo y lo que las sostiene.</summary>
    private static readonly MarcaDeclarada[] s_declaradas =
    [
        new(
            "ElAntepasadoMasCercanoGanaTests.La_cantidad_por_debajo_del_primer_tramo_no_encuentra_nada",
            "Pide una cantidad menor que el primer tramo de la única línea. Con el primer tramo en " +
            "cero, sin tramos modificables ni líneas borrables y sin cantidades negativas, no hay " +
            "cantidad por debajo; el caso dice que, si la hubiera, no hay precio y no hay cero.",
            s_tramosQueEmpiezanEnCero),
        new(
            "ElAntepasadoMasCercanoGanaTests.Una_categoria_cercana_sin_tramo_aplicable_deja_pasar_a_la_de_arriba",
            "Un destino con líneas que no cubren la cantidad. Con las mismas cuatro condiciones, un " +
            "destino que tiene alguna línea las cubre todas y el ascenso nunca pasa de nivel; el " +
            "caso dice que la precedencia es sobre el par (destino, tramo) y no sobre el destino.",
            s_tramosQueEmpiezanEnCero),
    ];

    private static readonly Regex s_laMarca = new(
        @"^\[Trait\(""Alcance"", ""NoAlcanzablePorLaApi""\)\]$",
        RegexOptions.None,
        TimeSpan.FromSeconds(1));

    private static readonly Regex s_unTipo = new(
        @"\b(?:class|record|struct|interface)\s+(?<nombre>\w+)",
        RegexOptions.None,
        TimeSpan.FromSeconds(1));

    private static readonly Regex s_unMetodo = new(
        @"^(?:public|internal|private|protected)\s[^=]*?\b(?<nombre>\w+)\s*\(",
        RegexOptions.None,
        TimeSpan.FromSeconds(1));

    [Fact]
    public void Las_marcas_de_alcance_son_las_declaradas()
    {
        Lectura lectura = LeerLosProyectosDePruebas();

        lectura.Ilegibles.ShouldBeEmpty(
            "estas líneas llevan la clave del rasgo y el lector no sabe a qué caso marcan. Una " +
            "marca va en el método, con el valor NoAlcanzablePorLaApi escrito tal cual");

        lectura.Marcas.Order(StringComparer.Ordinal).ToArray().ShouldBe(
            [.. s_declaradas.Select(marca => marca.Caso).Order(StringComparer.Ordinal)],
            customMessage: "las marcas del código y las declaradas aquí no coinciden. Una marca " +
                "nueva se declara con su motivo; una que se quita, se borra de la lista");
    }

    [Fact]
    public void Cada_marca_sigue_sostenida_por_las_condiciones_de_su_motivo()
    {
        Lectura lectura = LeerLosProyectosDePruebas();
        using var openApi = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RaizDelRepositorio.Ruta(), RutaDelOpenApi)));
        Evidencia evidencia = new(lectura.Casos, openApi.RootElement);

        List<string> sinMotivo = [];

        foreach (MarcaDeclarada marca in s_declaradas)
        {
            if (string.IsNullOrWhiteSpace(marca.Motivo) || marca.Condiciones.Count == 0)
            {
                sinMotivo.Add($"{marca.Caso}: no dice por qué ni qué lo sostiene");
            }

            sinMotivo.AddRange(
                from condicion in marca.Condiciones
                let incumplida = condicion.Incumplida(evidencia)
                where incumplida is not null
                select $"{marca.Caso}: «{condicion.Nombre}» ya no se cumple — {incumplida}");
        }

        sinMotivo.ShouldBeEmpty(
            "estas marcas dicen que su caso no es alcanzable por la API y lo que lo impedía ha " +
            "cambiado. El caso puede estar vivo: hay que quitarle la marca y cubrir el camino, o " +
            "escribir el motivo nuevo");
    }

    [Fact]
    public void La_lectura_de_marcas_puede_dispararse()
    {
        Lectura buena = Leer(
            "Canario.cs",
            [
                "public sealed class Canario",
                "{",
                "    [Fact]",
                "    [Trait(\"Alcance\", \"NoAlcanzablePorLaApi\")]",
                "    public void Un_caso()",
                "    {",
                "    }",
                "}",
            ]);
        buena.Marcas.ShouldBe(["Canario.Un_caso"]);
        buena.Casos.ShouldContain("Canario.Un_caso");
        buena.Ilegibles.ShouldBeEmpty();

        Lectura enLaClase = Leer(
            "Canario.cs",
            ["[Trait(\"Alcance\", \"NoAlcanzablePorLaApi\")]", "public sealed class Canario", "{", "}"]);
        enLaClase.Ilegibles.Count.ShouldBe(1, "una marca en la clase tiene que salir ilegible");

        Lectura otroValor = Leer(
            "Canario.cs",
            ["public sealed class Canario", "{", "    [Trait(\"Alcance\", \"Otro\")]", "    public void Uno()", "}"]);
        otroValor.Ilegibles.Count.ShouldBe(1, "un valor que no es el declarado tiene que salir ilegible");

        // Y cada condición se incumple con la evidencia que la contradice: si alguna no pudiera
        // dispararse, la regla de arriba estaría sostenida por una comprobación que no mira nada.
        using var roto = JsonDocument.Parse(
            """
            {
              "paths": {
                "/api/v1/catalogo/tarifas/{codigo}/precio": {
                  "get": { "parameters": [ { "name": "cantidad", "schema": { "minimum": -1 } } ] }
                },
                "/api/v1/catalogo/tarifas/lineas/{id}": { "delete": {} }
              },
              "components": {
                "schemas": { "ModificarLineaTarifaDto": { "properties": { "cantidadDesde": {} } } }
              }
            }
            """);
        Evidencia contraria = new(new HashSet<string>(StringComparer.Ordinal), roto.RootElement);

        s_tramosQueEmpiezanEnCero
            .Where(condicion => condicion.Incumplida(contraria) is null)
            .Select(condicion => condicion.Nombre)
            .ShouldBeEmpty("estas condiciones no se incumplen ni con la evidencia que las niega");
    }

    private static decimal? MinimoDeLaCantidadPedida(JsonElement openApi) =>
        openApi.GetProperty("paths").GetProperty("/api/v1/catalogo/tarifas/{codigo}/precio")
            .GetProperty("get").GetProperty("parameters").EnumerateArray()
            .Where(parametro => parametro.GetProperty("name").GetString() == "cantidad")
            .Select(parametro => parametro.GetProperty("schema").TryGetProperty("minimum", out JsonElement minimo)
                ? minimo.GetDecimal()
                : (decimal?)null)
            .SingleOrDefault();

    private static Lectura LeerLosProyectosDePruebas()
    {
        string pruebas = Path.Combine(RaizDelRepositorio.Ruta(), "tests");
        List<string> marcas = [];
        HashSet<string> casos = new(StringComparer.Ordinal);
        List<string> ilegibles = [];

        foreach (string fichero in Directory.EnumerateFiles(pruebas, "*.cs", SearchOption.AllDirectories))
        {
            string[] tramos = fichero.Split(Path.DirectorySeparatorChar, '/');
            if (tramos.Contains("bin", StringComparer.Ordinal)
                || tramos.Contains("obj", StringComparer.Ordinal)
                || Path.GetFileName(fichero) == nameof(LasMarcasDeAlcanceTests) + ".cs")
            {
                continue;
            }

            Lectura lectura = Leer(
                Path.GetRelativePath(RaizDelRepositorio.Ruta(), fichero).Replace('\\', '/'),
                File.ReadAllLines(fichero));
            marcas.AddRange(lectura.Marcas);
            casos.UnionWith(lectura.Casos);
            ilegibles.AddRange(lectura.Ilegibles);
        }

        // Si el lector dejara de ver casos —otra carpeta, otra forma de escribirlos—, las dos
        // reglas de arriba se cumplirían en vacío. Cientos de casos es lo que hay hoy.
        casos.Count.ShouldBeGreaterThan(500, "el lector no ve los casos de los proyectos de pruebas");

        return new Lectura(marcas, casos, ilegibles);
    }

    /// <summary>
    /// Lee un fichero: los atributos se acumulan hasta la declaración que decoran, y ahí se decide
    /// si decoran un caso, y si la marca está bien escrita y en su sitio.
    /// </summary>
    private static Lectura Leer(string nombre, string[] lineas)
    {
        List<string> marcas = [];
        HashSet<string> casos = new(StringComparer.Ordinal);
        List<string> ilegibles = [];
        List<(int Numero, string Texto)> atributos = [];
        int corchetesAbiertos = 0;
        string tipo = "?";

        for (int numero = 1; numero <= lineas.Length; numero++)
        {
            string linea = lineas[numero - 1].Trim();

            // Un atributo que sigue en la línea siguiente —un `[InlineData(` partido— es parte del
            // mismo atributo, y no la declaración que decora.
            if (linea.StartsWith('[') || corchetesAbiertos > 0)
            {
                atributos.Add((numero, linea));
                corchetesAbiertos += linea.Count(caracter => caracter == '[') - linea.Count(caracter => caracter == ']');
                continue;
            }

            if (linea.Length == 0 || linea.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            Match deTipo = s_unTipo.Match(linea);
            Match deMetodo = s_unMetodo.Match(linea);
            string? metodo = !deTipo.Success && deMetodo.Success ? deMetodo.Groups["nombre"].Value : null;

            if (deTipo.Success)
            {
                tipo = deTipo.Groups["nombre"].Value;
            }

            if (metodo is not null && atributos.Any(atributo =>
                atributo.Texto.StartsWith("[Fact", StringComparison.Ordinal)
                || atributo.Texto.StartsWith("[Theory", StringComparison.Ordinal)))
            {
                casos.Add($"{tipo}.{metodo}");
            }

            foreach ((int enLinea, string texto) in atributos)
            {
                if (!texto.Contains("\"Alcance\"", StringComparison.Ordinal))
                {
                    continue;
                }

                if (metodo is not null && s_laMarca.IsMatch(texto))
                {
                    marcas.Add($"{tipo}.{metodo}");
                }
                else
                {
                    ilegibles.Add($"{nombre}:{enLinea} — {texto}");
                }
            }

            atributos.Clear();
        }

        return new Lectura(marcas, casos, ilegibles);
    }

    private sealed record MarcaDeclarada(string Caso, string Motivo, IReadOnlyList<Condicion> Condiciones);

    /// <summary>Una condición del motivo: <c>null</c> mientras se cumple, y si no, qué ha cambiado.</summary>
    private sealed record Condicion(string Nombre, Func<Evidencia, string?> Incumplida);

    private sealed record Evidencia(IReadOnlySet<string> Casos, JsonElement OpenApi);

    private sealed record Lectura(
        IReadOnlyList<string> Marcas,
        IReadOnlySet<string> Casos,
        IReadOnlyList<string> Ilegibles);
}
