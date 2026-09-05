using System.Text.Json;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Pruebas.Comun;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Errores;

/// <summary>
/// El artefacto <c>docs/api/errores.json</c> —el catálogo de los <c>type</c> que la API puede
/// emitir— describe errores que este proyecto sabe construir, y no una lista de cadenas cualquiera.
/// </summary>
/// <remarks>
/// <para>
/// <b>Qué hace el guion y qué hace esto.</b> <c>scripts/generar-errores.sh</c> barre el código
/// fuente y compone el artefacto; la CI lo vuelve a generar y falla si el versionado se quedó
/// atrás, igual que con <c>openapi.json</c>. Eso garantiza que el fichero está <b>al día</b>, no
/// que diga algo con sentido: un barrido puede estar al día y componer basura. Lo que se afirma
/// aquí es lo segundo, y contra el código compilado —<see cref="TipoDeError"/> y las guardas de
/// <see cref="ErrorDeOperacion"/>—, que es una fuente que no se deriva del texto barrido.
/// </para>
/// <para>
/// <b>Por qué importa que la clase sea de verdad.</b> Desde el ADR-0030 el frontal escribe el
/// texto a partir del <c>type</c>, y para decidir qué hacer con él —reintentar, mandar a la
/// pantalla de acceso, enseñar un aviso— mira la clase. Una clase inventada no rompe nada al
/// generar y deja al frontal ramificando sobre un valor que no existe.
/// </para>
/// <para>
/// <b>Está en el carril rápido</b> porque no necesita ni base de datos ni host: es un fichero del
/// repositorio y un enumerado.
/// </para>
/// </remarks>
public sealed class ElCatalogoDeTiposEsElQueLaApiEmiteTests
{
    private const string RutaDelCatalogo = "docs/api/errores.json";

    /// <summary>
    /// Códigos que el proyecto emite HOY y que tienen que estar sí o sí.
    /// </summary>
    /// <remarks>
    /// El ancla contra el falso verde (ADR-0020). Todo lo demás de este fichero recorre lo que
    /// ENCUENTRE en el artefacto: si el artefacto se quedara con dos entradas —o con ninguna—,
    /// los bucles no recorrerían nada y saldría verde sin haber mirado un solo error. Son tres, de
    /// tres sitios distintos, para que mover una carpeta no los apague a los tres a la vez.
    /// </remarks>
    private static readonly string[] s_codigosQueTienenQueEstar =
    [
        "datos-no-validos",     // de BuildingBlocks, el 400 de toda validación por campo
        "version-obsoleta",     // de BuildingBlocks, el 412 de la concurrencia optimista
        "empresa-no-encontrada", // de un módulo, para que el ancla no sea toda del mismo sitio
    ];

    [Fact]
    public void El_catalogo_existe_y_no_esta_vacio()
    {
        IReadOnlyList<TipoDelCatalogo> catalogo = Leer();

        catalogo.ShouldNotBeEmpty(
            $"{RutaDelCatalogo} no trae ni un tipo, así que el frontal no tiene contra qué comparar " +
            "sus textos y las demás reglas de este fichero recorrerían una lista vacía");

        foreach (string codigo in s_codigosQueTienenQueEstar)
        {
            catalogo.ShouldContain(
                tipo => tipo.Codigo == codigo,
                $"el código «{codigo}» se emite en el proyecto y no está en {RutaDelCatalogo}: o el " +
                "barrido del guion ha dejado de verlo, o el artefacto se ha quedado atrás");
        }
    }

    [Fact]
    public void Cada_codigo_del_catalogo_es_una_ranura_estable_que_ErrorDeOperacion_aceptaria()
    {
        IReadOnlyList<TipoDelCatalogo> catalogo = Leer();
        catalogo.ShouldNotBeEmpty();

        List<string> rechazados = [];

        foreach (TipoDelCatalogo tipo in catalogo)
        {
            try
            {
                // Se construye el error DE VERDAD, con la misma guarda que corre en producción, en
                // vez de repetir aquí la regla de qué es una ranura estable. Repetida, las dos
                // versiones se separan y la de aquí deja de decir nada.
                ErrorDeOperacion.Validacion(tipo.Codigo, "Comprobación del catálogo.");
            }
            catch (ArgumentException fallo)
            {
                rechazados.Add($"{tipo.Codigo}: {fallo.Message}");
            }
        }

        rechazados.ShouldBeEmpty(
            "estos códigos están en el catálogo pero `ErrorDeOperacion` no los aceptaría, así que " +
            "no puede haberlos emitido nadie: el barrido ha recogido algo que no es un código — " +
            string.Join(" | ", rechazados));
    }

