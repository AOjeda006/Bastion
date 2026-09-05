using NetArchTest.Rules;
using Shouldly;

namespace Bastion.Arquitectura.Tests;

/// <summary>
/// §4, regla 2 y el reparto por capas: ningún <c>Domain</c> conoce EF Core, ASP.NET Core ni nada
/// de infraestructura, y dentro de cada módulo las dependencias van siempre hacia dentro
/// (<c>Endpoints → Application → Domain</c>, <c>Infrastructure → Application/Domain</c>).
/// </summary>
/// <remarks>
/// <para>
/// Aquí sí hay cadenas escritas a mano —<c>Microsoft.EntityFrameworkCore</c> y las otras dos— y no
/// se pueden derivar de nada: son de fuera del proyecto. Esa es exactamente la forma de romper una
/// regla sin que nadie se entere: <c>Microsoft.EntityFramworkCore</c>, con una letra de menos,
/// compila, pasa el linter, no casa con ningún tipo y deja la regla verde para siempre con el
/// dominio hecho un almacén de <c>DbContext</c>.
/// </para>
/// <para>
/// Por eso cada prohibición viene con su <b>contraejemplo</b>: la capa donde ese mismo espacio de
/// nombres SÍ tiene que aparecer. Si la cadena está bien escrita, allí se encuentra; si está mal,
/// no se encuentra en ninguna parte y <c>La_prohibicion_al_dominio_puede_dispararse</c> lo dice
/// nombrando la cadena. Una prohibición que no puede dispararse no es una regla: es una frase.
/// </para>
/// <para>
/// El contraejemplo ya ha hecho su trabajo una vez, antes de que este fichero existiera: el primer
/// borrador prohibía también <c>System.Data</c> —ADO.NET, la otra puerta al mismo sitio— y el
/// contraejemplo salió a cero en las tres capas. Nadie en todo el proyecto depende de
/// <c>System.Data</c>, porque el acceso a datos entra por EF Core y por Npgsql. Prohibirlo habría
/// sumado una regla verde que no protege nada, y por eso no está.
/// </para>
/// </remarks>
public sealed class LasCapasVanHaciaDentroTests
{
    /// <summary>
    /// Qué no puede ver cada capa DE SU PROPIO MÓDULO, y el motivo. Lo de fuera del módulo es de
    /// <see cref="LasFronterasEntreModulosTests"/>: aquí solo se mira hacia dentro de casa.
    /// </summary>
    private static readonly SortedDictionary<string, string[]> s_haciaFuera =
        new SortedDictionary<string, string[]>(StringComparer.Ordinal)
        {
            // El dominio es el centro: no mira a nadie (`principios/clean-architecture.md`).
            ["Domain"] = ["Application", "Infrastructure", "Endpoints"],

            // `Contracts` es lo ÚNICO público del módulo, y esto no es una regla de capas sino la
            // regla 1 vista desde dentro: si el contrato arrastrase el `Domain`, cualquier módulo
            // que lo referenciara —que es lo que la regla 1 le PERMITE hacer— acabaría viendo el
            // dominio ajeno por transitividad, sin escribir ni una línea prohibida.
            ["Contracts"] = ["Domain", "Application", "Infrastructure", "Endpoints"],

            // Los casos de uso hablan con puertos, no con adaptadores.
            ["Application"] = ["Infrastructure", "Endpoints"],

            // La persistencia no sabe quién la llama por HTTP.
            ["Infrastructure"] = ["Endpoints"],
        };

    /// <summary>
    /// Los pares <c>Modulo.Capa</c> a los que la regla de arriba llega a aplicarse hoy, con la
    /// prohibición no vacía. Se declara y se compara porque el que no está no se comprueba.
    /// </summary>
    /// <remarks>
    /// Faltan los cinco de Auditoría, y no por descuido: cuatro de sus capas están vacías y la
    /// quinta —<c>Infrastructure</c>— solo tendría prohibido su propio <c>Endpoints</c>, que
    /// también está vacío. O sea que <b>a Auditoría no se le comprueba hoy ni una sola regla de
    /// capas</b>, y hace falta decirlo en voz alta: en una lista de reglas verdes, Auditoría
    /// parecería tan comprobada como los otros dos.
    /// </remarks>
    private static readonly string[] s_paresComprobados =
    [
        "Identidad.Application",
        "Identidad.Contracts",
        "Identidad.Domain",
        "Identidad.Infrastructure",
        "Organizacion.Application",
        "Organizacion.Contracts",
        "Organizacion.Domain",
        "Organizacion.Infrastructure",
        "Terceros.Application",
        "Terceros.Contracts",
        "Terceros.Domain",
        "Terceros.Infrastructure",
    ];

