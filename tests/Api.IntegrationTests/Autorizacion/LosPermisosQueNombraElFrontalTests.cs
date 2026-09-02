using System.Globalization;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Autorizacion;

/// <summary>
/// Todo identificador de permiso que el frontal teclea existe en el catálogo que sirve la API.
/// </summary>
/// <remarks>
/// <para>
/// <b>Qué agujero tapa.</b> Los permisos del frontal son lo único del contrato que se escribe a
/// mano: el catálogo no está en el documento OpenAPI como enumerado, así que no hay de dónde
/// generarlos (ver <c>frontend/src/shared/sesion/permisos.ts</c>). Y una cadena tecleada que no se
/// puede derivar del proyecto es un selector que se rompe en silencio: <c>concede()</c> devuelve
/// <c>false</c>, la opción no se pinta, y la interfaz se queda coja <b>sin un solo error</b>. El
/// fallo cae hacia el lado seguro —el servidor autoriza igual—, pero es indistinguible de «este
/// usuario no tiene ese permiso», que es el estado normal de casi todos los usuarios.
/// </para>
/// <para>
/// <b>Por qué vive en el carril de integración y no en el funcional.</b> Lo que hay que comparar
/// no es una constante de C#: es <i>lo que la API sirve de verdad</i> por
/// <c>GET /api/v1/identidad/roles/permisos</c>, que exige <c>identidad.rol.ver</c>. Un token solo
/// existe si hay un inicio de sesión de verdad, y un inicio de sesión de verdad necesita usuario,
/// rol y pertenencia en una base de datos. El carril funcional tiene el host en pie pero no tiene
/// base, así que ahí solo se podría llegar al catálogo forjando un testigo — y forjar el testigo
/// es exactamente lo que este proyecto no hace nunca (ver <see cref="Sesiones"/>): probaría el
/// manejador de permisos, no el catálogo. Comparar contra las constantes de C# tampoco valdría:
/// diría que dos listas escritas a mano coinciden, no que la API sirva ninguna de las dos.
/// </para>
/// <para>
/// <b>Se barre el frontal entero, no un fichero.</b> La primera versión de esto habría mirado solo
/// <c>permisos.ts</c>, y ya hay una segunda lista tecleada en <c>src/pruebas/datos.ts</c>. Un
/// barrido que mira un fichero conocido deja de mirar en cuanto alguien escribe el literal en
/// otro sitio, que es justo lo que pasa.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LosPermisosQueNombraElFrontalTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string RutaDelCatalogo = "/api/v1/identidad/roles/permisos";

    /// <summary>El fichero que TIENE que aportar literales, pase lo que pase con el resto.</summary>
    /// <remarks>
    /// El ancla del barrido. Sin ella, un <c>glob</c> que dejara de encontrar ficheros —una carpeta
    /// que se mueve, una extensión nueva— daría cero literales y verde: la forma exacta de falso
    /// verde que este proyecto persigue.
    /// </remarks>
    private const string FicheroDeclarado = "frontend/src/shared/sesion/permisos.ts";

    /// <summary>
    /// Un literal entrecomillado que empieza por letra minúscula y lleva al menos un punto.
    /// </summary>
    private static readonly Regex s_literal = new(
        "['\"](?<valor>[a-z][a-z0-9]*(?:\\.[a-z0-9]+)+)['\"]",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private readonly ApiDeVerdad _api = new(postgres);

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Todo_permiso_que_el_frontal_teclea_lo_sirve_la_api()
    {
        using HttpClient cliente = await _api.ComoAdministradorAsync();

        IReadOnlyList<string> catalogo =
            await cliente.GetFromJsonAsync<IReadOnlyList<string>>(RutaDelCatalogo)
            ?? [];

        // Si el catálogo llega vacío, todo lo de abajo se cumple sin comprobar nada: el conjunto
        // de candidatos sale vacío porque no hay prefijos con los que reconocerlos.
        catalogo.ShouldNotBeEmpty(
            $"{RutaDelCatalogo} no ha servido ningún permiso: sin catálogo, este barrido no puede " +
            "decir nada de los literales del frontal");

        // Los prefijos salen del PROPIO catálogo, no de una lista tecleada aquí. Así, el módulo
        // que se monte en la fase 1 entra en el barrido el día que publique su primer permiso, sin
        // que nadie tenga que acordarse de añadirlo.
        HashSet<string> prefijos = [.. catalogo.Select(permiso => permiso.Split('.')[0])];

        IReadOnlyList<(string Fichero, string Valor)> candidatos = Candidatos(prefijos);

        candidatos.ShouldNotBeEmpty(
            "el barrido no ha encontrado ni un literal de permiso en el frontal. O han dejado de " +
            "usarse, o el barrido no está mirando donde debe");

        candidatos.Select(candidato => candidato.Fichero)
            .ShouldContain(
                FicheroDeclarado,
                $"{FicheroDeclarado} no ha aportado ningún literal. Es el fichero que los declara: " +
                "que no aparezca significa que el barrido se ha quedado sin ver el sitio principal");

        IReadOnlyList<string> inexistentes =
        [
            .. from candidato in candidatos
               where !catalogo.Contains(candidato.Valor, StringComparer.Ordinal)
               orderby candidato.Fichero + ":" + candidato.Valor, StringComparer.Ordinal
               select $"{candidato.Fichero} → «{candidato.Valor}»",
        ];

        inexistentes.ShouldBeEmpty(
            "estos identificadores de permiso los teclea el frontal y la API no los sirve. Un " +
            "permiso que no existe no da error: deniega en silencio y la opción no se pinta. " +
            "Catálogo servido (" + catalogo.Count.ToString(CultureInfo.InvariantCulture) + "): " +
            string.Join(", ", catalogo.Order(StringComparer.Ordinal)));
    }

    /// <summary>
    /// Los literales del frontal que tienen forma de permiso de un módulo que existe, con el
    /// fichero del que salen (en ruta relativa a la raíz del repositorio).
    /// </summary>
    private static IReadOnlyList<(string Fichero, string Valor)> Candidatos(HashSet<string> prefijos)
    {
        string raiz = Raiz();
        string fuentes = Path.Combine(raiz, "frontend", "src");

        Directory.Exists(fuentes).ShouldBeTrue($"no existe {fuentes}: el barrido no tiene dónde mirar");

        return
        [
            .. from ruta in Directory.EnumerateFiles(fuentes, "*.*", SearchOption.AllDirectories)
               where Path.GetExtension(ruta) is ".ts" or ".tsx"
               let relativa = Path.GetRelativePath(raiz, ruta).Replace('\\', '/')
               from coincidencia in s_literal.Matches(File.ReadAllText(ruta)).Cast<Match>()
               let valor = coincidencia.Groups["valor"].Value
               // El primer segmento tiene que ser un módulo que publica permisos. Sin este filtro
               // entrarían nombres de fichero y de paquete, que también son minúsculas con puntos.
               where prefijos.Contains(valor.Split('.')[0])
               orderby relativa + ":" + valor, StringComparer.Ordinal
               select (relativa, valor),
        ];
    }

    /// <summary>La raíz del repositorio, subiendo hasta encontrar la solución.</summary>
    private static string Raiz()
    {
        DirectoryInfo? directorio = new(AppContext.BaseDirectory);

        while (directorio is not null && !File.Exists(Path.Combine(directorio.FullName, "Bastion.sln")))
        {
            directorio = directorio.Parent;
        }

        directorio.ShouldNotBeNull(
            $"no se encuentra Bastion.sln subiendo desde {AppContext.BaseDirectory}");

        return directorio.FullName;
    }
}
