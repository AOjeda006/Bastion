using System.Text.RegularExpressions;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Shouldly;

namespace Bastion.Arquitectura.Tests;

/// <summary>
/// Ningún identificador fiscal ni ningún IBAN con forma de real se queda escrito en el
/// repositorio: ni en <i>fixtures</i>, ni en semillas, ni en ADR, ni en comentarios.
/// </summary>
/// <remarks>
/// <para>
/// <b>Esto era prosa y ahora es una regla.</b> El ítem 1.5 escribió que el DNI de ocho cifras
/// consecutivas de todos los manuales existe y es de alguien, y a la vez lo dejó vivo en cinco
/// ficheros que el ítem no tocaba. Se limpiaron a mano, y una limpieza a mano se deshace la
/// siguiente vez que alguien necesita un valor de ejemplo y coge el primero que encuentra. Lo que
/// impide que se repita es este barrido.
/// </para>
/// <para>
/// <b>Por qué un IBAN es peor que un NIF.</b> Un identificador fiscal identifica; un IBAN
/// <b>cobra</b>. Y ninguno de los dos se queda en el fichero: viajan al artefacto de resultados de
/// la CI, a su registro, y al historial de git, donde no hay plazo de supresión que valga.
/// </para>
/// <para>
/// <b>La lista se declara por lo que se PERMITE, no por lo que se prohíbe</b>, y esa es la
/// decisión de diseño de este fichero. Una lista de valores prohibidos tendría que llevarlos
/// escritos, y escribir un IBAN real para prohibirlo es exactamente el daño que se está evitando —
/// «tampoco para señalarlos». Así que cada forma declara su <b>espacio inventado</b>: el relleno
/// con el que se fabrican los ejemplos de este proyecto. Todo lo que tenga la forma y no esté en
/// ese espacio es rojo, sin que nadie haya tenido que teclear un solo dato de nadie.
/// </para>
/// <para>
/// <b>Y por eso los mensajes no publican lo que encuentran.</b> Un fallo dice el fichero, la línea
/// y la forma enmascarada. Si el barrido caza un IBAN de verdad y lo imprime, el registro de la CI
/// acaba conteniendo justo lo que la regla existe para sacar del repositorio.
/// </para>
/// <para>
/// <b>Lo que esta regla NO promete.</b> Que ningún identificador válido sea de nadie: todo
/// identificador válido es de alguien o lo será, y eso no lo arregla ningún generador. Lo que sí
/// promete es lo que el 1.5 dejó escrito como la regla cumplible: <b>no pegar el ejemplo
/// conocido</b>, porque el ejemplo conocido es justo el que se puede atribuir a una persona.
/// </para>
/// </remarks>
public sealed class NingunDatoConFormaDeRealTests
{
    /// <summary>Las formas que este barrido conoce. Crece con las fases.</summary>
    /// <remarks>
    /// Hoy dos. La fase 3 traerá la referencia del mandato SEPA, que también es única y también
    /// identifica. Añadir una forma es añadir su línea aquí y su detector abajo, y la regla
    /// <c>Las_formas_declaradas_son_las_que_el_barrido_detecta</c> no deja hacer una sin la otra.
    /// </remarks>
    private static readonly string[] s_declaradas =
    [
        "IBAN",
        "Identificador fiscal español",
    ];

    /// <summary>Dónde se barre. Todo lo que se publica en el repositorio.</summary>
    /// <remarks>
    /// <c>ERP-PLAN-MAESTRO.md</c> no está: lo aporta el usuario fuera del repositorio y está en
    /// <c>.gitignore</c>, así que no se publica y no es de este barrido arreglarlo.
    /// </remarks>
    private static readonly string[] s_raices =
    [
        ".github",
        "docs",
        "frontend/src",
        "scripts",
        "src",
        "tests",
    ];

    /// <summary>Lo que no se lee: ni salidas de compilación ni dependencias ajenas.</summary>
    private static readonly string[] s_carpetasExcluidas =
    [
        "bin",
        "dist",
        "node_modules",
        "obj",
    ];

    /// <summary>
    /// La lista declarada y los detectores implementados son la misma lista, en los dos sentidos.
    /// </summary>
    /// <remarks>
    /// Sin esta comparación, añadir un detector y olvidarse de la lista —o al revés— deja el
    /// barrido diciendo que cubre algo que no mira, que es la clase de mentira que este proyecto
    /// persigue desde el ítem 1.2.
    /// </remarks>
    [Fact]
    public void Las_formas_declaradas_son_las_que_el_barrido_detecta()
    {
        IReadOnlyList<string> implementadas =
            [.. Formas().Select(forma => forma.Nombre).Order(StringComparer.Ordinal)];

        implementadas.ShouldBe(
            [.. s_declaradas.Order(StringComparer.Ordinal)],
            customMessage: "la lista declarada de formas y los detectores no coinciden");
    }

