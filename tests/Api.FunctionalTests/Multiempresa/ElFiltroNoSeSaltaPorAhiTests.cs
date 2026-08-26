using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Multiempresa;

/// <summary>
/// Los caminos que se saltan el filtro global sin que EF Core diga nada, y la prohibición —de
/// verdad comprobada— de cada uno.
/// </summary>
/// <remarks>
/// <para>
/// Un filtro de consulta protege lo que pasa por el <i>traductor de consultas</i> y nada más. Hay
/// una lista corta y conocida de puertas que lo rodean: pedirle a EF Core que lo ignore, escribir
/// SQL a mano, buscar por clave con <c>Find</c> —que puede contestar desde el rastreador de
/// cambios sin llegar a consultar— y las escrituras masivas <c>ExecuteUpdate</c> y
/// <c>ExecuteDelete</c>, que saltan el rastreador y la unidad de trabajo. Ninguna de ellas
/// <b>falla</b> cuando se cuela: devuelve más filas, o toca las de otro.
/// </para>
/// <para>
/// De ninguna hay test de comportamiento posible mientras no se usen —no se puede ejercitar un
/// camino que no existe—, así que lo que se comprueba es que <b>siguen sin usarse</b>. Es una
/// prohibición, no una preferencia: el día que alguien necesite una de verdad, este fichero le
/// obliga a decirlo aquí y a explicar por qué.
/// </para>
/// <para>
/// <b>Se leen los ficheros de <c>src/</c> con los comentarios quitados</b>: lo que se prohíbe es
/// llamar, no nombrar. Si no fuera así, documentar la regla en el sitio donde importa rompería el
/// test que la defiende.
/// </para>
/// </remarks>
public sealed class ElFiltroNoSeSaltaPorAhiTests
{
    // Las llamadas que rodean el filtro, y por qué cada una lo rodea.
    private static readonly Dictionary<string, string> s_prohibidas = new(StringComparer.Ordinal)
    {
        [".IgnoreQueryFilters("] =
            "apaga el filtro para esa consulta. No hace falta: lo que se necesitaba de verdad " +
            "-semilla, acceso, unicidad global, pertenencias- pasa por SinInquilino, que deja " +
            "rastro en el registro y tiene los motivos en una lista cerrada. Con una alternativa " +
            "auditada al lado, la prohibición puede ser absoluta",

        [".FromSql"] =
            "el SQL escrito a mano no pasa por el traductor, así que el filtro no se le aplica",

        [".ExecuteSql"] =
            "lo mismo, y además escribe",

        [".SqlQuery"] =
            "lo mismo: consulta cruda sobre el contexto",

        [".ExecuteUpdate"] =
            "se traduce a un UPDATE directo. Este SÍ respeta el filtro de la consulta, pero salta " +
            "el rastreador y la unidad de trabajo, así que ni la auditoría (0.7) ni la " +
            "concurrencia (0.9) lo verían pasar",

        [".ExecuteDelete"] =
            "lo mismo, borrando",

        [".Find("] =
            "busca por clave y puede contestar desde el rastreador de cambios SIN consultar. " +
            "Cuando contesta desde ahí no hay consulta que filtrar: devuelve la fila de otra " +
            "empresa si alguien la cargó antes en el mismo contexto",

        [".FindAsync("] =
            "igual que Find",

        ["Set<RolDeMembresia>"] =
            "RolDeMembresia no filtra -depende de la pertenencia, que sí- y solo es seguro " +
            "mientras no se consulte por su cuenta",

        ["Set<PermisoDeRol>"] =
            "PermisoDeRol es parte del rol; consultarlo suelto lo saca de su dueño",
    };

    // Los sitios donde una de esas llamadas SÍ está, con su motivo. La lista nació con una sola
    // línea, y la puso este test: el barrido la encontró y hubo que ir a mirarla.
    private static readonly Dictionary<string, string> s_saltosPermitidos = new(StringComparer.Ordinal)
    {
        ["src/Modules/Identidad/Bastion.Identidad.Infrastructure/Persistencia/Repositorios/RepositorioDeRoles.cs" +
         " usa Set<PermisoDeRol>"] =
            "arma los permisos que van al token a partir de los roles de la pertenencia activa. " +
            "Los identificadores de rol NO vienen de la petición: los pone ConstructorDeSesion a " +
            "partir de la membresía, que sí filtra. Y el rol es global por decisión (ADR-0011), " +
            "así que sus permisos no son de ninguna empresa en particular",
    };

