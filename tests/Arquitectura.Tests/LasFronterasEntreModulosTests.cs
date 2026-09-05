using System.Globalization;
using System.Text.RegularExpressions;
using NetArchTest.Rules;
using Shouldly;

namespace Bastion.Arquitectura.Tests;

/// <summary>
/// §4, reglas 1 y 5: un módulo solo puede referenciar el <c>Contracts</c> de otro, nunca su
/// <c>Domain</c>, su <c>Application</c> ni su <c>Infrastructure</c>; y entre módulos no se
/// escribe llamando, se escribe por eventos.
/// </summary>
/// <remarks>
/// <para>
/// Las prohibiciones NO están tecleadas: se construyen con los módulos que descubre
/// <see cref="Ensamblados"/> y las capas que declara <see cref="Inventario"/>. Es a propósito. Una
/// lista escrita a mano tendría que crecer trece veces según se monten los módulos que faltan, y
/// la que no creciera dejaría el hueco justo donde nadie mira. Y de paso quita de en medio una
/// forma de estropear la regla sin que se note: un espacio de nombres mal tecleado aquí no
/// compilaría, porque el nombre sale del ensamblado descubierto.
/// </para>
/// <para>
/// Solo se prohíben interiores que EXISTEN —los que el inventario declara con tipos—. Prohibir
/// <c>Bastion.Auditoria.Domain</c>, que hoy es un ensamblado sin un solo tipo, sería una
/// prohibición que no puede dispararse: nadie puede depender de lo que no hay. Sale verde y no
/// protege nada, que es exactamente lo que este ítem viene a impedir.
/// </para>
/// <para>
/// Las tres primeras reglas leen <b>IL</b>, y por eso hay una cuarta que no:
/// <c>Las_referencias_de_proyecto_son_las_declaradas</c> lee los <c>.csproj</c>. Una referencia de
/// proyecto que todavía no usa nadie no emite IL, así que el cruce prohibido se puede
/// <b>autorizar</b> hoy y <b>ejercer</b> dentro de tres commits, y las otras tres reglas no dirían
/// nada hasta el tercero. Lo comprobó la batería de mutaciones de este ítem. Uso y permiso son dos
/// hechos distintos y se vigilan por separado.
/// </para>
/// </remarks>
public sealed class LasFronterasEntreModulosTests
{
    /// <summary>Las capas que son el INTERIOR de un módulo: todo menos <c>Contracts</c>.</summary>
    private static readonly string[] s_interiores = ["Domain", "Application", "Infrastructure", "Endpoints"];

    [Fact]
    public void Ningun_modulo_ve_el_interior_de_otro()
    {
        IReadOnlyList<string> montados = Montados();

        // El conjunto sobre el que se itera también se compara: si mañana solo se descubrieran dos
        // módulos, este test seguiría verde habiendo comprobado un tercio menos de fronteras.
        montados.ShouldBe(
            [.. Inventario.Modulos.Where(par => par.Value == Presencia.Montado)
                .Select(par => par.Key).Order(StringComparer.Ordinal)],
            customMessage: "los módulos a los que se les aplica la frontera no son los que el inventario declara " +
            "montados");

        foreach (string modulo in montados)
        {
            IReadOnlyList<string> ajenos = InterioresDeLosDemas(modulo);

            ajenos.ShouldNotBeEmpty(
                $"a {modulo} no se le prohíbe ver el interior de ningún otro módulo, así que su " +
                "frontera no puede fallar. Con un solo módulo montado esto sería normal; con " +
                "tres, es que el descubrimiento se ha roto");

            Barrido.Exige(
                $"{modulo} solo puede ver el Contracts de otro módulo, nunca su interior",
                Capas(modulo),
                tipos => tipos.Should().NotHaveDependencyOnAny([.. ajenos]));
        }
    }

    [Fact]
    public void El_unico_cruce_entre_modulos_va_por_contratos()
    {
        List<string> encontrados = [];

        foreach (string clave in Inventario.EnsambladosConTipos)
        {
            string modulo = clave.Split('.')[0];

            foreach (string ajeno in Inventario.EnsambladosConTipos
                .Where(otra => !string.Equals(otra.Split('.')[0], modulo, StringComparison.Ordinal))
                .Select(otra => Inventario.Raiz + "." + otra)
                .Order(StringComparer.Ordinal))
            {
                if (Barrido.Dependen([clave], ajeno) > 0)
                {
                    encontrados.Add($"{clave} -> {ajeno}");
                }
            }
        }

        // La cara POSITIVA de la regla 1, y hace falta. `Ningun_modulo_ve_el_interior_de_otro`
        // seguiría verde en un proyecto donde los módulos no se hablaran en absoluto — que es el
        // estado en el que estaba esto hasta el 0.5 y en el que la regla no demostraba nada. Que
        // haya un cruce, y que vaya por un `Contracts`, es lo que prueba que el mecanismo existe y
        // se usa; que sea exactamente este, lo que impide que aparezca otro sin decirlo.
        IReadOnlyList<string> ordenados = [.. encontrados.Order(StringComparer.Ordinal)];

        ordenados.ShouldBe(
            [.. Inventario.CrucesDeclarados.Keys],
            customMessage: "los cruces entre módulos no son los declarados:" + Environment.NewLine +
            Ensamblados.Enumerar(encontrados));
    }

