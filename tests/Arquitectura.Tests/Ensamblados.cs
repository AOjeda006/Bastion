using System.Globalization;
using System.Reflection;
using NetArchTest.Rules;
using Shouldly;

namespace Bastion.Arquitectura.Tests;

/// <summary>
/// El descubrimiento: qué módulos y qué capas hay DE VERDAD, leído de los ensamblados compilados
/// y de las carpetas de <c>src/Modules/</c>. Es la mitad descubierta de las comparaciones; la
/// declarada está en <see cref="Inventario"/>.
/// </summary>
/// <remarks>
/// <para>
/// Los ensamblados salen del directorio de salida de este proyecto de test, que los tiene todos
/// porque el <c>.csproj</c> referencia <c>Bastion.Api</c> —el raíz de composición, el único
/// proyecto que ve a todos los módulos—. Descubrir en vez de listar es lo que hace que el módulo
/// que se monte en la fase 1 entre solo en todas las reglas de este fichero.
/// </para>
/// <para>
/// Y descubrir <b>por fichero .dll</b> y no por los tipos que aparezcan: un proyecto vacío también
/// compila a un ensamblado, y ese ensamblado tiene que aparecer en la lista para que alguien
/// pueda decir que está vacío. Si el descubrimiento se hiciera por tipos, los cuatro ensamblados
/// vacíos de Auditoría no existirían para este carril y sus fronteras no se echarían de menos.
/// </para>
/// </remarks>
internal static class Ensamblados
{
    /// <summary>
    /// Los ensamblados de módulo encontrados en la salida, por <c>Modulo.Capa</c>.
    /// </summary>
    internal static IReadOnlyDictionary<string, Assembly> Modulares { get; } =
        Descubrir(modular: true);

    /// <summary>
    /// Los del bloque común, por <c>BuildingBlocks.Capa</c>. Van aparte de
    /// <see cref="Modulares"/> porque NO son un módulo y no entran en la comparación del mapa del
    /// §5; pero la regla 2 —el dominio no conoce la infraestructura— les vale igual, y de hecho
    /// les vale más: una fuga en el dominio común la heredan los dieciséis.
    /// </summary>
    internal static IReadOnlyDictionary<string, Assembly> Comunes { get; } =
        Descubrir(modular: false);

    /// <summary>Los unos y los otros, que es contra lo que resuelve <see cref="Barrido"/>.</summary>
    internal static IReadOnlyDictionary<string, Assembly> Todos { get; } =
        new SortedDictionary<string, Assembly>(
            Modulares.Concat(Comunes).ToDictionary(par => par.Key, par => par.Value),
            StringComparer.Ordinal);

    /// <summary>
    /// Los ensamblados de las capas indicadas que el inventario declara CON TIPOS. Es el alcance
    /// de cualquier regla: nunca se le pasa a NetArchTest un ensamblado que se sabe vacío, porque
    /// entonces la regla no distinguiría «cumple» de «no hay nada que comprobar».
    /// </summary>
    internal static IReadOnlyList<Assembly> ConTipos(params string[] capas) =>
    [
        .. from par in Modulares
           where capas.Contains(par.Key.Split('.')[1], StringComparer.Ordinal)
              && Inventario.EnsambladosConTipos.Contains(par.Key)
           orderby par.Key, StringComparer.Ordinal
           select par.Value,
    ];

    /// <summary>Las claves <c>Modulo.Capa</c> de <see cref="ConTipos"/>, para poder nombrarlas.</summary>
    internal static IReadOnlyList<string> ClavesConTipos(params string[] capas) =>
    [
        .. from clave in Modulares.Keys
           where capas.Contains(clave.Split('.')[1], StringComparer.Ordinal)
              && Inventario.EnsambladosConTipos.Contains(clave)
           orderby clave, StringComparer.Ordinal
           select clave,
    ];

    /// <summary>Cuántos tipos ve NetArchTest en un ensamblado. Cero significa vacío.</summary>
    internal static int Tipos(Assembly ensamblado) => Types.InAssembly(ensamblado).GetTypes().Count();

    /// <summary>
    /// La raíz del repositorio, subiendo desde el directorio del ensamblado hasta encontrar la
    /// solución. Si no aparece, REVIENTA: un barrido que no encuentra qué barrer no puede dar
    /// verde, que es el modo de fallo entero de este ítem.
    /// </summary>
    internal static string Raiz()
    {
        DirectoryInfo? donde = new(AppContext.BaseDirectory);

        while (donde is not null && !File.Exists(Path.Combine(donde.FullName, "Bastion.sln")))
        {
            donde = donde.Parent;
        }

        donde.ShouldNotBeNull(
            "no se ha encontrado Bastion.sln subiendo desde " + AppContext.BaseDirectory +
            ": sin la raíz del repositorio no se pueden leer las carpetas de src/Modules, y un " +
            "barrido que no encuentra qué barrer tiene que fallar, no pasar");

        return donde.FullName;
    }

    private static SortedDictionary<string, Assembly> Descubrir(bool modular)
    {
        SortedDictionary<string, Assembly> encontrados = new(StringComparer.Ordinal);

        foreach (string fichero in Directory.EnumerateFiles(
            AppContext.BaseDirectory, Inventario.Raiz + ".*.dll"))
        {
            string[] partes = Path.GetFileNameWithoutExtension(fichero).Split('.');

            // `Bastion.<Modulo>.<Capa>` y nada más. Descarta `Bastion.Api` (dos partes) y
            // `Bastion.Arquitectura.Tests` (la capa no es una de las cinco).
            if (partes.Length != 3
                || !string.Equals(partes[0], Inventario.Raiz, StringComparison.Ordinal)
                || string.Equals(partes[1], Inventario.BloqueComun, StringComparison.Ordinal) == modular
                || !Inventario.Capas.Contains(partes[2], StringComparer.Ordinal))
            {
                continue;
            }

            encontrados[partes[1] + "." + partes[2]] = Assembly.LoadFrom(fichero);
        }

        return encontrados;
    }

    /// <summary>Ordena y numera una lista para que un fallo se lea de un vistazo.</summary>
    internal static string Enumerar(IEnumerable<string> lineas) => string.Join(
        Environment.NewLine,
        lineas.Order(StringComparer.Ordinal)
            .Select((linea, indice) =>
                (indice + 1).ToString(CultureInfo.InvariantCulture) + ". " + linea));
}