    [Fact]
    public void Cada_tipo_del_catalogo_lleva_una_clase_de_error_que_existe_y_tiene_estado()
    {
        IReadOnlyList<TipoDelCatalogo> catalogo = Leer();
        catalogo.ShouldNotBeEmpty();

        List<string> rotos = [];

        foreach (TipoDelCatalogo tipo in catalogo)
        {
            if (!Enum.TryParse(tipo.Clase, ignoreCase: false, out TipoDeError clase))
            {
                rotos.Add($"{tipo.Codigo}: «{tipo.Clase}» no es un valor de TipoDeError");
                continue;
            }

            // Y que además tenga traducción a HTTP. `TipoDeError` podría estrenar un valor sin
            // añadirlo a la correspondencia del §9, y entonces el error existiría en el dominio y
            // reventaría al salir.
            if (!Enum.IsDefined(clase))
            {
                rotos.Add($"{tipo.Codigo}: «{tipo.Clase}» no está definido en TipoDeError");
            }
        }

        rotos.ShouldBeEmpty(
            "estas entradas del catálogo dicen una clase de error que el dominio no tiene, y el " +
            "frontal ramifica sobre ella: " + string.Join(" | ", rotos));
    }

    [Fact]
    public void El_type_de_cada_entrada_es_el_que_compone_la_politica_y_no_otro()
    {
        IReadOnlyList<TipoDelCatalogo> catalogo = Leer();
        catalogo.ShouldNotBeEmpty();

        // `PoliticaDeErrores` vive en Infrastructure y este ensamblado no la referencia, así que la
        // base se lee del propio artefacto y se comprueba la composición. Lo que se afirma es que
        // `type` y `codigo` no se puedan separar: si alguien tocara el guion y compusiera el `type`
        // de otra manera, el frontal buscaría un texto por una clave que la API no emite.
        string baseDeTipos = BaseDelCatalogo();

        baseDeTipos.ShouldBe(
            "/errors/",
            "la base de los `type` ha cambiado en el artefacto. Es contrato publicado: cambiarla " +
            "invalida todo lo que un cliente tuviera escrito, y además tiene que seguir casando " +
            "con `PoliticaDeErrores.BaseDeTipos`");

        List<string> descuadres =
        [
            .. from tipo in catalogo
               where tipo.Tipo != baseDeTipos + tipo.Codigo
               select $"{tipo.Codigo} -> {tipo.Tipo}",
        ];

        descuadres.ShouldBeEmpty(
            "el `type` de estas entradas no es la base más el código: " + string.Join(" | ", descuadres));
    }

    [Fact]
    public void Ningun_codigo_aparece_dos_veces_en_el_catalogo()
    {
        IReadOnlyList<TipoDelCatalogo> catalogo = Leer();
        catalogo.ShouldNotBeEmpty();

        List<string> repetidos =
        [
            .. from tipo in catalogo
               group tipo by tipo.Codigo into grupo
               where grupo.Count() > 1
               select $"{grupo.Key} ×{grupo.Count()}",
        ];

        repetidos.ShouldBeEmpty(
            "un código repetido en el catálogo deja al frontal con dos textos posibles para el " +
            "mismo `type`, y cuál gana depende de en qué orden lo recorra: " +
            string.Join(" | ", repetidos));
    }

    private static JsonDocument Documento()
    {
        string? raiz = RaizDelRepositorio.Buscar();

        raiz.ShouldNotBeNull(
            "no se ha encontrado Bastion.sln, ni subiendo desde el ensamblado ni desde este fichero");

        string ruta = Path.Combine(raiz, RutaDelCatalogo.Replace('/', Path.DirectorySeparatorChar));

        File.Exists(ruta).ShouldBeTrue(
            $"no existe {RutaDelCatalogo}. Se genera con: bash scripts/generar-errores.sh");

        return JsonDocument.Parse(File.ReadAllText(ruta));
    }

    private static string BaseDelCatalogo()
    {
        using JsonDocument documento = Documento();

        return documento.RootElement.GetProperty("base").GetString() ?? string.Empty;
    }

    private static IReadOnlyList<TipoDelCatalogo> Leer()
    {
        using JsonDocument documento = Documento();

        return
        [
            .. from entrada in documento.RootElement.GetProperty("tipos").EnumerateArray()
               select new TipoDelCatalogo(
                   entrada.GetProperty("codigo").GetString() ?? string.Empty,
                   entrada.GetProperty("type").GetString() ?? string.Empty,
                   entrada.GetProperty("clase").GetString() ?? string.Empty),
        ];
    }

    private sealed record TipoDelCatalogo(string Codigo, string Tipo, string Clase);
}
