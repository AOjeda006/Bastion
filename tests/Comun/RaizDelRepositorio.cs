using System.Runtime.CompilerServices;

namespace Bastion.Pruebas.Comun;

/// <summary>
/// Dónde está la raíz del repositorio, para las reglas que comparan código contra ficheros del
/// árbol y no contra objetos en memoria.
/// </summary>
/// <remarks>
/// <para>
/// <b>Se sube hasta encontrar la solución</b> en vez de escribir una ruta relativa a mano: el
/// ensamblado no corre donde está el fuente, sino en <c>bin/&lt;configuración&gt;/&lt;marco&gt;/</c>,
/// y un <c>../../../..</c> deja de valer en cuanto cambia la configuración o el marco de destino.
/// </para>
/// <para>
/// <b>Se parte del directorio del ENSAMBLADO y no del fichero del test.</b> Al revés se cae en la
/// CI estando verde en local —la primera versión, en <c>ElFiltroNoSeSaltaPorAhiTests</c>, lo hizo—:
/// <c>Directory.Build.props</c> pone <c>ContinuousIntegrationBuild</c> cuando corre en GitHub
/// Actions, eso activa <c>DeterministicSourcePaths</c>, y con él las rutas de los fuentes se
/// reescriben a <c>/_/tests/…</c> para que dos máquinas produzcan el mismo binario. Un
/// <c>[CallerFilePath]</c> así no apunta a ningún sitio que exista.
/// </para>
/// <para>
/// El fichero de quien llama queda de segundo intento, por si algún día la salida se mueve fuera
/// del árbol. Y si no aparece por ninguno de los dos, esto <b>revienta</b>: un barrido que no
/// encuentra qué barrer no puede dar verde, y devolver <c>null</c> dejaba a cada llamador la
/// ocasión de olvidarse de comprobarlo.
/// </para>
/// <para>
/// <b>Es la única búsqueda del repositorio.</b> Hasta el ítem 1.14 hubo seis copias privadas —dos
/// en <c>Api.FunctionalTests</c>, que ya enlazaba esta, tres en <c>Api.IntegrationTests</c> y la de
/// <c>Arquitectura.Tests</c>—, cada una con su mensaje y dos sin el segundo intento. Se enlaza por
/// <c>Compile Include</c>, como <see cref="CensoDeReglas"/>, y
/// <c>LaRaizDelRepositorioSeBuscaEnUnSitioTests</c> pone en rojo la copia que vuelva.
/// </para>
/// </remarks>
internal static class RaizDelRepositorio
{
    /// <summary>La carpeta que contiene <c>Bastion.sln</c>. Si no aparece, lanza.</summary>
    /// <param name="desde">El fichero de quien llama, lo pone el compilador.</param>
    /// <exception cref="DirectoryNotFoundException">Si no aparece por ninguno de los dos caminos.</exception>
    internal static string Ruta([CallerFilePath] string desde = "") =>
        Subiendo(AppContext.BaseDirectory)
        ?? Subiendo(Path.GetDirectoryName(desde))
        ?? throw new DirectoryNotFoundException(
            "no se ha encontrado Bastion.sln, ni subiendo desde el ensamblado (" + AppContext.BaseDirectory +
            ") ni desde el fichero que lo pide (" + desde + "): sin la raíz del repositorio un " +
            "barrido no tiene qué barrer, y tiene que fallar, no pasar");

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
