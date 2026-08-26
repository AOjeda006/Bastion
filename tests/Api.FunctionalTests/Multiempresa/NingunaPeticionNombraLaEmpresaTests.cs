using Bastion.Api.FunctionalTests.Salud;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Multiempresa;

/// <summary>
/// La forma fuerte de «el identificador del cuerpo se ignora»: que <b>no haya</b> identificador de
/// empresa que ignorar.
/// </summary>
/// <remarks>
/// <para>
/// Comprobar que un campo se ignora exige que el campo exista, y un campo que existe es un campo
/// que algún día alguien lee «solo para este caso». La regla de verdad es la ausencia: la empresa
/// entra por el <i>claim</i> y por ningún otro sitio (R8), así que ninguna acción debería tener
/// por dónde recibirla — ni en el cuerpo, ni en la ruta, ni en la cadena de consulta, ni en una
/// cabecera.
/// </para>
/// <para>
/// <b>Las excepciones son cuatro y son de la misma familia:</b> operaciones cuyo SUJETO es la
/// empresa —a cuál se entra, en cuál se da de alta a alguien— y no operaciones sobre filas de una
/// empresa. En todas ellas el valor recibido <b>no se usa como inquilino</b>: se contrasta contra
/// el <i>claim</i> antes de tocar nada, y quien lo hace está nombrado en la lista.
/// </para>
/// <para>
/// Se lee la tabla de rutas del host, igual que <c>CadaAccionDeclaraSuPermisoTests</c>: un
/// endpoint nuevo entra aquí solo, sin que nadie se acuerde de añadirlo.
/// </para>
/// </remarks>
public sealed class NingunaPeticionNombraLaEmpresaTests : IDisposable
{
    // Acción -> quién comprueba el valor recibido. Añadir una línea aquí es abrir una puerta por
    // la que entra un identificador de empresa: tiene que costar escribirla, y tiene que decir
    // quién la vigila.
    private static readonly Dictionary<string, string> s_puedenNombrarla = new(StringComparer.Ordinal)
    {
        ["SesionesController.Iniciar"] =
            "sugiere con qué empresa empezar entre las propias; la elige usuario.EnEmpresa y, si no " +
            "pertenece, sale por credenciales incorrectas",

        ["SesionesController.CambiarEmpresa"] =
            "es literalmente «cambia de empresa»; usuario.EnEmpresa niega la que no es suya",

        ["UsuariosController.Conceder"] =
            "da de alta en una empresa que puede no ser la activa (arranque en frío); lo decide " +
            "ErroresDePertenencia.PuedeAdministrarAsync",

        ["UsuariosController.Retirar"] =
            "retira de una empresa que puede no ser la activa; mismo guardián",

        ["UsuariosController.AsignarRol"] =
            "el rol se asigna sobre la pertenencia de esa empresa; mismo guardián, vía ResolverAsync",

        ["UsuariosController.RetirarRol"] =
            "ídem",
    };

    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void Ninguna_accion_recibe_la_empresa_por_la_peticion()
    {
        List<string> puertas = [];

        foreach (ActionDescriptor accion in Acciones())
        {
            string nombre = Nombre(accion);

            if (s_puedenNombrarla.ContainsKey(nombre))
            {
                continue;
            }

            foreach (ParameterDescriptor parametro in accion.Parameters)
            {
                foreach (string sitio in DondeSeNombraLaEmpresa(parametro))
                {
                    puertas.Add($"{nombre} la recibe en {sitio}");
                }
            }
        }

        // Un campo `empresaId` en un DTO de alta es un campo que el enlace de modelo rellena y
        // que el caso de uso puede leer «solo esta vez». La única forma de que eso no pase es que
        // no esté.
        puertas.ShouldBeEmpty(string.Join("; ", puertas));
    }

    [Fact]
    public void La_lista_de_excepciones_no_nombra_acciones_que_ya_no_la_reciben()
    {
        HashSet<string> laReciben = [.. Acciones()
            .Where(accion => accion.Parameters.Any(parametro => DondeSeNombraLaEmpresa(parametro).Count > 0))
            .Select(Nombre)];

        List<string> sobran = [.. s_puedenNombrarla.Keys.Where(nombre => !laReciben.Contains(nombre))];

        // Una excepción que ya no hace falta es un permiso que sigue concedido. Se quita.
        sobran.ShouldBeEmpty(
            "estas acciones están autorizadas a recibir la empresa y ya no la reciben: " +
            string.Join(", ", sobran));
    }

    private static List<string> DondeSeNombraLaEmpresa(ParameterDescriptor parametro)
    {
        BindingSource? origen = parametro.BindingInfo?.BindingSource;
        string sitio = origen?.DisplayName ?? "la petición";
        List<string> hallazgos = [];

        if (Nombra(parametro.Name))
        {
            hallazgos.Add($"{sitio} («{parametro.Name}»)");
        }

        foreach (string miembro in MiembrosQueNombranLaEmpresa(parametro.ParameterType, 0))
        {
            hallazgos.Add($"{sitio}, en {parametro.ParameterType.Name}.{miembro}");
        }

        return hallazgos;
    }

    // Recursiva y acotada a los tipos de Bastion: un DTO puede anidar otro, y el campo prohibido
    // escondido un nivel más abajo se enlaza igual de bien.
    private static IEnumerable<string> MiembrosQueNombranLaEmpresa(Type tipo, int profundidad)
    {
        if (profundidad > 3 || tipo.Namespace?.StartsWith("Bastion.", StringComparison.Ordinal) != true)
        {
            yield break;
        }

        foreach (System.Reflection.PropertyInfo propiedad in tipo.GetProperties())
        {
            if (Nombra(propiedad.Name))
            {
                yield return propiedad.Name;

                continue;
            }

            foreach (string anidado in MiembrosQueNombranLaEmpresa(propiedad.PropertyType, profundidad + 1))
            {
                yield return $"{propiedad.Name}.{anidado}";
            }
        }
    }

    private static bool Nombra(string? nombre) =>
        nombre?.Contains("mpresa", StringComparison.Ordinal) == true;

    private static string Nombre(ActionDescriptor accion) => accion is ControllerActionDescriptor controlador
        ? $"{controlador.ControllerTypeInfo.Name}.{controlador.ActionName}"
        : accion.DisplayName ?? "(sin nombre)";

    private IReadOnlyList<ActionDescriptor> Acciones() =>
        _api.Services.GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items;
}
