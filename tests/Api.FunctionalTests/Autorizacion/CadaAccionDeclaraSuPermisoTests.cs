using Bastion.Api.FunctionalTests.Salud;
using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Autorizacion;

/// <summary>
/// La forma de la autorización, mirada sobre <b>la tabla de rutas que el host ha construido de
/// verdad</b>, no sobre los ficheros de código.
/// </summary>
/// <remarks>
/// <para>
/// Estos tests no ejercitan una petición, y por eso no sustituyen a los de 401 y 403: comprueban
/// lo que aquellos no pueden, que es que la lista esté COMPLETA. Un endpoint nuevo al que se le
/// olvide el atributo no rompe ningún test de 403 —nadie ha escrito el suyo— y aquí sí.
/// </para>
/// <para>
/// La fuente es <see cref="IActionDescriptorCollectionProvider"/>, que es la tabla que usa el
/// enrutado para servir. Leerla con reflexión sobre los tipos daría el mismo resultado hoy y
/// dejaría de darlo el día que una acción se publique de otra manera.
/// </para>
/// </remarks>
public sealed class CadaAccionDeclaraSuPermisoTests : IDisposable
{
    // Las cuatro acciones sin permiso de todo el sistema, con su motivo. La lista es corta a
    // propósito: cada nombre que se añada aquí es una puerta que deja de estar detrás de un
    // permiso, y tiene que costar escribirlo.
    private static readonly Dictionary<string, string> s_sinPermisoAPosta = new(StringComparer.Ordinal)
    {
        ["SesionesController.Iniciar"] = "es la manera de conseguir permisos; no puede exigir uno",
        ["SesionesController.Renovar"] = "lo autoriza la cookie de refresco, no un permiso",
        ["SesionesController.Cerrar"] = "cerrar la sesión propia no se le niega a nadie",
        ["SesionesController.CambiarEmpresa"] =
            "elegir entre las empresas a las que uno ya pertenece no es una facultad que se conceda",
        ["UsuariosController.CambiarContrasenaPropia"] =
            "cambiar la contraseña de uno mismo no puede depender de un permiso que se le pueda quitar",
    };

    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void Toda_accion_o_exige_un_permiso_o_esta_en_la_lista_de_excepciones()
    {
        List<string> sinDeclarar = [.. Acciones()
            .Where(accion => PermisoDe(accion) is null && !s_sinPermisoAPosta.ContainsKey(Nombre(accion)))
            .Select(Nombre)];

        // El mensaje lleva los nombres porque un «esperaba 0, había 3» obliga a ir a buscarlos.
        sinDeclarar.ShouldBeEmpty(
            "estas acciones no dicen qué permiso exigen, así que solo piden estar autenticado: " +
            string.Join(", ", sinDeclarar));
    }

    [Fact]
    public void Ninguna_accion_autoriza_por_rol_ni_por_una_politica_inventada()
    {
        List<string> sospechosas = [];

        foreach (ActionDescriptor accion in Acciones())
        {
            foreach (AuthorizeAttribute atributo in accion.EndpointMetadata.OfType<AuthorizeAttribute>())
            {
                // Un rol repartido por los controladores es lo que hace que «quién puede hacer
                // qué» no se pueda contestar sin leerlos todos, y que añadir un perfil obligue a
                // tocar código (§11: permisos por acción, roles como agrupación de permisos).
                if (!string.IsNullOrEmpty(atributo.Roles))
                {
                    sospechosas.Add($"{Nombre(accion)} autoriza por rol «{atributo.Roles}»");
                }

                // Una política con nombre propio se resuelve por otro camino que el manejador de
                // permisos, y ese camino no lo cubre ningún test de 403.
                if (!string.IsNullOrEmpty(atributo.Policy) &&
                    !atributo.Policy.StartsWith(ExigePermisoAttribute.Prefijo, StringComparison.Ordinal))
                {
                    sospechosas.Add($"{Nombre(accion)} usa la política «{atributo.Policy}»");
                }
            }
        }

        sospechosas.ShouldBeEmpty(string.Join(", ", sospechosas));
    }

    [Fact]
    public void Todo_permiso_exigido_existe_en_el_catalogo_que_registra_el_host()
    {
        ICatalogoDePermisos catalogo = _api.Services.GetRequiredService<ICatalogoDePermisos>();

        List<string> fantasmas = [.. Acciones()
            .Select(accion => (Accion: Nombre(accion), Permiso: PermisoDe(accion)))
            .Where(par => par.Permiso is not null && !catalogo.Contiene(par.Permiso!))
            .Select(par => $"{par.Accion} exige «{par.Permiso}»")];

        // Un permiso que no está en el catálogo no se le puede conceder a nadie: la acción queda
        // cerrada para todo el mundo, incluido el administrador, y no da error en ningún sitio.
        fantasmas.ShouldBeEmpty(string.Join(", ", fantasmas));
    }