    // Dónde se abre un ámbito sin inquilino, cuántas veces, y por qué ahí. Es la lista blanca del
    // único mecanismo que apaga el filtro a propósito.
    private static readonly Dictionary<string, int> s_ambitosPermitidos = new(StringComparer.Ordinal)
    {
        ["src/Api/Arranque/SemillaDeArranque.cs"] = 1,
        ["src/Modules/Identidad/Bastion.Identidad.Application/Sesiones/CambiarEmpresaActiva.cs"] = 1,
        ["src/Modules/Identidad/Bastion.Identidad.Application/Sesiones/IniciarSesion.cs"] = 1,
        ["src/Modules/Identidad/Bastion.Identidad.Application/Sesiones/RenovarSesion.cs"] = 1,
        ["src/Modules/Identidad/Bastion.Identidad.Application/Usuarios/CrearUsuario.cs"] = 1,
        ["src/Modules/Identidad/Bastion.Identidad.Application/Usuarios/Pertenencias.cs"] = 4,
        ["src/Modules/Organizacion/Bastion.Organizacion.Application/Empresas/CrearEmpresa.cs"] = 1,
    };

    // Los dos únicos sitios donde se define un filtro global. Repartirlos por otros ficheros no
    // rompería nada; solo haría imposible contestar «qué filtra y qué no» leyendo un sitio.
    private static readonly string[] s_dondeSeDefinenLosFiltros =
    [
        "src/Modules/Identidad/Bastion.Identidad.Infrastructure/Persistencia/IdentidadDbContext.cs",
        "src/Modules/Organizacion/Bastion.Organizacion.Infrastructure/Persistencia/OrganizacionDbContext.cs",
    ];

    private const string Bloque = @"/\*.*?\*/";

    private const string Linea = @"//.*?$";

    [Fact]
    public void Ninguna_llamada_de_las_que_rodean_el_filtro_aparece_en_el_codigo()
    {
        List<string> hallazgos = [];

        // Solo los ficheros que ven EF Core. En el resto, un `.Find(` es el de `List<T>` —el
        // dominio lo usa, y el dominio no puede tocar EF Core—, así que contarlo sería ruido con
        // forma de aviso de seguridad, que es la clase de aviso que se acaba ignorando.
        foreach ((string ruta, string codigo) in CodigoDeProduccion().Where(fichero => VeEfCore(fichero.Codigo)))
        {
            foreach (string prohibida in s_prohibidas.Keys)
            {
                if (Veces(codigo, prohibida) == 0 || s_saltosPermitidos.ContainsKey($"{ruta} usa {prohibida}"))
                {
                    continue;
                }

                hallazgos.Add($"{ruta} usa {prohibida}");
            }
        }

        hallazgos.ShouldBeEmpty(string.Join("; ", hallazgos));
    }

    [Fact]
    public void La_lista_de_saltos_permitidos_no_nombra_sitios_que_ya_no_existen()
    {
        HashSet<string> presentes = [];

        foreach ((string ruta, string codigo) in CodigoDeProduccion().Where(fichero => VeEfCore(fichero.Codigo)))
        {
            foreach (string prohibida in s_prohibidas.Keys.Where(aguja => Veces(codigo, aguja) > 0))
            {
                presentes.Add($"{ruta} usa {prohibida}");
            }
        }

        List<string> sobran = [.. s_saltosPermitidos.Keys.Where(sitio => !presentes.Contains(sitio))];

        // Un permiso que ya no hace falta es un permiso que sigue concedido, y el siguiente que
        // escriba ahí esa llamada no se encontrará ningún rojo.
        sobran.ShouldBeEmpty(
            "estos saltos están autorizados y ya no están en el código: " + string.Join(", ", sobran));
    }

