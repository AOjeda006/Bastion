using System.Globalization;
using System.Text.RegularExpressions;
using Bastion.Pruebas.Comun;
using Shouldly;

namespace Bastion.Arquitectura.Tests;

/// <summary>
/// La tabla de las diecisiete reglas duras y el repositorio dicen lo mismo, en los dos sentidos.
/// </summary>
/// <remarks>
/// <para>
/// <b>Un identificador que está en la tabla y no existe es tan rojo como uno que existe y no está.</b>
/// De la tabla hacia fuera: las filas son las diecisiete, y lo que cada una nombra como sitio está
/// declarado en el código. Del repositorio hacia la tabla: toda regla que un fichero cita tiene su
/// fila. Sin lo segundo, una regla citada en cien comentarios podría no tener estado; sin lo
/// primero, una fila podría señalar a un test que se borró hace tres ítems.
/// </para>
/// <para>
/// <b>El límite, y por qué no se disimula.</b> El original de las reglas es el §6 del plan maestro,
/// que vive fuera del repositorio. Esta clase NO compara los enunciados con él, y no hay ninguna
/// aserción que lo aparente: el número diecisiete está transcrito aquí igual que los dieciséis
/// módulos de <c>ElInventarioDeModulosTests</c>, y la fidelidad del texto se revisa a mano en cada
/// puerta de fase, con la orden y la huella escritas en la cabecera de la tabla.
/// </para>
/// <para>
/// Tampoco comprueba que un test nombrado haga cumplir su regla. Comprueba que existe: lo que se
/// borra o se renombra deja su fila en rojo, y lo que dice ese test lo dice su lectura.
/// </para>
/// </remarks>
public sealed partial class LasDiecisieteReglasTests
{
    private const string Tabla = "docs/dominio/reglas-duras.md";

    /// <summary>
    /// Cuántas reglas tiene el §6 del plan maestro. Es el único dato de esta clase que no se puede
    /// descubrir, porque el plan no está en el repositorio: si un día son más, esta línea es la que
    /// obliga a mirarlo, y la tabla la que dice cuáles.
    /// </summary>
    private const int ReglasDelSextoApartado = 17;

    /// <summary>La primera fase que no está cerrada y la última del §15.</summary>
    private const int PrimeraFaseAbierta = 2;

    private const int UltimaFase = 11;

    /// <summary>
    /// Dónde se buscan citas de reglas. Todo lo que se versiona y alguien lee: el código, sus
    /// pruebas, el frontal, la documentación, el esquema, el despliegue, los guiones y la CI.
    /// </summary>
    private static readonly string[] s_carpetasCitadas =
        ["src", "tests", "frontend/src", "docs", "db", "deploy", "scripts", ".github"];

    private static readonly string[] s_ficherosCitados = ["AGENTS.md", "CLAUDE.md", "README.md"];

    /// <summary>Dónde se buscan las declaraciones de tipo que la tabla nombra.</summary>
    private static readonly string[] s_carpetasDeTipos = ["src", "tests"];

    private static readonly string[] s_carpetasDeSalida = ["bin", "obj", "node_modules", "dist"];

    [Fact]
    public void La_tabla_y_los_dos_barridos_encuentran_algo()
    {
        // Las tres maneras de comparar la nada: que la tabla no se lea, que el barrido de citas no
        // abra ni un fichero y que el de tipos no encuentre ni una declaración. Cada una saldría
        // verde igual de bien que una tabla correcta.
        File.Exists(Ruta(Tabla)).ShouldBeTrue($"no hay tabla en {Tabla}: esta regla no compararía nada");

        FilasDeLaTabla().ShouldNotBeEmpty(
            $"no se ha leído ni una fila de {Tabla}: o está vacía, o el formato cambió y este lector " +
            "mira cero filas");

        CitasDelRepositorio().Keys.ShouldContain(
            "R8",
            "el barrido de citas no encuentra la R8, que es la regla más citada del repositorio: " +
            "o no está abriendo los ficheros, o la expresión ya no reconoce una cita");

        TiposDeclarados().Keys.ShouldContain(
            nameof(LasDiecisieteReglasTests),
            "el barrido de tipos no encuentra ni esta misma clase: no está leyendo tests/");
    }

