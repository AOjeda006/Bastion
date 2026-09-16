using System.Text.RegularExpressions;
using Bastion.Pruebas.Comun;
using Shouldly;

namespace Bastion.Arquitectura.Tests;

/// <summary>
/// La raíz del repositorio se busca en <c>tests/Comun/RaizDelRepositorio.cs</c> y en ningún otro
/// sitio.
/// </summary>
/// <remarks>
/// <para>
/// Hubo seis copias, y no por descuido de una tarde: cada regla que lee el árbol necesita la raíz,
/// escribirla son diez líneas, y copiar la del fichero de al lado es lo natural. Las copias
/// divergieron —dos sin el segundo intento, cinco mensajes distintos, una que devolvía
/// <c>null</c>— y la compartida, que existía desde el 1.9, no las recogió porque nada lo pedía.
/// </para>
/// <para>
/// <b>Qué se busca, y qué se escapa.</b> Las dos agujas sin las que la búsqueda no se puede
/// escribir: el nombre de la solución entre comillas y el paso hacia la carpeta madre. Una copia
/// que subiera buscando otra marca —<c>.git</c>, <c>global.json</c>— sin tocar <c>.Parent</c>
/// tendría que salir del árbol por otro camino, y ese no lo ve. Es el límite, y se dice.
/// </para>
/// </remarks>
public sealed partial class LaRaizDelRepositorioSeBuscaEnUnSitioTests
{
    private const string Compartida = "tests/Comun/RaizDelRepositorio.cs";

    private static readonly string[] s_carpetasDeSalida = ["bin", "obj"];

    [Fact]
    public void Las_dos_agujas_encuentran_la_busqueda_compartida()
    {
        // El detector, contra lo único que tiene que encontrar. Si la compartida se reescribe sin
        // una de las dos agujas, la regla de abajo ya no ve la copia que las use igual.
        List<(string Fichero, string Aguja)> hallazgos = Hallazgos();

        hallazgos.Select(hallazgo => hallazgo.Fichero).ShouldContain(Compartida);
        hallazgos.Where(hallazgo => hallazgo.Fichero == Compartida)
            .Select(hallazgo => hallazgo.Aguja)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ShouldBe(
                ["el nombre de la solución", "el paso a la carpeta madre"],
                ignoreOrder: false,
                customMessage: $"{Compartida} ya no lleva las dos agujas que esta regla busca: una copia escrita " +
                "como ella dejaría de verse");
    }

    [Fact]
    public void Nadie_mas_busca_la_raiz_del_repositorio()
    {
        List<string> copias =
        [
            .. from hallazgo in Hallazgos()
               where hallazgo.Fichero != Compartida
               select $"{hallazgo.Fichero} ({hallazgo.Aguja})",
        ];

        copias.ShouldBeEmpty(
            $"la raíz del repositorio se busca fuera de {Compartida}: " + string.Join("; ", copias) +
            ". Enlaza la compartida con <Compile Include> y llama a RaizDelRepositorio.Ruta()");
    }

    private static List<(string Fichero, string Aguja)> Hallazgos()
    {
        string raiz = RaizDelRepositorio.Ruta();

        return
        [
            .. from fichero in Directory.EnumerateFiles(Path.Combine(raiz, "tests"), "*.cs", SearchOption.AllDirectories)
               let relativo = Path.GetRelativePath(raiz, fichero).Replace('\\', '/')
               where !relativo.Split('/').Any(parte => s_carpetasDeSalida.Contains(parte, StringComparer.Ordinal))
               let codigo = File.ReadAllText(fichero)
               from aguja in new[]
               {
                   (Nombre: "el nombre de la solución", Encontrada: codigo.Contains("\"Bastion.sln\"", StringComparison.Ordinal)),
                   (Nombre: "el paso a la carpeta madre", Encontrada: PasoALaMadre().IsMatch(codigo)),
               }
               where aguja.Encontrada
               orderby relativo, aguja.Nombre
               select (relativo, aguja.Nombre),
        ];
    }

    [GeneratedRegex(@"\.Parent\s*;")]
    private static partial Regex PasoALaMadre();
}