    [Fact]
    public void El_ambito_sin_inquilino_solo_se_abre_donde_esta_declarado()
    {
        Dictionary<string, int> aperturas = [];

        foreach ((string ruta, string codigo) in CodigoDeProduccion())
        {
            int cuantas = Veces(codigo, ".SinInquilino(");

            if (cuantas > 0)
            {
                aperturas[ruta] = cuantas;
            }
        }

        // Se comparan las dos listas ENTERAS, y no solo los sitios de más: un ámbito que
        // desaparece de donde hacía falta deja ese camino lanzando en cuanto lo pise una petición
        // sin empresa, y eso es un 500 que aquí se ve antes.
        Enumerar(aperturas).ShouldBe(Enumerar(s_ambitosPermitidos));
    }

    [Fact]
    public void Los_filtros_globales_se_definen_solo_en_los_contextos_de_modulo()
    {
        List<string> fuera = [.. CodigoDeProduccion()
            .Where(fichero => Veces(fichero.Codigo, ".HasQueryFilter(") > 0)
            .Select(fichero => fichero.Ruta)
            .Where(ruta => !s_dondeSeDefinenLosFiltros.Contains(ruta, StringComparer.Ordinal))];

        fuera.ShouldBeEmpty(
            "estos ficheros definen filtros globales fuera de los contextos de módulo: " +
            string.Join(", ", fuera));
    }

    private static string Enumerar(Dictionary<string, int> cuenta) => string.Join(
        "\n",
        cuenta.OrderBy(par => par.Key, StringComparer.Ordinal)
            .Select(par => $"{par.Key} x {par.Value.ToString(CultureInfo.InvariantCulture)}"));

    // Un fichero que no nombra EF Core no puede llamar a nada de EF Core: ni compilaría. Es el
    // filtro más barato que distingue «esto rodea el filtro global» de «esto es un método de
    // `List<T>` que se llama igual».
    private static bool VeEfCore(string codigo) =>
        codigo.Contains("Microsoft.EntityFrameworkCore", StringComparison.Ordinal);

    private static int Veces(string codigo, string aguja)
    {
        int cuantas = 0;
        int desde = 0;

        while ((desde = codigo.IndexOf(aguja, desde, StringComparison.Ordinal)) >= 0)
        {
            cuantas++;
            desde += aguja.Length;
        }

        return cuantas;
    }

    private static IEnumerable<(string Ruta, string Codigo)> CodigoDeProduccion()
    {
        string raiz = Raiz();
        string separador = Path.DirectorySeparatorChar.ToString();

        foreach (string fichero in Directory.EnumerateFiles(
            Path.Combine(raiz, "src"), "*.cs", SearchOption.AllDirectories))
        {
            // La carpeta de trabajo del compilador guarda copias generadas de los propios
            // fuentes: contarlas duplicaría cada hallazgo y ataría el test a si alguien ha
            // compilado antes de ejecutarlo.
            if (fichero.Contains(separador + "obj" + separador, StringComparison.Ordinal))
            {
                continue;
            }

            yield return (
                Path.GetRelativePath(raiz, fichero).Replace(Path.DirectorySeparatorChar, '/'),
                SinComentarios(File.ReadAllText(fichero)));
        }
    }

    // Basta y sobra para lo que se busca. Se lleva por delante lo que vaya detrás de las dos
    // barras dentro de una cadena -una dirección web, por ejemplo-, y da igual: ninguna de las
    // agujas de este fichero puede aparecer ahí.
    private static string SinComentarios(string codigo) => Regex.Replace(
        Regex.Replace(codigo, Bloque, string.Empty, RegexOptions.Singleline),
        Linea,
        string.Empty,
        RegexOptions.Multiline);

    // El repositorio se encuentra subiendo desde ESTE fichero hasta la solución. Partir del
    // directorio de salida apuntaría a bin/Release/net10.0, que es igual de válido hoy y deja de
    // serlo en cuanto cambie la profundidad de la salida.
    private static string Raiz([CallerFilePath] string desde = "")
    {
        DirectoryInfo? carpeta = new FileInfo(desde).Directory;

        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "Bastion.sln")))
        {
            carpeta = carpeta.Parent;
        }

        carpeta.ShouldNotBeNull("no se ha encontrado Bastion.sln subiendo desde el fichero del test");

        return carpeta.FullName;
    }
}