    /// <summary>
    /// El canario: cada detector encuentra un dato con su forma y lo da por NO inventado.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es la afirmación sin la cual todo lo demás sale verde por la peor razón. Un detector con la
    /// expresión mal escrita no encuentra nada, y «no he encontrado ningún dato real» y «no sé
    /// buscar» dan exactamente el mismo resultado.
    /// </para>
    /// <para>
    /// Los canarios se <b>construyen en tiempo de ejecución</b> —probando controles hasta que el
    /// propio objeto de valor los acepta— y no se escriben como literales. Un canario con forma de
    /// real escrito en este fichero sería la primera infracción de la regla que este fichero
    /// defiende.
    /// </para>
    /// </remarks>
    [Fact]
    public void Cada_detector_encuentra_lo_que_dice_buscar()
    {
        foreach (Forma forma in Formas())
        {
            string canario = forma.Canario();
            string texto = $"el valor de ejemplo es {canario}, entre otras cosas";

            // Sobre un booleano y NO con `ShouldContain(canario)`: al fallar, Shouldly imprime
            // la colección esperada y la encontrada, y las dos llevarían el canario dentro. Un
            // canario en el registro de la CI es lo mismo que la regla persigue.
            forma.Buscar(texto).Any(hallado =>
                string.Equals(hallado, canario, StringComparison.Ordinal)).ShouldBeTrue(
                $"el detector de «{forma.Nombre}» no encuentra un dato con su forma metido en " +
                "una frase, así que el barrido de abajo no puede encontrar ninguno tampoco");

            forma.EsInventado(canario).ShouldBeFalse(
                $"el detector de «{forma.Nombre}» da por inventado un dato que no lo es, así que " +
                "el barrido lo dejaría pasar");

            forma.EsInventado(forma.Inventado()).ShouldBeTrue(
                $"el detector de «{forma.Nombre}» no reconoce como inventado el relleno con el " +
                "que este proyecto fabrica sus ejemplos, así que el barrido saldría rojo con " +
                "todas las fixtures legítimas");
        }
    }

    /// <summary>El barrido llega a los ficheros. Si no, no está barriendo nada.</summary>
    [Fact]
    public void El_barrido_lee_los_ficheros_del_repositorio()
    {
        IReadOnlyList<string> ficheros = [.. Ficheros()];

        ficheros.ShouldNotBeEmpty(
            "el barrido no ha leído ni un fichero, así que la regla de abajo se cumple en vacío. " +
            "Mira si las raíces declaradas siguen existiendo");

        // Y que llegue a TODAS las raíces: una raíz renombrada dejaría de barrerse en silencio y
        // el recuento total seguiría siendo alto.
        foreach (string raiz in s_raices)
        {
            ficheros.ShouldContain(
                fichero => fichero.Replace('\\', '/').Contains('/' + raiz + '/', StringComparison.Ordinal)
                    || fichero.Replace('\\', '/').Contains("/" + raiz.Split('/')[0] + "/", StringComparison.Ordinal),
                customMessage: $"el barrido no ha leído ni un fichero de «{raiz}»");
        }
    }

    /// <summary>
    /// Y ningún dato con forma de real, fuera del espacio inventado, se queda escrito.
    /// </summary>
    [Fact]
    public void Ningun_dato_con_forma_de_real_se_queda_escrito()
    {
        List<string> hallazgos = [];
        IReadOnlyList<Forma> formas = Formas();

        foreach (string fichero in Ficheros())
        {
            string[] lineas = File.ReadAllLines(fichero);

            for (int numero = 0; numero < lineas.Length; numero++)
            {
                foreach (Forma forma in formas)
                {
                    hallazgos.AddRange(
                        from encontrado in forma.Buscar(lineas[numero])
                        where !forma.EsInventado(encontrado)
                        select $"{Relativo(fichero)}:{numero + 1} — {forma.Nombre} " +
                               $"«{Enmascarar(encontrado)}»");
                }
            }
        }

        hallazgos.ShouldBeEmpty(
            "estos sitios llevan escrito un dato con forma de real que no sale del espacio " +
            "inventado de este proyecto. Un identificador válido es de alguien, y de un " +
            "repositorio no se borra: sustitúyelo por un valor de relleno con su control " +
            "calculado, como hace el resto de la suite." + Environment.NewLine + "· " +
            string.Join(Environment.NewLine + "· ", hallazgos.Order(StringComparer.Ordinal)));
    }

