using System.Globalization;
using System.Reflection;
using NetArchTest.Rules;
using Shouldly;

namespace Bastion.Arquitectura.Tests;

/// <summary>
/// El único sitio por el que se ejecuta una regla de este carril, con las tres afirmaciones que
/// una regla de arquitectura necesita para significar algo.
/// </summary>
/// <remarks>
/// <para>
/// <b>La trampa de la herramienta.</b> En NetArchTest una regla se cumple cuando ningún tipo
/// seleccionado la incumple. Si el selector no casa con nada, no hay ningún tipo que incumpla y la
/// regla <i>se cumple</i>: verdad por vacuidad, verde para siempre, y un informe que cuenta esa
/// regla entre las comprobadas. Un espacio de nombres mal tecleado, un módulo que todavía no
/// existe o un ensamblado que dejó de copiarse a la salida bastan para llegar ahí, y ninguno de
/// los tres deja rastro en la salida del test.
/// </para>
/// <para>
/// De ahí las tres afirmaciones, y las tres son obligatorias:
/// </para>
/// <list type="number">
/// <item><b>El alcance está declarado y no está vacío.</b> Los ensamblados que la regla dice
/// cubrir se comparan enteros contra <see cref="Inventario.EnsambladosConTipos"/>, y esa lista se
/// compara a su vez contra la realidad en
/// <c>ElInventarioDeModulosTests.Cada_ensamblado_modular_lleva_los_tipos_que_el_inventario_declara</c>.
/// Así la cadena se cierra: ninguna regla puede aplicarse a un ensamblado vacío sin que alguien lo
/// haya escrito.</item>
/// <item><b>El conteo se compara.</b> Los tipos que la herramienta dice haber seleccionado tienen
/// que ser todos los que llevan esos ensamblados. Si el selector se estropea, el número baja y la
/// diferencia se imprime.</item>
/// <item><b>Y entonces, la regla.</b> Con los tipos que fallan nombrados uno a uno, porque un
/// «expected True to be False» no dice qué frontera se ha cruzado ni quién la cruzó.</item>
/// </list>
/// <para>
/// Falta una cuarta que no cabe aquí porque no es de la regla sino de la prohibición: que lo
/// prohibido EXISTA. Prohibirle al dominio un espacio de nombres mal escrito da verde igual que
/// prohibirle uno bien escrito, y en ese caso el selector está perfecto —son todos los tipos del
/// dominio— así que la afirmación 2 no lo nota. Eso lo comprueban los tests de contraejemplo, uno
/// por cada prohibición de este carril.
/// </para>
/// </remarks>
internal static class Barrido
{
    /// <summary>
    /// Ejecuta una regla sobre el alcance declarado y exige las tres afirmaciones.
    /// </summary>
    /// <param name="regla">Qué se está prohibiendo, en una frase.</param>
    /// <param name="claves">Los ensamblados que la regla cubre, como <c>Modulo.Capa</c>.</param>
    /// <param name="condicion">La regla, construida sobre los tipos del alcance.</param>
    internal static void Exige(
        string regla,
        IReadOnlyList<string> claves,
        Func<Types, ConditionList> condicion)
    {
        // 1. El alcance.
        claves.ShouldNotBeEmpty(
            $"la regla «{regla}» no cubre ningún ensamblado, así que no puede fallar nunca. Una " +
            "regla sobre el conjunto vacío es verdad por vacuidad: sale verde hoy y saldrá verde " +
            "el día que alguien cruce la frontera. Revisa el inventario y el descubrimiento");

        IReadOnlyList<Assembly> alcance =
            [.. claves.Select(clave => Ensamblados.Todos[clave])];

        TestResult resultado = condicion(Types.InAssemblies(alcance)).GetResult();

        IReadOnlyList<string> cargados =
        [
            .. resultado.LoadedAssemblies
                .Select(ensamblado => ensamblado.FullName.Split(',')[0])
                .Order(StringComparer.Ordinal),
        ];

        cargados.ShouldBe(
            [.. claves.Select(clave => Inventario.Raiz + "." + clave).Order(StringComparer.Ordinal)],
            customMessage: $"la regla «{regla}» no ha leído los ensamblados que dice cubrir");

        // 2. El conteo.
        int esperados = alcance.Sum(Ensamblados.Tipos);

        resultado.SelectedTypesForTesting.Count.ShouldBe(
            esperados,
            $"la regla «{regla}» dice cubrir {Numero(esperados)} tipos de " +
            $"{Numero(claves.Count)} ensamblados y ha seleccionado " +
            $"{Numero(resultado.SelectedTypesForTesting.Count)}: el selector se ha quedado corto, " +
            "y lo que no se selecciona no se comprueba");

        resultado.SelectedTypesForTesting.Count.ShouldBeGreaterThan(
            0,
            $"la regla «{regla}» no ha seleccionado ni un tipo");

        // 3. La regla.
        resultado.IsSuccessful.ShouldBeTrue(
            $"{regla} — y estos {Numero(resultado.FailingTypes.Count)} tipos la cruzan:" +
                Environment.NewLine +
                Ensamblados.Enumerar(resultado.FailingTypes.Select(Describir)));
    }

    /// <summary>
    /// Cuántos tipos del alcance dependen de <paramref name="prohibida"/>. Es el CONTRAEJEMPLO:
    /// se usa para exigir que lo prohibido exista de verdad en algún sitio donde está permitido.
    /// </summary>
    internal static int Dependen(IReadOnlyList<string> claves, string prohibida) =>
        claves.Count == 0
            ? 0
            : Types.InAssemblies([.. claves.Select(clave => Ensamblados.Todos[clave])])
                .That()
                .HaveDependencyOnAny(prohibida)
                .GetTypes()
                .Count();

    private static string Describir(IType tipo) =>
        string.IsNullOrWhiteSpace(tipo.Explanation)
            ? tipo.FullName
            : tipo.FullName + " — " + tipo.Explanation;

    private static string Numero(int cuantos) => cuantos.ToString(CultureInfo.InvariantCulture);
}
