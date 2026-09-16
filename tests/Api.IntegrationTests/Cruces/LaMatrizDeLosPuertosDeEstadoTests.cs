using System.Globalization;
using System.Reflection;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Pruebas.Comun;
using Bastion.Terceros.Contracts.Terceros;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Cruces;

/// <summary>
/// Ninguna casilla de puerto × estado se queda sin un caso que la afirme contra la base, sea cual
/// sea el <c>Contracts</c> que publique el puerto y el enumerado que conteste.
/// </summary>
/// <remarks>
/// <para>
/// <b>La regla existía desde el ítem 1.7 y no veía los puertos nuevos.</b>
/// <c>LaMatrizDePuertoYEstadoTests</c>, en <c>Organizacion.IntegrationTests</c>, descubre puertos
/// en un solo ensamblado y por un solo tipo de retorno: <c>Task&lt;EstadoDeMaestro&gt;</c>. Los dos
/// puertos del cruce mutuo devuelven <c>EstadoDelTercero</c> y <c>EstadoDeLaTarifa</c>, desde
/// <c>Terceros.Contracts</c> y <c>Catalogo.Contracts</c>, así que entraron con la matriz en verde y
/// sus siete casillas sin mirar —seis, desde que una resultó no existir—. Es la infradetección del
/// ADR-0024 otra vez: una regla que descubre su universo por una forma concreta se queda ciega en
/// cuanto la forma cambia un poco.
/// </para>
/// <para>
/// <b>Aquí la forma es la general</b>: toda interfaz de un <c>Bastion.*.Contracts</c> con un método
/// que devuelva <c>Task</c> de un enumerado. Y la familia de <c>EstadoDeMaestro</c> no se duplica:
/// se <b>delega</b> en la matriz que ya la cubre, con el fichero nombrado y comprobado en disco, de
/// modo que cada familia tiene exactamente una matriz y ninguna se queda sin ella.
/// </para>
/// <para>
/// <b>Y fue esta regla la que encontró la primera avería del ítem, antes de escribir un solo
/// caso.</b> Con el enumerado de terceros tal como se diseñó —cuatro valores, uno de ellos
/// <c>Bloqueado</c>— la casilla <c>IConsultaDeTerceros -&gt; Bloqueado</c> no tenía dueño, y al
/// escribirle uno contra PostgreSQL el puerto contestó <c>NoExiste</c>: el filtro de repositorio
/// del art. 32 esconde la ficha antes de que el <c>switch</c> llegue a verla. Era un valor que
/// ningún productor produce, el mismo defecto que abrió el 1.7. La decisión que lo cierra está en
/// <c>docs/PLAN.md</c>, ítem 1.10.
/// </para>
/// <para>
/// <b>Sin contenedor, y por eso sin el rasgo <c>Integracion</c></b>, como su gemela: comparar dos
/// conjuntos no necesita PostgreSQL. Lo que lo necesita es la aserción de dentro de cada casilla, y
/// la cuarta regla de aquí abajo exige justo eso de cada marca.
/// </para>
/// </remarks>
public sealed class LaMatrizDeLosPuertosDeEstadoTests
{
    /// <summary>
    /// Las familias que ya tienen matriz en otro carril, con el fichero que la sostiene.
    /// </summary>
    /// <remarks>
    /// Una sola entrada y ninguna previsión de más: la de <c>EstadoDeMaestro</c> existía antes que
    /// esta regla y sus marcas viven en <c>Organizacion.IntegrationTests</c>, que este ensamblado no
    /// referencia. Una familia nueva no se añade aquí: se cubre aquí.
    /// </remarks>
    private static readonly Dictionary<Type, string> s_matricesEnOtroCarril = new()
    {
        [typeof(EstadoDeMaestro)] =
            "tests/Organizacion.IntegrationTests/Persistencia/LaMatrizDePuertoYEstadoTests.cs",
    };

    /// <summary>Que haya matriz que mirar, y que vea los dos sentidos del cruce mutuo.</summary>
    [Fact]
    public void La_matriz_no_esta_vacia_y_ve_los_dos_sentidos_del_cruce()
    {
        IReadOnlyList<Type> puertos = Puertos();

        // Los dos anclajes no son la lista de puertos —esa se descubre—: son la prueba de que el
        // descubrimiento está mirando más de un `Contracts`. Si solo encontrara los de Organización,
        // la comparación de abajo saldría verde por no tener nada propio que comparar.
        puertos.ShouldContain(
            typeof(IConsultaDeTerceros),
            "el descubrimiento no ve el puerto de Terceros. O se ha dejado de cargar " +
            "`Bastion.Terceros.Contracts`, o la forma del puerto ha cambiado y la regla ya no la " +
            "reconoce — y en los dos casos las casillas de ese puerto no las mira nadie");

        puertos.ShouldContain(typeof(IConsultaDeTarifas), "ídem con el puerto de Catálogo");

        Cubrimientos().ShouldNotBeEmpty(
            "no hay un solo caso marcado con `CubreEstadoDelPuerto`, así que todas las casillas " +
            "saldrían sin dueño o —si alguien vaciara también la lista de puertos— la matriz " +
            "compararía la nada contra la nada");
    }

