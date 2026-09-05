using System.Runtime.CompilerServices;

namespace Bastion.Pruebas.Comun;

/// <summary>
/// Dónde está la raíz del repositorio, para las reglas que comparan código contra ficheros del
/// árbol y no contra objetos en memoria.
/// </summary>
/// <remarks>
/// <para>
/// Se parte del directorio del ENSAMBLADO y no del fichero del test. Al revés se cae en la CI
/// estando verde en local: <c>Directory.Build.props</c> pone <c>ContinuousIntegrationBuild</c>
/// cuando corre en GitHub Actions, eso activa <c>DeterministicSourcePaths</c>, y con él las rutas
/// de los fuentes se reescriben a <c>/_/tests/…</c> para que dos máquinas produzcan el mismo
/// binario. Un <c>[CallerFilePath]</c> así no apunta a ningún sitio que exista.
/// </para>
/// <para>
/// El fichero de quien llama queda de segundo intento, por si algún día la salida se mueve fuera
/// del árbol. Y si no aparece por ninguno de los dos, esto <b>revienta</b>: un barrido que no
/// encuentra qué barrer no puede dar verde.
/// </para>
/// <para>
/// Vive en <c>tests/Comun</c> y se enlaza por <c>Compile Include</c>, como
/// <see cref="CensoDeReglas"/>. Las tres copias privadas que ya había —en
/// <c>ElFiltroNoSeSaltaPorAhiTests</c>, <c>LasSemillasLleganDondeSeCarganTests</c> y
/// <c>LosPermisosQueNombraElFrontalTests</c>— se quedan donde están: unificarlas es tocar tres
/// reglas verdes para no ganar nada hoy, y está anotado en el PLAN.
/// </para>
/// </remarks>
internal static class RaizDelRepositorio
{
    /// <summary>La carpeta que contiene <c>Bastion.sln</c>, o <c>null</c> si no aparece.</summary>
    internal static string? Buscar([CallerFilePath] string desde = "") =>
        Subiendo(AppContext.BaseDirectory) ?? Subiendo(Path.GetDirectoryName(desde));

    private static string? Subiendo(string? partida)
    {
        DirectoryInfo? carpeta = string.IsNullOrEmpty(partida) ? null : new DirectoryInfo(partida);

        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "Bastion.sln")))
        {
            carpeta = carpeta.Parent;
        }

        return carpeta?.FullName;
    }
}
