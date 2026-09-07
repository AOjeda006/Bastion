using System.Globalization;
using System.Reflection;
using Bastion.Organizacion.Contracts.Comun;
using Shouldly;

namespace Bastion.Organizacion.IntegrationTests.Persistencia;

/// <summary>
/// Ningún valor de <see cref="EstadoDeMaestro"/> se queda sin que algún puerto lo conteste y
/// alguien lo afirme.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es la otra mitad de lo que el ADR-0020 tenía escrito.</b> La mitad conocida es la del
/// artículo 32: una lista cerrada a la que le <i>faltaban</i> valores para cosas que existían.
/// Esta es la imagen especular: una lista cerrada en la que <i>sobra</i> un valor que ningún
/// productor produce. <see cref="EstadoDeMaestro.SoloResuelveLoViejo"/> lo contestaba solo
/// <c>ConsultaDeImpuestos</c>; divisas y unidades no podían, y el comentario de las dos lo decía
/// —«la tercera llega con la retirada»— sin que nada comprobara que llegaba. Las dos mitades son
/// la misma pregunta: <b>¿la lista cerrada y el conjunto que enumera se comparan enteros?</b>
/// </para>
/// <para>
/// <b>Los dos lados se descubren.</b> Los puertos, de lo que el <c>Contracts</c> publica: toda
/// interfaz con un método que devuelva <c>Task&lt;EstadoDeMaestro&gt;</c>. Las casillas cubiertas,
/// de los <see cref="CubreEstadoDeMaestroAttribute"/> puestos sobre los casos que las afirman.
/// Ninguna de las dos es una lista escrita a mano, así que un puerto nuevo entra solo —con sus
/// tres casillas por cubrir— y un caso borrado se lleva su casilla por delante.
/// </para>
/// <para>
/// <b>Sin contenedor, y por eso NO lleva el rasgo <c>Integracion</c>.</b> Comparar dos conjuntos
/// no necesita PostgreSQL. Lo que sí lo necesita es la aserción que hay dentro de cada casilla, y
/// esa vive donde le toca, en el carril de integración: aquí solo se comprueba que ninguna casilla
/// se ha quedado sin dueño, que es exactamente lo que en verde no miraba nadie.
/// </para>
/// </remarks>
public sealed class LaMatrizDePuertoYEstadoTests
{
    /// <summary>Que haya matriz que mirar: sin esto, cero por cero sale verde.</summary>
    [Fact]
    public void La_matriz_no_esta_vacia()
    {
        Puertos().ShouldNotBeEmpty(
            "no se ha descubierto ningún puerto que devuelva `Task<EstadoDeMaestro>`. O el " +
            "contrato ha cambiado de forma, o se está mirando el ensamblado equivocado — y las " +
            "dos cosas dejan esta regla comparando la nada contra la nada");

        Estados().Length.ShouldBe(
            3,
            "`EstadoDeMaestro` tiene tres valores por el motivo escrito en su propio " +
            "`remarks`: son dos preguntas y no una. Si ahora tiene otro número, esta regla sigue " +
            "valiendo pero la matriz que hay que cubrir es otra, y eso se decide, no se hereda");

        Cubrimientos().ShouldNotBeEmpty(
            "no hay un solo caso marcado con `CubreEstadoDeMaestro`, así que la comparación de " +
            "abajo tendría todas las casillas vacías y diría lo mismo con la matriz cubierta que " +
            "sin ella");
    }

    /// <summary>Cada puerto contesta cada estado, y hay un caso que lo dice.</summary>
    [Fact]
    public void Cada_casilla_de_puerto_por_estado_esta_cubierta()
    {
        IReadOnlyList<string> sinDuenio =
        [
            .. from puerto in Puertos()
               from estado in Estados()
               where !Cubrimientos().Any(cubre =>
                   cubre.Puerto == puerto && cubre.Estado == estado)
               select $"{puerto.Name} -> {estado}",
        ];

        sinDuenio.ShouldBeEmpty(
            "hay casillas de la matriz puerto × estado que ningún caso afirma. Una casilla sin " +
            "dueño significa una de dos cosas, y las dos son malas: o el puerto no puede " +
            "contestar ese valor —y entonces la lista cerrada tiene un valor inalcanzable, que " +
            "es el defecto que abrió el ítem 1.7— o puede y nadie lo comprueba. Se cierra " +
            "marcando con `CubreEstadoDeMaestro` el caso que lo afirma, y si ese caso no existe " +
            "es que hay que escribirlo");
    }