    [Fact]
    public void Todo_permiso_del_catalogo_lo_exige_alguna_accion()
    {
        ICatalogoDePermisos catalogo = _api.Services.GetRequiredService<ICatalogoDePermisos>();

        HashSet<string> exigidos = [.. Acciones()
            .Select(PermisoDe)
            .Where(permiso => permiso is not null)
            .Select(permiso => permiso!.Valor)];

        List<string> huerfanos = [.. catalogo.Todos
            .Select(permiso => permiso.Valor)
            .Where(valor => !exigidos.Contains(valor))];

        // Al revés que el anterior: un permiso que se puede conceder y que no abre nada es una
        // promesa falsa en la pantalla de roles. Suele significar que falta el endpoint.
        huerfanos.ShouldBeEmpty(
            "estos permisos se pueden conceder y no abren ninguna puerta: " + string.Join(", ", huerfanos));
    }

    [Fact]
    public void Cada_permiso_es_del_modulo_por_cuya_ruta_se_entra()
    {
        List<string> cruzados = [];

        foreach (ActionDescriptor accion in Acciones())
        {
            Permiso? permiso = PermisoDe(accion);
            string? plantilla = accion.AttributeRouteInfo?.Template;

            if (permiso is null || plantilla is null)
            {
                continue;
            }

            // `api/v1/{modulo}/{recurso}`: el tercer segmento es el módulo (§9 y Anexo A.1).
            string moduloDeLaRuta = plantilla.Split('/')[2];

            if (!string.Equals(permiso.Modulo, moduloDeLaRuta, StringComparison.Ordinal))
            {
                cruzados.Add($"{Nombre(accion)} se sirve en «{moduloDeLaRuta}» y exige «{permiso}»");
            }
        }

        // Copiar y pegar un endpoint de otro módulo y olvidarse de cambiar la constante deja la
        // puerta abierta a quien tenga el permiso del módulo equivocado, y el log sale correcto.
        cruzados.ShouldBeEmpty(string.Join(", ", cruzados));
    }

    [Fact]
    public void Escribir_y_modificar_no_comparten_permiso_aunque_los_escriba_el_mismo_codigo()
    {
        Dictionary<string, List<string>> porPermiso = [];

        foreach (ActionDescriptor accion in Acciones())
        {
            Permiso? permiso = PermisoDe(accion);

            if (permiso is null || !EsEscritura(accion))
            {
                continue;
            }

            if (!porPermiso.TryGetValue(permiso.Valor, out List<string>? cuales))
            {
                porPermiso[permiso.Valor] = cuales = [];
            }

            cuales.Add(Nombre(accion));
        }

        List<string> compartidos = [.. porPermiso
            .Where(par => par.Value.Count > 1)
            .Select(par => $"«{par.Key}» abre {string.Join(" y ", par.Value)}")];

        // Autorizar una operación no autoriza lo que esa operación escribe. Crear y modificar se
        // conceden por separado —hay perfiles que dan de alta y no corrigen, y al revés—, y un
        // permiso compartido hace imposible expresarlo por mucho que el código sea parecido.
        // Las lecturas SÍ comparten: `ver` es una sola facultad, la liste quien la liste.
        compartidos.ShouldBeEmpty(string.Join("; ", compartidos));
    }

    private static string Nombre(ActionDescriptor accion) => accion is ControllerActionDescriptor controlador
        ? $"{controlador.ControllerTypeInfo.Name}.{controlador.ActionName}"
        : accion.DisplayName ?? "(sin nombre)";

    private static Permiso? PermisoDe(ActionDescriptor accion) =>
        accion.EndpointMetadata.OfType<ExigePermisoAttribute>().FirstOrDefault()?.Permiso;

    private static bool EsEscritura(ActionDescriptor accion) =>
        accion.ActionConstraints?
            .OfType<HttpMethodActionConstraint>()
            .SelectMany(restriccion => restriccion.HttpMethods)
            .Any(metodo => !string.Equals(metodo, "GET", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(metodo, "HEAD", StringComparison.OrdinalIgnoreCase)) ?? false;

    private IReadOnlyList<ActionDescriptor> Acciones() =>
        _api.Services.GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items;
}