    [Fact]
    public void Las_filas_son_las_diecisiete_una_vez_y_en_orden()
    {
        IReadOnlyList<string> enLaTabla = [.. FilasDeLaTabla().Select(fila => fila.Id)];

        IReadOnlyList<string> debidas =
        [
            .. Enumerable.Range(1, ReglasDelSextoApartado)
                .Select(numero => "R" + numero.ToString(CultureInfo.InvariantCulture)),
        ];

        // Entera, en orden y con repeticiones a la vista. De más es una fila sin regla detrás; de
        // menos, una regla que se quedó sin estado.
        enLaTabla.ShouldBe(
            debidas,
            $"las filas de {Tabla} no son R1 a R{ReglasDelSextoApartado}, una vez cada una y en " +
            $"orden. Sobran: [{string.Join(", ", enLaTabla.Except(debidas, StringComparer.Ordinal))}]. " +
            $"Faltan: [{string.Join(", ", debidas.Except(enLaTabla, StringComparer.Ordinal))}].");

        List<string> sinEnunciado =
            [.. FilasDeLaTabla().Where(fila => fila.Enunciado.Length == 0).Select(fila => fila.Id)];

        sinEnunciado.ShouldBeEmpty("filas sin enunciado: " + string.Join(", ", sinEnunciado));
    }

    [Fact]
    public void Toda_regla_que_cita_el_repositorio_tiene_su_fila()
    {
        SortedDictionary<string, SortedSet<string>> citas = CitasDelRepositorio();
        HashSet<string> enLaTabla = [.. FilasDeLaTabla().Select(fila => fila.Id)];

        List<string> sinFila =
        [
            .. from cita in citas
               where !enLaTabla.Contains(cita.Key)
               select $"{cita.Key} en {string.Join(", ", cita.Value.Take(3))}",
        ];

        sinFila.ShouldBeEmpty(
            $"el repositorio cita reglas que no tienen fila en {Tabla}: " + string.Join("; ", sinFila));
    }

    [Fact]
    public void Cada_estado_es_uno_de_los_tres_y_dice_donde_o_por_que()
    {
        List<string> malos = [];

        foreach (Fila fila in FilasDeLaTabla())
        {
            if (fila.Donde.Length == 0)
            {
                malos.Add($"{fila.Id}: la cuarta columna está vacía");
            }

            Match aplazada = Aplazada().Match(fila.Estado);

            if (fila.Estado == "viva")
            {
                if (!Nombrados(fila.Donde).Any(nombre => nombre.EndsWith("Tests", StringComparison.Ordinal)))
                {
                    malos.Add($"{fila.Id}: es viva y no nombra ningún test que se pondría en rojo");
                }
            }
            else if (aplazada.Success)
            {
                int fase = int.Parse(aplazada.Groups["fase"].Value, CultureInfo.InvariantCulture);

                if (fase is < PrimeraFaseAbierta or > UltimaFase)
                {
                    malos.Add(
                        $"{fila.Id}: aplazada a la fase {fase}, que no está entre la " +
                        $"{PrimeraFaseAbierta} y la {UltimaFase}");
                }
            }
            else if (fila.Estado != "no aplica")
            {
                malos.Add($"{fila.Id}: «{fila.Estado}» no es viva, aplazada a la fase N ni no aplica");
            }
        }

        malos.ShouldBeEmpty(string.Join("; ", malos));
    }