    /// <summary>Ninguna marca nombra algo que no sea un puerto de este contrato.</summary>
    [Fact]
    public void Cada_cubrimiento_declarado_apunta_a_un_puerto_de_verdad()
    {
        IReadOnlyList<string> ajenos =
        [
            .. from cubre in Cubrimientos()
               where !Puertos().Contains(cubre.Puerto)
               orderby cubre.Puerto.Name, StringComparer.Ordinal
               select $"{cubre.Puerto.Name} (marcado en {cubre.Caso})",
        ];

        // La dirección que le falta a la comparación de arriba. Sin ella, una marca sobre un tipo
        // que ya no es puerto —renombrado, retirado, o que nunca lo fue— seguiría contando como
        // cobertura de nada, y la matriz saldría verde con una casilla cubierta por un fantasma.
        ajenos.ShouldBeEmpty(
            "hay marcas de cobertura sobre tipos que no son puertos de estado de este contrato. " +
            "Un puerto es una interfaz del `Contracts` con un método que devuelve " +
            "`Task<EstadoDeMaestro>`; lo que no lo sea no cubre ninguna casilla");
    }

    /// <summary>Y cada marca está sobre un caso de verdad, y de los que tocan la base.</summary>
    [Fact]
    public void Cada_cubrimiento_vive_en_un_caso_que_corre_contra_la_base()
    {
        IReadOnlyList<string> flojos =
        [
            .. from cubre in Cubrimientos()
               where !EsUnCaso(cubre.Metodo) || !CorreContraLaBase(cubre.Metodo)
               orderby cubre.Caso, StringComparer.Ordinal
               select EsUnCaso(cubre.Metodo)
                   ? $"{cubre.Caso}: no corre en el carril de integración"
                   : $"{cubre.Caso}: no es un caso de prueba",
        ];

        // Las dos maneras de que una marca no signifique nada: puesta sobre un método corriente
        // —que no ejecuta nadie— o sobre un caso que no habla con PostgreSQL. Lo segundo importa
        // tanto como lo primero: el estado que contesta un puerto lo decide una consulta, así que
        // una casilla afirmada sin base de datos afirma lo que diga un doble.
        flojos.ShouldBeEmpty(
            "hay marcas de cobertura que no las sostiene una aserción ejecutada contra la base");
    }

    private static EstadoDeMaestro[] Estados() => Enum.GetValues<EstadoDeMaestro>();

    /// <summary>Los puertos, tal como el <c>Contracts</c> del módulo los publica.</summary>
    private static IReadOnlyList<Type> Puertos() =>
        [.. typeof(EstadoDeMaestro).Assembly
            .GetTypes()
            .Where(tipo => tipo.IsInterface && DevuelveUnEstado(tipo))
            .OrderBy(tipo => tipo.Name, StringComparer.Ordinal)];

    private static bool DevuelveUnEstado(Type tipo) =>
        tipo.GetMethods().Any(metodo => metodo.ReturnType == typeof(Task<EstadoDeMaestro>));

    /// <summary>Las casillas que alguien afirma, descubiertas por sus marcas.</summary>
    private static IReadOnlyList<Cubrimiento> Cubrimientos() =>
        [.. from tipo in typeof(LaMatrizDePuertoYEstadoTests).Assembly.GetTypes()
            from metodo in tipo.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            from marca in metodo.GetCustomAttributes<CubreEstadoDeMaestroAttribute>()
            select new Cubrimiento(
                marca.Puerto,
                marca.Estado,
                metodo,
                string.Create(CultureInfo.InvariantCulture, $"{tipo.Name}.{metodo.Name}"))];

    private static bool EsUnCaso(MethodInfo metodo) =>
        metodo.GetCustomAttributes().Any(atributo =>
            atributo is FactAttribute or TheoryAttribute);

    // Por los argumentos del constructor y no por propiedades: `TraitAttribute` de xUnit no las
    // publica, solo guarda el par que se le pasa. Se miran el metodo y su clase, porque el rasgo
    // se puede poner en cualquiera de los dos.
    private static bool CorreContraLaBase(MethodInfo metodo) =>
        Rasgos(metodo).Concat(Rasgos(metodo.DeclaringType))
            .Any(par => par is ["Category", "Integracion"]);

    private static IEnumerable<string?[]> Rasgos(MemberInfo? donde) =>
        (donde?.GetCustomAttributesData() ?? [])
            .Where(dato => dato.AttributeType == typeof(TraitAttribute))
            .Select(dato => dato.ConstructorArguments
                .Select(argumento => argumento.Value as string)
                .ToArray());

    private sealed record Cubrimiento(
        Type Puerto,
        EstadoDeMaestro Estado,
        MethodInfo Metodo,
        string Caso);
}
