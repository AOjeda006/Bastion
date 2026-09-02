using System.Globalization;
using System.Reflection;
using Bastion.BuildingBlocks.Application;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Composicion;

/// <summary>
/// Con un solo módulo, registrar <see cref="IUnidadTrabajo"/> a secas funciona. Con dos, la última
/// inscripción gana y todos los casos de uso de todos los módulos confirman sobre el MISMO
/// contexto.
/// </summary>
/// <remarks>
/// <para>
/// El fallo no da error. <c>SaveChangesAsync</c> sobre el contexto equivocado no ve ninguna
/// entidad rastreada, devuelve cero filas y no se queja: el caso de uso sale correcto, la API
/// contesta <c>201</c> con su DTO y en la base no hay nada. Es exactamente la avería que se
/// construye sin error, deja el registro correcto y no ejecuta nada.
/// </para>
/// <para>
/// La defensa no es acordarse: es que cada módulo pida <b>su</b> unidad de trabajo, de modo que el
/// contenedor no tenga que adivinar cuál toca. Este test guarda esa regla — si alguien vuelve a
/// pedir la común, aquí se ve.
/// </para>
/// <para>
/// <b>Los ensamblados se DESCUBREN, no se teclean.</b> Hasta el ítem 0.13 eran dos
/// <c>typeof(…).Assembly</c> escritos a mano, y les faltaba <c>Bastion.Auditoria.Application</c>:
/// la lista no había crecido cuando creció el proyecto, y un módulo que nadie miraba salía verde
/// por no estar mirado. Ahora se leen del directorio donde está <c>Bastion.Api.dll</c>, que es la
/// clausura que el host despliega de verdad; un módulo nuevo entra solo el día que se monta.
/// </para>
/// <para>
/// Se descubre por FICHERO y no con <see cref="Assembly.GetReferencedAssemblies"/>, y la
/// diferencia es justo la que se encontró en el ítem 0.12 con NetArchTest: los nombres que
/// devuelve esa llamada salen del IL, así que un ensamblado del que todavía no se usa ningún tipo
/// —hoy, <c>Bastion.Auditoria.Application</c>— <b>no aparece</b>. Descubrir desde el IL habría
/// dejado fuera exactamente el ensamblado que faltaba, o sea que habría reproducido el fallo
/// original con más ceremonia.
/// </para>
/// </remarks>
public sealed class UnidadDeTrabajoPorModuloTests
{
    /// <summary>El ensamblado del host, que es el ancla de todo el descubrimiento.</summary>
    private const string Host = "Bastion.Api.dll";

    /// <summary>
    /// El bloque común NO es un módulo: es donde vive <see cref="IUnidadTrabajo"/>, la interfaz
    /// que estos tests prohíben pedir. Incluirlo sería exigirle una unidad de trabajo propia a
    /// quien define el concepto.
    /// </summary>
    private const string BloqueComun = "Bastion.BuildingBlocks.Application.dll";

    private static readonly IReadOnlyList<Assembly> s_capasDeAplicacion = Descubrir();

