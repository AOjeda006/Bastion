using System.Reflection;
using Bastion.BuildingBlocks.Application;
using Bastion.Identidad.Application;
using Bastion.Organizacion.Application;
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
/// </remarks>
public sealed class UnidadDeTrabajoPorModuloTests
{
    private static readonly Assembly[] s_capasDeAplicacion =
    [
        typeof(CasosDeUsoDeOrganizacion).Assembly,
        typeof(CasosDeUsoDeIdentidad).Assembly,
    ];

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
        foreach (Assembly ensamblado in s_capasDeAplicacion)
        {
            Type[] propias = [.. ensamblado.GetTypes().Where(EsUnaUnidadDeTrabajoDeModulo)];

            propias.Length.ShouldBe(
                1,
                $"{ensamblado.GetName().Name} tiene que declarar exactamente una interfaz que " +
                "herede de IUnidadTrabajo, que es la que piden sus casos de uso");
        }
    }

    private static bool EsUnaUnidadDeTrabajoDeModulo(Type tipo) =>
        tipo.IsInterface
        && tipo != typeof(IUnidadTrabajo)
        && typeof(IUnidadTrabajo).IsAssignableFrom(tipo);
}