    /// <summary>Cada puerto contesta cada valor de su enumerado, y hay un caso que lo dice.</summary>
    [Fact]
    public void Cada_casilla_de_puerto_por_estado_esta_cubierta()
    {
        IReadOnlyList<string> sinDuenio =
        [
            .. from puerto in Puertos()
               let familia = FamiliaDe(puerto)
               where !s_matricesEnOtroCarril.ContainsKey(familia)
               from estado in Enum.GetValues(familia).Cast<object>()
               where !Cubrimientos().Any(cubre =>
                   cubre.Puerto == puerto && Equals(cubre.Estado, estado))
               select $"{puerto.Name} -> {estado}",
        ];

        sinDuenio.ShouldBeEmpty(
            "hay casillas de la matriz puerto × estado que ningún caso afirma contra la base. O " +
            "el puerto no puede contestar ese valor —y entonces el enumerado tiene un valor que " +
            "ningún productor produce, que es el defecto del ítem 1.7 y el que este mismo ítem " +
            "encontró en `EstadoDelTercero.Bloqueado`— o puede y nadie lo comprueba. Se cierra " +
            "marcando con `CubreEstadoDelPuerto` el caso que lo afirma; si al escribirlo el " +
            "puerto contesta otra cosa, lo que sobra es el valor");
    }

    /// <summary>Ninguna marca nombra un puerto que no lo sea, ni un valor de otro enumerado.</summary>
    [Fact]
    public void Cada_cubrimiento_nombra_un_puerto_de_aqui_y_un_valor_de_SU_enumerado()
    {
        IReadOnlyList<Type> puertos = Puertos();

        IReadOnlyList<string> ajenos =
        [
            .. from cubre in Cubrimientos()
               let motivo = !puertos.Contains(cubre.Puerto)
                   ? "no es un puerto de estado de ningún Contracts"
                   : s_matricesEnOtroCarril.ContainsKey(FamiliaDe(cubre.Puerto))
                       ? "su familia se cubre en otro carril"
                       : cubre.Estado.GetType() != FamiliaDe(cubre.Puerto)
                           ? $"el valor es de {cubre.Estado.GetType().Name} y el puerto " +
                             $"contesta {FamiliaDe(cubre.Puerto).Name}"
                           : null
               where motivo is not null
               orderby cubre.Caso
               select $"{cubre.Caso} ({cubre.Puerto.Name}, {cubre.Estado}): {motivo}",
        ];

        // Las tres maneras de que una marca cuente como cobertura de nada. La tercera es la que
        // trae el `object`: `[CubreEstadoDelPuerto(typeof(IConsultaDeTarifas),
        // EstadoDelTercero.NoExiste)]` compila, y sin esta comprobación taparía la casilla
        // `NoExiste` de las tarifas —los dos valores son el cero— con un caso que habla de otra cosa.
        ajenos.ShouldBeEmpty(
            "hay marcas de cobertura que no apuntan a una casilla de esta matriz");
    }

    /// <summary>Y cada marca está sobre un caso de verdad, y de los que tocan la base.</summary>
    [Fact]
    public void Cada_cubrimiento_vive_en_un_caso_que_corre_contra_la_base()
    {
        IReadOnlyList<string> flojos =
        [
            .. from cubre in Cubrimientos()
               where !EsUnCaso(cubre.Metodo) || !CorreContraLaBase(cubre.Metodo)
               orderby cubre.Caso
               select EsUnCaso(cubre.Metodo)
                   ? $"{cubre.Caso}: no corre en el carril de integración"
                   : $"{cubre.Caso}: no es un caso de prueba",
        ];

        // Lo que decide el estado de un puerto es una consulta, así que una casilla afirmada con
        // un doble afirma lo que diga el doble. Los dobles de `Catalogo.UnitTests` y
        // `Terceros.UnitTests` prueban la TRADUCCIÓN de cada estado, que es otra cosa y no está
        // aquí; lo que aquí se exige es que el valor salga de PostgreSQL.
        flojos.ShouldBeEmpty(
            "hay marcas de cobertura que no las sostiene una aserción ejecutada contra la base");
    }