    private static IReadOnlyList<Forma> Formas() =>
    [
        new Forma(
            "IBAN",
            s_elIban,
            valor => Iban.Intentar(valor, out _),
            // El espacio inventado: la cuenta, de relleno. Lo declara `IbanesInventados`, y aquí
            // se repite porque este carril no ve aquel ensamblado — que es lo que hace que las
            // dos declaraciones se comprueben contra la misma realidad en vez de leerse la una a
            // la otra.
            valor => EsRelleno(valor[4..]),
            () => ConControlBuscado("ES", Movido(Relleno(20)), valor => Iban.Intentar(valor, out _)),
            () => ConControlBuscado("ES", Relleno(20), valor => Iban.Intentar(valor, out _))),

        new Forma(
            "Identificador fiscal español",
            s_elNif,
            valor => Nif.Intentar(valor, out _),
            EsIdentificadorInventado,
            () => ConControlBuscado(string.Empty, Movido(Relleno(8)), valor => Nif.Intentar(valor, out _)),
            () => ConControlBuscado(string.Empty, Relleno(8), valor => Nif.Intentar(valor, out _))),
    ];

    // Dos letras de país, dos dígitos de control y de once a treinta caracteres más. El filtro de
    // verdad no es esta expresión: es el objeto de valor, que solo da por bueno lo que pasa el
    // mod-97 y mide lo que mide su país. Sin él, cualquier identificador de veinte caracteres en
    // mayúsculas sería un hallazgo.
    private static readonly Regex s_elIban =
        new(@"\b[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}\b", RegexOptions.None, TimeSpan.FromSeconds(2));

    // Una inicial opcional, siete u ocho cifras y el control. Igual que arriba: quien decide es
    // `Nif`, que comprueba el carácter de control de verdad.
    private static readonly Regex s_elNif =
        new(@"\b[A-Z]?[0-9]{7,8}[A-Z0-9]\b", RegexOptions.None, TimeSpan.FromSeconds(2));

    /// <summary>
    /// El espacio inventado de los identificadores fiscales: el cuerpo, sin la inicial ni el
    /// control, tiene que ser relleno.
    /// </summary>
    private static bool EsIdentificadorInventado(string valor) =>
        EsRelleno(char.IsAsciiDigit(valor[0]) ? valor[..^1] : valor[1..^1]);

    /// <summary>
    /// Qué cuenta como relleno: un carácter repetido, dos alternados, o cuatro ceros por delante.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Son las convenciones que la suite ya usaba</b>, no una inventada para este barrido: los
    /// contratos van con cifras repetidas y los escenarios de integración con una secuencia baja
    /// tras seis ceros. Escribir una definición más estrecha habría puesto rojo medio repositorio
    /// el primer día, y el rojo no habría señalado ningún dato de nadie — habría señalado que la
    /// definición estaba mal.
    /// </para>
    /// <para>
    /// <b>Y lo que promete es lo que se puede prometer.</b> No que ninguno de estos números sea de
    /// alguien: un identificador válido es de alguien o lo será, y eso no lo arregla ninguna
    /// definición. Lo que promete es que <b>no se ha pegado el ejemplo conocido</b>, que es el que
    /// se puede atribuir a una persona porque circula con su nombre al lado.
    /// </para>
    /// </remarks>
    private static bool EsRelleno(string cuerpo) =>
        cuerpo.All(caracter => caracter == cuerpo[0])
        || cuerpo.Where((_, posicion) => posicion % 2 == 0).All(caracter => caracter == cuerpo[0])
            && cuerpo.Where((_, posicion) => posicion % 2 == 1).All(caracter => caracter == cuerpo[1])
        || cuerpo.StartsWith("0000", StringComparison.Ordinal);

    /// <summary>El relleno de una longitud: el dígito nueve, repetido.</summary>
    private static string Relleno(int cuantos) => new('9', cuantos);

    /// <summary>
    /// Y el relleno con la última posición movida, que es el canario: por la definición de
    /// <see cref="EsRelleno"/> deja de ser relleno, y es lo mínimo que hace falta para dejar de
    /// serlo. Escrito así, este fichero no lleva dentro ningún número copiado de ninguna parte.
    /// </summary>
    private static string Movido(string relleno) => relleno[..^1] + '8';