    [Fact]
    public void Lo_que_la_tabla_nombra_existe()
    {
        SortedDictionary<string, SortedSet<string>> tipos = TiposDeclarados();
        List<string> inexistentes = [];

        foreach (Fila fila in FilasDeLaTabla())
        {
            foreach (string nombre in Nombrados(fila.Donde))
            {
                string[] partes = nombre.Split('.');

                if (!tipos.TryGetValue(partes[0], out SortedSet<string>? ficheros))
                {
                    inexistentes.Add($"{fila.Id}: `{nombre}` no está declarado en src/ ni en tests/");
                }
                else if (partes.Length == 2
                    && !ficheros.Any(fichero => Regex.IsMatch(
                        File.ReadAllText(Ruta(fichero)), $@"\b{Regex.Escape(partes[1])}\b")))
                {
                    inexistentes.Add($"{fila.Id}: `{partes[0]}` existe y no tiene `{partes[1]}`");
                }
            }

            foreach (Match adr in Adr().Matches(fila.Donde))
            {
                if (!Directory.EnumerateFiles(Ruta("docs/adr"), $"adr-{adr.Groups["numero"].Value}-*.md").Any())
                {
                    inexistentes.Add($"{fila.Id}: {adr.Value} no tiene fichero en docs/adr");
                }
            }
        }

        inexistentes.ShouldBeEmpty(
            $"{Tabla} nombra sitios que no existen: " + string.Join("; ", inexistentes));
    }

    /// <summary>
    /// Las filas de la tabla: las líneas que empiezan por <c>| R</c> y un número. El encabezado
    /// empieza por <c>| Regla</c> y no casa.
    /// </summary>
    private static List<Fila> FilasDeLaTabla()
    {
        string ruta = Ruta(Tabla);

        if (!File.Exists(ruta))
        {
            return [];
        }

        return
        [
            .. from linea in File.ReadLines(ruta)
               where FilaDeRegla().IsMatch(linea)
               let celdas = linea.Trim().Trim('|').Split('|').Select(celda => celda.Trim()).ToArray()
               select new Fila(
                   celdas[0],
                   celdas.Length > 1 ? celdas[1] : "",
                   celdas.Length > 2 ? celdas[2] : "",
                   celdas.Length > 3 ? celdas[3] : ""),
        ];
    }

    /// <summary>
    /// Qué reglas se citan y en qué ficheros. La tabla no cuenta: sus identificadores son lo que
    /// se compara, no una cita.
    /// </summary>
    /// <remarks>
    /// Dos formas de citar. En prosa y en código, <c>R</c> mayúscula. Y en la cabecera de un ADR,
    /// la etiqueta en minúscula de su línea <c>tags:</c>, que es donde está la única cita de la
    /// R13. Solo ahí: fuera de esas líneas, una <c>r</c> minúscula con un número es cualquier cosa
    /// —un nombre de variable, un atributo de un SVG— y contarla daría rojos que no son reglas.
    /// </remarks>
    private static SortedDictionary<string, SortedSet<string>> CitasDelRepositorio()
    {
        SortedDictionary<string, SortedSet<string>> citas = new(StringComparer.Ordinal);

        IEnumerable<string> ficheros =
            s_carpetasCitadas.SelectMany(Ficheros).Concat(s_ficherosCitados.Where(fichero => File.Exists(Ruta(fichero))));

        foreach (string fichero in ficheros.Where(fichero => fichero != Tabla))
        {
            string texto = File.ReadAllText(Ruta(fichero));
            IEnumerable<string> encontradas = Cita().Matches(texto).Select(cita => cita.Value);

            if (fichero.StartsWith("docs/adr/", StringComparison.Ordinal))
            {
                encontradas = encontradas.Concat(
                    from linea in texto.Split('\n')
                    where linea.StartsWith("tags:", StringComparison.Ordinal)
                    from etiqueta in Etiqueta().Matches(linea)
                    select etiqueta.Value.ToUpperInvariant());
            }

            foreach (string cita in encontradas)
            {
                if (!citas.TryGetValue(cita, out SortedSet<string>? donde))
                {
                    citas[cita] = donde = new SortedSet<string>(StringComparer.Ordinal);
                }

                donde.Add(fichero);
            }
        }

        return citas;
    }