    /// <summary>Lo delegado tiene su matriz donde dice, y es una matriz de esa familia.</summary>
    [Fact]
    public void Cada_familia_delegada_tiene_su_matriz_en_el_otro_carril()
    {
        string raiz = RaizDelRepositorio.Ruta();

        foreach ((Type familia, string ruta) in s_matricesEnOtroCarril)
        {
            string fichero = Path.Combine(raiz, ruta);

            File.Exists(fichero).ShouldBeTrue(
                $"la familia {familia.Name} se da por cubierta en {ruta}, y ese fichero no está. " +
                "Una delegación a un sitio que no existe es una casilla sin dueño con otro nombre");

            string texto = File.ReadAllText(fichero);

            // Por el texto, porque este ensamblado no referencia aquel ni debe: lo que se busca es
            // que el fichero siga descubriendo SU familia y siga comparando casillas. Si alguien lo
            // reescribiera para mirar otra cosa, esta delegación dejaría de ser verdad.
            texto.ShouldContain(
                $"typeof({familia.Name}).Assembly",
                customMessage: $"{ruta} ya no descubre los puertos de {familia.Name}");

            texto.ShouldContain(
                "Cada_casilla_de_puerto_por_estado_esta_cubierta",
                customMessage: $"{ruta} ya no compara casillas");
        }
    }

    /// <summary>
    /// Los puertos de estado de todos los <c>Contracts</c> que acompañan a la API.
    /// </summary>
    /// <remarks>
    /// Se cargan desde el directorio de salida y no desde tipos ancla: con anclas, el
    /// <c>Contracts</c> de un módulo nuevo no entraría hasta que alguien se acordara de añadir la
    /// suya, que es la lista escrita a mano que esta regla existe para no tener.
    /// </remarks>
    private static IReadOnlyList<Type> Puertos() =>
        [.. Directory.GetFiles(AppContext.BaseDirectory, "Bastion.*.Contracts.dll")
            .Select(ruta => Assembly.Load(AssemblyName.GetAssemblyName(ruta)))
            .SelectMany(ensamblado => ensamblado.GetTypes())
            .Where(tipo => tipo.IsInterface && FamiliaONula(tipo) is not null)
            .Distinct()
            .OrderBy(tipo => tipo.FullName, StringComparer.Ordinal)];

    private static Type FamiliaDe(Type puerto) =>
        FamiliaONula(puerto)
        ?? throw new InvalidOperationException($"{puerto.Name} no contesta ningún estado.");

    // El enumerado de `Task<TEnumerado>`. Un puerto con dos métodos que contestaran dos enumerados
    // distintos no existe hoy; si llega, `Single` lo dice en vez de elegir uno a ciegas.
    private static Type? FamiliaONula(Type tipo) =>
        tipo.GetMethods()
            .Select(metodo => metodo.ReturnType)
            .Where(retorno => retorno.IsGenericType
                && retorno.GetGenericTypeDefinition() == typeof(Task<>)
                && retorno.GetGenericArguments()[0].IsEnum)
            .Select(retorno => retorno.GetGenericArguments()[0])
            .Distinct()
            .SingleOrDefault();

    /// <summary>Las casillas que alguien afirma, descubiertas por sus marcas.</summary>
    private static IReadOnlyList<Cubrimiento> Cubrimientos() =>
        [.. from tipo in typeof(LaMatrizDeLosPuertosDeEstadoTests).Assembly.GetTypes()
            from metodo in tipo.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            from marca in metodo.GetCustomAttributes<CubreEstadoDelPuertoAttribute>()
            select new Cubrimiento(
                marca.Puerto,
                marca.Estado,
                metodo,
                string.Create(CultureInfo.InvariantCulture, $"{tipo.Name}.{metodo.Name}"))];

    private static bool EsUnCaso(MethodInfo metodo) =>
        metodo.GetCustomAttributes().Any(atributo =>
            atributo is FactAttribute or TheoryAttribute);

    // Por los argumentos del constructor, como en la gemela: `TraitAttribute` no publica
    // propiedades. Y en el método o en su clase, que es donde este proyecto lo pone.
    private static bool CorreContraLaBase(MethodInfo metodo) =>
        Rasgos(metodo).Concat(Rasgos(metodo.DeclaringType))
            .Any(par => par is ["Category", "Integracion"]);

    private static IEnumerable<string?[]> Rasgos(MemberInfo? donde) =>
        (donde?.GetCustomAttributesData() ?? [])
            .Where(dato => dato.AttributeType == typeof(TraitAttribute))
            .Select(dato => dato.ConstructorArguments
                .Select(argumento => argumento.Value as string)
                .ToArray());

    private sealed record Cubrimiento(Type Puerto, object Estado, MethodInfo Metodo, string Caso);
}