    [Fact]
    public void Las_puertas_publicas_de_los_contratos_son_las_declaradas()
    {
        IReadOnlyList<string> claves = Ensamblados.ClavesConTipos("Contracts");

        claves.ShouldNotBeEmpty("no hay ningún Contracts con tipos que barrer");

        IReadOnlyList<string> encontradas =
        [
            .. Types.InAssemblies([.. claves.Select(clave => Ensamblados.Todos[clave])])
                .That()
                .ArePublic()
                .And()
                .AreInterfaces()
                .GetTypes()
                .Select(tipo => tipo.FullName)
                .Order(StringComparer.Ordinal),
        ];

        // Lo que este carril puede decir de la regla 5. Que nadie LLAME a un caso de uso ajeno ya
        // lo impide la regla 1 —no alcanza su `Application`—; lo que la regla 1 no impide es que
        // un módulo publique en su propio `Contracts` un puerto que escriba, y ese sí es un hecho
        // de tipos. Aquí están todos los que hay, y para añadir uno hay que decir si lee o escribe.
        encontradas.ShouldBe(
            [.. Inventario.PuertasPublicas.Keys],
            customMessage: "las interfaces públicas de los Contracts no son las declaradas:" + Environment.NewLine +
            Ensamblados.Enumerar(encontradas));
    }

    [Fact]
    public void Las_referencias_de_proyecto_son_las_declaradas()
    {
        string modulos = Path.Combine(Ensamblados.Raiz(), "src", "Modules");

        IReadOnlyList<string> proyectos =
        [
            .. Directory.EnumerateFiles(modulos, "*.csproj", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal),
        ];

        // La afirmación de conjunto no vacío de esta regla, con su número: los cuatro módulos
        // montados llevan cinco proyectos cada uno. Los doce que faltan no tienen ni `.csproj`,
        // así que un barrido que encontrara menos de veinte estaría mirando menos módulos de los
        // que hay y —sin esta línea— no lo diría.
        proyectos.Count.ShouldBe(
            20,
            "el barrido de referencias tiene que encontrar los cinco proyectos de cada uno de los " +
            "cuatro módulos montados, y ha encontrado " +
            proyectos.Count.ToString(CultureInfo.InvariantCulture));

        IReadOnlyList<string> encontradas =
        [
            .. from proyecto in proyectos
               let origen = Corto(Path.GetFileNameWithoutExtension(proyecto))
               from referencia in Regex.Matches(
                   File.ReadAllText(proyecto),
                   "<ProjectReference\\s+Include=\"(?<destino>[^\"]+)\"")
               let destino = Corto(Path.GetFileNameWithoutExtension(
                   referencia.Groups["destino"].Value.Replace('\\', '/')))
               orderby origen + " -> " + destino, StringComparer.Ordinal
               select origen + " -> " + destino,
        ];

        // El PERMISO para cruzar, que va un paso por delante del cruce. Las demás reglas leen IL, y
        // una referencia que todavía no usa nadie no emite IL: se comprobó en la batería de este
        // ítem y las catorce salían verdes. Aquí no, porque aquí se lee el `.csproj`.
        encontradas.ShouldBe(
            [.. Inventario.AristasDeProyecto],
            customMessage: "las referencias de proyecto no son las declaradas:" + Environment.NewLine +
            Ensamblados.Enumerar(encontradas));
    }

    /// <summary>
    /// El nombre del proyecto sin la raíz: <c>Bastion.Identidad.Domain</c> → <c>Identidad.Domain</c>.
    /// </summary>
    private static string Corto(string proyecto) =>
        proyecto.StartsWith(Inventario.Raiz + ".", StringComparison.Ordinal)
            ? proyecto[(Inventario.Raiz.Length + 1)..]
            : proyecto;

    private static IReadOnlyList<string> Montados() =>
    [
        .. Ensamblados.Modulares.Keys
            .Select(clave => clave.Split('.')[0])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>Los ensamblados con tipos de un módulo, como <c>Modulo.Capa</c>.</summary>
    private static IReadOnlyList<string> Capas(string modulo) =>
    [
        .. Inventario.EnsambladosConTipos
            .Where(clave => string.Equals(clave.Split('.')[0], modulo, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>
    /// Los espacios de nombres del interior de los DEMÁS módulos, y solo los que llevan tipos.
    /// </summary>
    private static IReadOnlyList<string> InterioresDeLosDemas(string modulo) =>
    [
        .. from clave in Inventario.EnsambladosConTipos
           let partes = clave.Split('.')
           where !string.Equals(partes[0], modulo, StringComparison.Ordinal)
              && s_interiores.Contains(partes[1], StringComparer.Ordinal)
           orderby clave, StringComparer.Ordinal
           select Inventario.Raiz + "." + clave,
    ];
}
