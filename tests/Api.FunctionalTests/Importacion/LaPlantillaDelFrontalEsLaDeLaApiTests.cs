using System.Text.RegularExpressions;
using Bastion.Pruebas.Comun;
using Bastion.Terceros.Contracts.Terceros;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Importacion;

/// <summary>
/// La cabecera que la pantalla de importación manda copiar es la que la API exige, columna a columna
/// y en su orden.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué hay una copia.</b> El contrato publica la ruta y el informe, pero no las columnas de la
/// plantilla como dato: son constantes de <see cref="ImportacionDeTerceros"/>. La pantalla tiene que
/// enseñarlas —el error <c>importacion-cabecera-no-valida</c> dice «cópiala tal cual»—, así que las
/// lleva escritas en <c>model/importacion.ts</c>. Una copia que se separa del servidor manda a copiar
/// una cabecera que el servidor rechaza, y ningún test del frontal lo puede notar: su servidor es
/// simulado.
/// </para>
/// <para>
/// <b>La lista se extrae del fuente, y la extracción no se salta lo que no entiende.</b> Dentro del
/// literal solo puede haber cadenas entre comillas simples, comas, espacios y comentarios de línea; si
/// alguien escribe una constante, una propagación o comillas dobles, la extracción lo dice en vez de
/// devolver una lista más corta que luego no cuadra por un motivo que no es el de verdad.
/// </para>
/// <para>
/// <b>Está en el carril rápido</b> porque no necesita ni base de datos ni host: un fichero del árbol y
/// una lista compilada.
/// </para>
/// </remarks>
public sealed class LaPlantillaDelFrontalEsLaDeLaApiTests
{
    private const string RutaDelModelo = "frontend/src/features/terceros/terceros/model/importacion.ts";

    private static readonly Regex s_declaracion = new(
        @"export\s+const\s+CABECERA_DE_LA_PLANTILLA\s*=\s*\[(?<cuerpo>[^\]]*)\]\s*as\s+const\s*;",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private static readonly Regex s_cadena = new(
        "'(?<valor>[^'\\\\\\r\\n]*)'",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private static readonly Regex s_relleno = new(
        @"^(?:\s|,|//[^\n]*)*$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    [Fact]
    public void La_extraccion_lee_la_lista_entera_y_no_se_salta_lo_que_no_entiende()
    {
        // El ancla: si la extracción devolviera siempre una lista vacía, la comparación de abajo diría
        // «no cuadra» y el motivo sería este fichero, no la plantilla.
        Extraer(
                """
                export const CABECERA_DE_LA_PLANTILLA = [
                  'uno',
                  // un comentario no es una columna
                  'dos', 'tres',
                ] as const;
                """)
            .ShouldBe(["uno", "dos", "tres"]);

        // Lo que no es una cadena literal no se ignora: se señala.
        Should.Throw<InvalidOperationException>(() => Extraer(
            """
            export const CABECERA_DE_LA_PLANTILLA = [
              'uno',
              OTRA_COLUMNA,
            ] as const;
            """));

        Should.Throw<InvalidOperationException>(() => Extraer(
            """
            export const CABECERA_DE_LA_PLANTILLA = [
              'uno',
              "dos",
            ] as const;
            """));

        // Y sobre el fichero de verdad encuentra algo, que es lo que hace que la otra regla mire.
        Extraer(LeerModelo()).ShouldNotBeEmpty($"no se ha extraído ninguna columna de {RutaDelModelo}");
    }

    [Fact]
    public void La_cabecera_que_ensena_el_frontal_es_la_que_exige_la_api()
    {
        IReadOnlyList<string> delFrontal = Extraer(LeerModelo());

        delFrontal.ShouldBe(
            ImportacionDeTerceros.Cabecera,
            $"{RutaDelModelo} enseña una cabecera distinta de ImportacionDeTerceros.Cabecera: quien la copie " +
            "de la pantalla recibirá importacion-cabecera-no-valida. Se corrige la copia del frontal, que " +
            "es la que sigue a la API");
    }

    private static IReadOnlyList<string> Extraer(string fuente)
    {
        MatchCollection declaraciones = s_declaracion.Matches(fuente);

        if (declaraciones.Count != 1)
        {
            throw new InvalidOperationException(
                $"se esperaba exactamente una declaración de CABECERA_DE_LA_PLANTILLA y hay {declaraciones.Count}");
        }

        string cuerpo = declaraciones[0].Groups["cuerpo"].Value;
        string sinCadenas = s_cadena.Replace(cuerpo, string.Empty);

        if (!s_relleno.IsMatch(sinCadenas))
        {
            throw new InvalidOperationException(
                "dentro de CABECERA_DE_LA_PLANTILLA hay algo que no es una cadena entre comillas simples: " +
                sinCadenas.Trim());
        }

        return [.. from cadena in s_cadena.Matches(cuerpo) select cadena.Groups["valor"].Value];
    }

    private static string LeerModelo()
    {
        string raiz = RaizDelRepositorio.Ruta();
        string ruta = Path.Combine(raiz, RutaDelModelo.Replace('/', Path.DirectorySeparatorChar));

        File.Exists(ruta).ShouldBeTrue($"no existe {RutaDelModelo}: la pantalla de importación no tiene de dónde enseñar la plantilla");

        return File.ReadAllText(ruta);
    }
}