    [Fact]
    public void El_dominio_no_conoce_la_infraestructura_ni_el_framework()
    {
        IReadOnlyList<string> dominios =
        [
            .. Ensamblados.ClavesConTipos("Domain").Concat(["BuildingBlocks.Domain"])
                .Order(StringComparer.Ordinal),
        ];

        Barrido.Exige(
            "el dominio no sabe que existe una base de datos ni un servidor web (§4, regla 2)",
            dominios,
            tipos => tipos.Should().NotHaveDependencyOnAny([.. Inventario.ProhibidasAlDominio.Keys]));
    }

    [Fact]
    public void La_prohibicion_al_dominio_puede_dispararse()
    {
        List<string> mudas = [];

        foreach ((string prohibida, string capa) in Inventario.ProhibidasAlDominio)
        {
            IReadOnlyList<string> donde =
            [
                .. Ensamblados.ClavesConTipos(capa)
                    .Concat(Inventario.ComunesConTipos.Where(
                        clave => clave.EndsWith("." + capa, StringComparison.Ordinal)))
                    // Sin el ensamblado que ES lo prohibido. Sus tipos se dependen unos a otros,
                    // así que se contaría a sí mismo y el contraejemplo saldría bien aunque nadie
                    // más lo usara — un contraejemplo que se demuestra solo no demuestra nada.
                    .Where(clave => !string.Equals(
                        Inventario.Raiz + "." + clave, prohibida, StringComparison.Ordinal))
                    .Order(StringComparer.Ordinal),
            ];

            int cuantos = Barrido.Dependen(donde, prohibida);

            if (cuantos == 0)
            {
                mudas.Add(
                    $"«{prohibida}»: ni un tipo de {capa} depende de eso, así que prohibírselo al " +
                    "dominio no puede fallar nunca. O la cadena está mal escrita, o eso ya no se " +
                    "usa en el proyecto y la prohibición sobra");
            }
        }

        // La afirmación que le falta a una regla de arquitectura corriente, y la que caza la
        // mutación de este ítem: romper el selector sin tocar el código. Una letra de menos en
        // `Microsoft.EntityFrameworkCore` deja la prohibición verde, el conjunto seleccionado
        // intacto —son todos los tipos del dominio, no cambia nada— y la frontera abierta. Lo
        // único que se mueve es esto: la cadena deja de encontrarse donde tiene que estar.
        mudas.ShouldBeEmpty(
            "estas prohibiciones no pueden dispararse:" + Environment.NewLine +
            Ensamblados.Enumerar(mudas));
    }

    [Fact]
    public void Ninguna_capa_mira_hacia_fuera_de_su_modulo()
    {
        List<string> comprobados = [];

        foreach (string clave in Inventario.EnsambladosConTipos.Order(StringComparer.Ordinal))
        {
            string[] partes = clave.Split('.');

            if (!s_haciaFuera.TryGetValue(partes[1], out string[]? fuera))
            {
                // `Endpoints` es la capa de más afuera: no tiene ninguna otra por encima a la que
                // no pueda mirar. No es una regla que falte, es que ahí no hay nada que prohibir.
                continue;
            }

            IReadOnlyList<string> prohibidos =
            [
                .. from capa in fuera
                   let ajena = partes[0] + "." + capa
                   where Inventario.EnsambladosConTipos.Contains(ajena)
                   orderby ajena, StringComparer.Ordinal
                   select Inventario.Raiz + "." + ajena,
            ];

            if (prohibidos.Count == 0)
            {
                // La capa existe pero todo lo que tendría prohibido está vacío. La regla saldría
                // verde sin mirar nada, así que NO se ejecuta: se queda fuera de la lista de
                // comprobados, y la comparación de abajo obliga a que eso esté escrito.
                continue;
            }

            comprobados.Add(clave);

            Barrido.Exige(
                $"{clave} solo mira hacia dentro de su módulo (§4, capas)",
                [clave],
                tipos => tipos.Should().NotHaveDependencyOnAny([.. prohibidos]));
        }

        // Y la lista de lo que se ha llegado a comprobar, entera. Sin esto, el día que una capa se
        // quedara fuera —porque su vecina se vació, o porque el descubrimiento falló— el test
        // seguiría verde habiendo comprobado menos, y nadie lo sabría.
        comprobados.ShouldBe([.. s_paresComprobados]);
    }
}
