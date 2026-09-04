using System.Reflection;
using Shouldly;

namespace Bastion.Pruebas.Comun;

/// <summary>
/// El censo de un carril: la lista entera de sus casos, comparada contra la que está escrita.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es la última rendija de la vacuidad, y la única que las demás no pueden tapar.</b> Todo este
/// proyecto está montado para que una regla no pueda quedarse mirando al vacío; nada de eso sirve
/// contra una regla <b>borrada</b>. Un caso que desaparece no deja hueco: la suite sale verde, más
/// rápida, con un caso menos que nadie echa de menos, y la frontera que guardaba pasa a no estar
/// guardada por nadie.
/// </para>
/// <para>
/// <b>Está en un fichero compartido y enlazado, no copiado.</b> Desde el ítem 1.4 el censo cubre
/// varios ensamblados, y cada uno necesita su clase porque la reflexión solo alcanza al suyo. Lo
/// que NO puede repetirse es la consulta que descubre los casos: tres copias de estas doce líneas
/// divergen el día que una cuente los <c>[Theory]</c> y otra no, y entonces el carril que se quedó
/// atrás deja de censar justo lo que dejó de contar. El <c>Link</c> del <c>.csproj</c> lo compila
/// en cada ensamblado desde un único sitio.
/// </para>
/// <para>
/// <b>Lista de NOMBRES y no un recuento.</b> Un número diría que falta uno; esto dice cuál. Y se
/// compara <b>entera y en los dos sentidos</b>: de menos sale una regla borrada, y de más una
/// añadida sin declarar — que suena inocente y no lo es, porque la que se añade sin pasar por esta
/// lista es la que se añade sin que nadie decida si de verdad protege algo.
/// </para>
/// </remarks>
internal static class CensoDeReglas
{
    /// <summary>Los casos de un ensamblado, como <c>Clase.Metodo</c> y en orden.</summary>
    /// <remarks>
    /// <b><c>[Theory]</c> entra sin nombrarlo, y eso hay que saberlo.</b> En xUnit,
    /// <c>TheoryAttribute</c> deriva de <c>FactAttribute</c>, así que preguntar por el segundo
    /// encuentra los dos. No es un detalle cómodo: es una dependencia de una jerarquía ajena, y por
    /// eso <see cref="Comprobar"/> la afirma en vez de confiar en ella. Si algún día dejara de ser
    /// cierta, este censo dejaría de ver una familia entera de casos <b>sin ponerse rojo</b>.
    /// </remarks>
    /// <param name="ensamblado">El ensamblado del carril que se censa.</param>
    public static IReadOnlyList<string> Encontradas(Assembly ensamblado)
    {
        ArgumentNullException.ThrowIfNull(ensamblado);

        // El orden es ORDINAL y se pide con `Order(...)`, no con un `orderby`. La versión anterior
        // decía `orderby nombre, StringComparer.Ordinal`, que NO es «ordena por nombre con este
        // comparador»: en la sintaxis de consulta eso son DOS claves de orden —el nombre con el
        // comparador de la CULTURA y, a igualdad, una constante—, así que el comparador ordinal no
        // se aplicaba nunca. Con veintitrés nombres los dos órdenes coincidían y el carril salía
        // verde; con ciento treinta y tres dejan de coincidir, porque la cultura ordena
        // «El_contador» antes que «El_NIF» y el ordinal al revés. Es la misma trampa que un test
        // que hereda la cultura del ejecutor: verde justo en la máquina que tenía que avisar.
        return
        [
            .. (from tipo in ensamblado.GetTypes()
                where tipo.IsPublic && tipo.IsClass
                from metodo in tipo.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                where metodo.GetCustomAttribute<FactAttribute>() is not null
                select tipo.Name + "." + metodo.Name)
               .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>Compara el censo del ensamblado con la lista declarada, entera y sin vaciedad.</summary>
    /// <param name="ensamblado">El ensamblado del carril que se censa.</param>
    /// <param name="declaradas">Los nombres escritos a mano, en cualquier orden.</param>
    public static void Comprobar(Assembly ensamblado, IReadOnlyList<string> declaradas)
    {
        ArgumentNullException.ThrowIfNull(declaradas);

        // El arnés del arnés (ADR-0020). Si la reflexión dejara de encontrar casos —un cambio de
        // visibilidad, un atributo distinto, otro ensamblado— la comparación de abajo pasaría a
        // exigir que la lista escrita esté vacía, y el rojo diría «sobran doscientas» en vez de
        // «este barrido no ve nada». Con esto, lo primero que se lee es lo segundo.
        IReadOnlyList<string> encontradas = Encontradas(ensamblado);

        encontradas.ShouldNotBeEmpty(
            $"el censo no ha encontrado ni un caso en {ensamblado.GetName().Name}, así que no está " +
            "censando nada: lo que hay roto es el descubrimiento, no la lista declarada");

        // Y la pregunta de control sobre la jerarquía de xUnit, que es de quien depende que los
        // `[Theory]` entren en el censo. No es paranoia: es una herencia de una biblioteca ajena de
        // la que depende que una familia entera de casos se cuente.
        typeof(TheoryAttribute).IsSubclassOf(typeof(FactAttribute)).ShouldBeTrue(
            "`TheoryAttribute` ha dejado de derivar de `FactAttribute`, así que este censo ya no " +
            "ve los `[Theory]` y se pueden borrar sin que nada se ponga rojo");

        encontradas.ShouldBe([.. declaradas.Order(StringComparer.Ordinal)]);
    }
}