    /// <summary>
    /// Construye un dato válido probando controles hasta que el objeto de valor lo acepta.
    /// </summary>
    /// <remarks>
    /// Por fuerza bruta y no calculando el control: así este fichero no lleva dentro una segunda
    /// implementación del mod-97 ni del algoritmo del NIF, que serían dos sitios más donde
    /// equivocarse, y lo que construye es exactamente lo que el objeto de valor da por bueno.
    /// </remarks>
    private static string ConControlBuscado(
        string prefijo, string cuerpo, TryPattern intentar)
    {
        const string Alfabeto = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        // Un IBAN lleva el control DELANTE, entre el país y la cuenta; un NIF, detrás. Las dos
        // formas se cubren probando las dos colocaciones.
        foreach (char primero in Alfabeto)
        {
            foreach (char segundo in Alfabeto)
            {
                string delante = prefijo + primero + segundo + cuerpo;
                if (prefijo.Length > 0 && intentar(delante))
                {
                    return delante;
                }

                string detras = prefijo + cuerpo + primero;
                if (prefijo.Length == 0 && intentar(detras))
                {
                    return detras;
                }
            }
        }

        throw new InvalidOperationException(
            $"no se ha encontrado ningún control que valide «{prefijo}…{cuerpo.Length} cifras», " +
            "así que el canario de este barrido no se puede construir y la regla no significa " +
            "nada. Mira si el objeto de valor ha cambiado de reglas");
    }

    private static IEnumerable<string> Ficheros()
    {
        string raizDelRepositorio = Ensamblados.Raiz();

        foreach (string raiz in s_raices)
        {
            string carpeta = Path.Combine(
                raizDelRepositorio, raiz.Replace('/', Path.DirectorySeparatorChar));

            if (!Directory.Exists(carpeta))
            {
                continue;
            }

            foreach (string fichero in Directory.EnumerateFiles(
                carpeta, "*", SearchOption.AllDirectories))
            {
                if (!Excluido(fichero) && !EsBinario(fichero))
                {
                    yield return fichero;
                }
            }
        }
    }

    private static bool Excluido(string fichero) =>
        fichero.Split(Path.DirectorySeparatorChar, '/')
            .Any(tramo => s_carpetasExcluidas.Contains(tramo, StringComparer.Ordinal));

    // Los binarios no se leen como texto, y los ficheros de bloqueo llevan huellas en base64 que
    // no son datos de nadie y que dispararían el detector sin motivo.
    private static bool EsBinario(string fichero) =>
        Path.GetExtension(fichero).ToUpperInvariant() is ".DLL" or ".EXE" or ".PDB" or ".PNG"
            or ".JPG" or ".ICO" or ".WOFF" or ".WOFF2" or ".ZIP" or ".GZ"
        || Path.GetFileName(fichero).Equals("packages.lock.json", StringComparison.Ordinal)
        || Path.GetFileName(fichero).Equals("package-lock.json", StringComparison.Ordinal);

    private static string Relativo(string fichero) =>
        Path.GetRelativePath(Ensamblados.Raiz(), fichero).Replace('\\', '/');

    // Ni siquiera en el mensaje de fallo: si el barrido caza un dato de verdad y lo imprime, el
    // registro de la CI acaba guardando justo lo que la regla existe para sacar del repositorio.
    private static string Enmascarar(string valor) =>
        valor.Length <= 4 ? new string('*', valor.Length) : valor[..2] + new string('*', valor.Length - 4) + valor[^2..];

    private delegate bool TryPattern(string valor);

    /// <summary>Una forma de dato sensible: cómo se encuentra y qué se le permite.</summary>
    /// <param name="Nombre">Como aparece en la lista declarada.</param>
    /// <param name="Expresion">Qué tiene la pinta. El filtro grueso.</param>
    /// <param name="EsValido">Y el fino: si el objeto de valor lo da por bueno.</param>
    /// <param name="EsInventado">Si sale del espacio de relleno de este proyecto.</param>
    /// <param name="Canario">Un dato con la forma que NO es inventado, construido al vuelo.</param>
    /// <param name="Inventado">Y uno que sí lo es.</param>
    private sealed record Forma(
        string Nombre,
        Regex Expresion,
        Func<string, bool> EsValido,
        Func<string, bool> EsInventado,
        Func<string> Canario,
        Func<string> Inventado)
    {
        internal IEnumerable<string> Buscar(string texto) =>
            from Match coincidencia in Expresion.Matches(texto)
            where EsValido(coincidencia.Value)
            select coincidencia.Value;
    }
}