    [Fact]
    public void NingunCasoDeUso_PideLaUnidadDeTrabajoComun()
    {
        List<string> culpables =
        [
            .. from ensamblado in s_capasDeAplicacion
               from tipo in ensamblado.GetTypes()
               from constructor in tipo.GetConstructors(
                   BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
               from parametro in constructor.GetParameters()
               where parametro.ParameterType == typeof(IUnidadTrabajo)
               select $"{tipo.FullName}({parametro.Name})",
        ];

        culpables.ShouldBeEmpty(
            "estos tipos piden IUnidadTrabajo a secas, y con dos módulos registrados el " +
            "contenedor les dará la del último que se inscribiera: tienen que pedir la de su " +
            "propio módulo");
    }

    [Fact]
    public void CadaModulo_DeclaraSuPropiaUnidadDeTrabajo()
    {
        List<string> sinCasosDeUso = [];
        List<string> conUnidadPropia = [];
        List<string> incumplen = [];

        foreach (Assembly ensamblado in s_capasDeAplicacion)
        {
            string nombre = ensamblado.GetName().Name ?? ensamblado.FullName!;
            Type[] tipos = ensamblado.GetTypes();

            // Una capa de aplicación VACÍA no incumple: no tiene casos de uso a los que dar una
            // unidad de trabajo. Es el estado de Auditoría, cuyo módulo escribe por un interceptor
            // y no por un caso de uso. Se anota aparte en vez de darla por buena en silencio: el
            // día que aparezca su primer tipo, este test empieza a exigirle la suya sin que nadie
            // tenga que acordarse.
            if (tipos.Length == 0)
            {
                sinCasosDeUso.Add(nombre);

                continue;
            }

            Type[] propias = [.. tipos.Where(EsUnaUnidadDeTrabajoDeModulo)];

            if (propias.Length == 1)
            {
                conUnidadPropia.Add(nombre);
            }
            else
            {
                incumplen.Add($"{nombre} declara {propias.Length.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        incumplen.ShouldBeEmpty(
            "cada capa de aplicación con tipos tiene que declarar exactamente UNA interfaz que " +
            "herede de IUnidadTrabajo, que es la que piden sus casos de uso. Capas descubiertas: " +
            Descripcion());

        // La afirmación de conjunto no vacío, que es la que impide que este test se cumpla por no
        // haber mirado nada. Sin ella, un descubrimiento roto —una ruta que cambia, un `.dll` que
        // deja de copiarse— deja los dos hechos en verde habiendo recorrido cero ensamblados.
        conUnidadPropia.ShouldNotBeEmpty(
            "ninguna capa de aplicación declara una unidad de trabajo propia. O el proyecto ha " +
            "dejado de tener casos de uso, o el descubrimiento no está encontrando nada. Capas " +
            "descubiertas: " + Descripcion());

        // Y la contraria: que las vacías sean MENOS que el total. Si un día se descubrieran solo
        // ensamblados sin tipos, la comprobación de arriba sería la única que hablaría y esta lo
        // dice antes.
        sinCasosDeUso.Count.ShouldBeLessThan(
            s_capasDeAplicacion.Count,
            "todas las capas de aplicación descubiertas están vacías: " + Descripcion());
    }

    /// <summary>
    /// Los <c>Bastion.*.Application.dll</c> que acompañan al host, ordenados y sin el bloque común.
    /// </summary>
    private static IReadOnlyList<Assembly> Descubrir()
    {
        string directorio = AppContext.BaseDirectory;

        // El ancla. Si el host no está donde corren los tests, lo que venga después no es «no hay
        // capas de aplicación»: es que se está mirando el sitio equivocado, y hay que decirlo aquí
        // y no dejar que salga como un conjunto vacío que todo lo cumple.
        File.Exists(Path.Combine(directorio, Host)).ShouldBeTrue(
            $"no está {Host} en {directorio}: el descubrimiento de capas de aplicación se apoya " +
            "en que los ensamblados del host se copian junto a los tests, y ahí no hay host");

        return
        [
            .. Directory.EnumerateFiles(directorio, "Bastion.*.Application.dll")
                .Where(ruta => !string.Equals(Path.GetFileName(ruta), BloqueComun, StringComparison.Ordinal))
                .OrderBy(ruta => Path.GetFileName(ruta), StringComparer.Ordinal)
                .Select(Assembly.LoadFrom),
        ];
    }

    private static string Descripcion() =>
        string.Join(", ", s_capasDeAplicacion.Select(ensamblado => ensamblado.GetName().Name));

    private static bool EsUnaUnidadDeTrabajoDeModulo(Type tipo) =>
        tipo.IsInterface
        && tipo != typeof(IUnidadTrabajo)
        && typeof(IUnidadTrabajo).IsAssignableFrom(tipo);
}