    /// <summary>Cada tipo declarado en <c>src/</c> o <c>tests/</c>, con los ficheros que lo declaran.</summary>
    private static SortedDictionary<string, SortedSet<string>> TiposDeclarados()
    {
        SortedDictionary<string, SortedSet<string>> tipos = new(StringComparer.Ordinal);

        foreach (string fichero in s_carpetasDeTipos.SelectMany(Ficheros).Where(EsCSharp))
        {
            foreach (Match declaracion in Declaracion().Matches(File.ReadAllText(Ruta(fichero))))
            {
                string nombre = declaracion.Groups["nombre"].Value;

                if (!tipos.TryGetValue(nombre, out SortedSet<string>? donde))
                {
                    tipos[nombre] = donde = new SortedSet<string>(StringComparer.Ordinal);
                }

                donde.Add(fichero);
            }
        }

        return tipos;
    }

    /// <summary>Lo que una celda nombra entre comillas invertidas con forma de tipo o de tipo y miembro.</summary>
    private static IEnumerable<string> Nombrados(string celda) =>
        Nombrado().Matches(celda).Select(nombre => nombre.Groups["nombre"].Value);

    /// <summary>Los ficheros de una carpeta, relativos a la raíz y con barras normales, sin salidas de compilación.</summary>
    private static IEnumerable<string> Ficheros(string carpeta)
    {
        string raiz = RaizDelRepositorio.Ruta();
        string donde = Ruta(carpeta);

        if (!Directory.Exists(donde))
        {
            return [];
        }

        return
        [
            .. from fichero in Directory.EnumerateFiles(donde, "*", SearchOption.AllDirectories)
               let relativo = Path.GetRelativePath(raiz, fichero).Replace('\\', '/')
               where !relativo.Split('/').Any(parte => s_carpetasDeSalida.Contains(parte, StringComparer.Ordinal))
               orderby relativo, StringComparer.Ordinal
               select relativo,
        ];
    }

    private static bool EsCSharp(string fichero) => fichero.EndsWith(".cs", StringComparison.Ordinal);

    private static string Ruta(string relativa) => Path.Combine(RaizDelRepositorio.Ruta(), relativa);

    /// <summary>
    /// Una cita de regla: una R mayúscula y un número, sueltos. Ni pegados a una palabra ni justo
    /// detrás de unas comillas, que es como aparecen los códigos inventados de los datos de prueba.
    /// </summary>
    [GeneratedRegex("""(?<![\w"])R\d+(?!\w)""")]
    private static partial Regex Cita();

    /// <summary>La etiqueta de regla de la cabecera de un ADR: <c>r</c> minúscula y un número, sueltos.</summary>
    [GeneratedRegex(@"(?<!\w)r\d+(?!\w)")]
    private static partial Regex Etiqueta();

    [GeneratedRegex(@"^\|\s*R\d+\s*\|")]
    private static partial Regex FilaDeRegla();

    [GeneratedRegex(@"^aplazada a la fase (?<fase>\d+)$")]
    private static partial Regex Aplazada();

    [GeneratedRegex(@"`(?<nombre>[A-Z]\w*(\.[A-Z]\w*)?)`")]
    private static partial Regex Nombrado();

    [GeneratedRegex(@"ADR-(?<numero>\d{4})")]
    private static partial Regex Adr();

    /// <summary>
    /// Una declaración de tipo al principio de una línea, con sus modificadores. Anclada a la línea
    /// para que un comentario que diga «la class X» no cuente como declaración.
    /// </summary>
    [GeneratedRegex(
        @"^\s*(?:(?:public|internal|private|protected|sealed|static|abstract|partial|file|readonly|ref)\s+)*(?:class|struct|interface|enum|record(?:\s+(?:class|struct))?)\s+(?<nombre>[A-Za-z_]\w*)",
        RegexOptions.Multiline)]
    private static partial Regex Declaracion();

    /// <summary>Una fila de la tabla.</summary>
    /// <param name="Id">El identificador de la regla, <c>R1</c> a <c>R17</c>.</param>
    /// <param name="Enunciado">El enunciado copiado del §6.</param>
    /// <param name="Estado">viva, aplazada a la fase N o no aplica.</param>
    /// <param name="Donde">Dónde se hace cumplir, o por qué todavía no.</param>
    private sealed record Fila(string Id, string Enunciado, string Estado, string Donde);
}
