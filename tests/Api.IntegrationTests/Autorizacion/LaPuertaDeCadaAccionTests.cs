using System.Net;
using System.Text;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Bastion.Identidad.Contracts;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Autorizacion;

/// <summary>
/// La regla de autorización de <b>cada</b> acción del sistema, ejercitada con una petición.
/// </summary>
/// <remarks>
/// <para>
/// La pregunta que contesta este fichero es «¿qué se rompería si esto no estuviera?», y la
/// contesta de la única manera que vale: quitando las credenciales y mirando qué devuelve, y
/// poniendo las equivocadas y mirando qué devuelve. Leer la configuración o el registro no prueba
/// nada — una cadena mal emparejada se construye sin error, deja el log correcto y deja la API
/// abierta.
/// </para>
/// <para>
/// Y barre la tabla de rutas entera en vez de una lista escrita a mano. Una regla que nadie se
/// acordó de probar es exactamente la que falta.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaPuertaDeCadaAccionTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    // Las tres acciones anónimas del sistema. Están aquí para que el barrido no las exija cerradas
    // y, sobre todo, para que la lista de lo que está abierto sea UNA y esté escrita.
    private static readonly HashSet<string> s_anonimas = new(StringComparer.Ordinal)
    {
        "SesionesController.Iniciar",
        "SesionesController.Renovar",
        "SesionesController.Cerrar",
    };

    // Autenticadas, pero sin permiso: cualquiera que haya entrado las puede usar sobre lo suyo.
    private static readonly HashSet<string> s_sinPermiso = new(StringComparer.Ordinal)
    {
        "SesionesController.CambiarEmpresa",
        "UsuariosController.CambiarContrasenaPropia",
    };

    private readonly ApiDeVerdad _api = new(postgres);

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Sin_credenciales_toda_accion_protegida_responde_401()
    {
        using HttpClient cliente = _api.CrearCliente();
        List<string> abiertas = [];

        foreach (ActionDescriptor accion in Protegidas())
        {
            using HttpRequestMessage peticion = PeticionDeSondeo.De(accion, token: null);
            using HttpResponseMessage respuesta = await cliente.SendAsync(peticion);

            if (respuesta.StatusCode != HttpStatusCode.Unauthorized)
            {
                abiertas.Add($"{PeticionDeSondeo.Nombre(accion)} → {(int)respuesta.StatusCode}");
            }
        }

        // Un 404 aquí sería la peor noticia posible: significaría que la petición ha entrado, ha
        // buscado el recurso y no lo ha encontrado. La puerta no estaba.
        abiertas.ShouldBeEmpty(
            "estas acciones contestan a quien no se ha identificado: " + string.Join(", ", abiertas));
    }

    [Fact]
    public async Task Con_un_permiso_que_no_es_el_suyo_toda_accion_protegida_responde_403()
    {
        // Un solo permiso, y uno que no abre casi nada. Sirve para todas: lo que se comprueba es
        // que la acción mira SU permiso y no «que traiga alguno».
        using HttpClient cliente = await _api.ConPermisosAsync(PermisosDeIdentidad.RolVer);
        string token = TokenDe(cliente);

        List<string> coladas = [];

        foreach (ActionDescriptor accion in Protegidas())
        {
            Permiso permiso = PeticionDeSondeo.PermisoDe(accion)!;

            if (permiso.Valor == PermisosDeIdentidad.RolVer)
            {
                continue;
            }

            using HttpRequestMessage peticion = PeticionDeSondeo.De(accion, token);
            using HttpResponseMessage respuesta = await cliente.SendAsync(peticion);

            if (!await EsPuertaCerradaAsync(respuesta))
            {
                coladas.Add($"{PeticionDeSondeo.Nombre(accion)} ({permiso}) → {(int)respuesta.StatusCode}");
            }
        }

        coladas.ShouldBeEmpty(
            "estas acciones dejan pasar a quien trae otro permiso: " + string.Join(", ", coladas));
    }

    [Fact]
    public async Task Con_su_permiso_y_solo_con_el_suyo_ninguna_accion_responde_401_ni_403()
    {
        List<string> cerradas = [];

        // Lo que salga después —400 por el cuerpo vacío, 404 por el identificador inventado— da
        // igual: lo que se comprueba es que la puerta se abre con ESTE permiso y con uno solo. Sin
        // este test, denegarlo todo pasaría los otros dos.
        await ConSuPermisoAsync(async (accion, respuesta) =>
        {
            if (await EsPuertaCerradaAsync(respuesta))
            {
                cerradas.Add(
                    $"{PeticionDeSondeo.Nombre(accion)} ({PeticionDeSondeo.PermisoDe(accion)}) → {(int)respuesta.StatusCode}");
            }
        });

        cerradas.ShouldBeEmpty(
            "estas acciones no se abren ni con el permiso que declaran exigir: " + string.Join(", ", cerradas));
    }

    [Fact]
    public async Task Ninguna_accion_contesta_con_un_fallo_del_servidor_al_sondeo()
    {
        List<string> reventadas = [];

        // El barrido de arriba da por buena CUALQUIER respuesta que no sea 401 ni 403, y eso
        // incluye un 500. Es correcto para lo que aquel comprueba —la puerta— y es un agujero
        // como evidencia: una acción que estalla nada más entrar lo pasa igual de bien que una
        // que funciona. Pasó de verdad, y con las diez acciones de pertenencias a la vez
        // (ADR-0010); lo que lo delató fue un test de contrato, no este barrido.
        //
        // Aquí un 500 no puede ser correcto: el sondeo manda cuerpos vacíos e identificadores
        // inventados, o sea, entrada mala, y a la entrada mala se contesta 4xx.
        await ConSuPermisoAsync((accion, respuesta) =>
        {
            if ((int)respuesta.StatusCode >= 500)
            {
                reventadas.Add($"{PeticionDeSondeo.Nombre(accion)} → {(int)respuesta.StatusCode}");
            }

            return Task.CompletedTask;
        });

        reventadas.ShouldBeEmpty(
            "estas acciones contestan con un fallo del servidor a una petición de sondeo: "
            + string.Join(", ", reventadas));
    }

    [Fact]
    public async Task Las_acciones_sin_permiso_las_puede_usar_cualquiera_que_haya_entrado()
    {
        using HttpClient cliente = await _api.ConPermisosAsync();
        string token = TokenDe(cliente);

        List<string> negadas = [];

        foreach (ActionDescriptor accion in Acciones()
            .Where(accion => s_sinPermiso.Contains(PeticionDeSondeo.Nombre(accion))))
        {
            using HttpRequestMessage peticion = PeticionDeSondeo.De(accion, token);
            using HttpResponseMessage respuesta = await cliente.SendAsync(peticion);

            if (await EsPuertaCerradaAsync(respuesta))
            {
                negadas.Add($"{PeticionDeSondeo.Nombre(accion)} → {(int)respuesta.StatusCode}");
            }
        }

        // Una cuenta sin ni un permiso. Cambiar la contraseña propia y elegir entre las empresas a
        // las que uno ya pertenece no se conceden: si dependieran de un permiso, quitárselo a
        // alguien lo dejaría sin poder cambiar una contraseña comprometida.
        negadas.ShouldBeEmpty(string.Join(", ", negadas));
    }

    [Fact]
    public async Task Las_tres_acciones_anonimas_se_alcanzan_sin_credenciales()
    {
        using HttpClient cliente = _api.CrearCliente();

        // Un 400 del enlace de modelo solo puede salir si la petición ha ENTRADO: con la puerta
        // cerrada, el 401 habría llegado antes de mirar el cuerpo.
        using StringContent vacio = new("{}", Encoding.UTF8, "application/json");
        (await cliente.PostAsync("/api/v1/identidad/sesiones", vacio)).StatusCode
            .ShouldBe(HttpStatusCode.BadRequest);

        // Cerrar sin haber abierto no es un error: el efecto que se pide —que esta sesión no
        // valga— ya se cumple. Y borra la cookie igual, que es lo único que hay que hacer.
        (await cliente.DeleteAsync("/api/v1/identidad/sesiones/actual")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        // La tercera, la renovación, responde 401 sin cookie: no se puede distinguir de la puerta
        // cerrada mirando el código. Lo que prueba que es anónima es que funcione CON cookie y
        // sin cabecera de autorización, y eso está en los tests de rotación.
    }

    [Fact]
    public async Task Una_ruta_que_no_existe_es_404_para_quien_si_se_ha_identificado()
    {
        using HttpClient cliente = await _api.ComoAdministradorAsync();

        using HttpResponseMessage respuesta = await cliente.GetAsync("/api/v1/esto-no-existe");

        // El otro lado del 401 que ve un anónimo (Api.FunctionalTests): la política de respaldo
        // cierra el sondeo de rutas, no el uso normal de la API.
        respuesta.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        respuesta.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    /// <summary>
    /// Sondea <b>cada</b> acción protegida con un token que trae exactamente su permiso, y le pasa
    /// la respuesta a quien mira.
    /// </summary>
    /// <remarks>
    /// Un cliente por permiso y no uno por acción: abrir sesión cuesta un resumen de contraseña
    /// completo —cien mil iteraciones, a propósito— y hay cuarenta y una acciones para treinta y
    /// cuatro permisos.
    /// </remarks>
    /// <param name="mirar">Qué se comprueba de cada respuesta.</param>
    private async Task ConSuPermisoAsync(Func<ActionDescriptor, HttpResponseMessage, Task> mirar)
    {
        Dictionary<string, HttpClient> porPermiso = [];

        try
        {
            foreach (ActionDescriptor accion in Protegidas())
            {
                Permiso permiso = PeticionDeSondeo.PermisoDe(accion)!;

                if (!porPermiso.TryGetValue(permiso.Valor, out HttpClient? cliente))
                {
                    porPermiso[permiso.Valor] = cliente = await _api.ConPermisosAsync(permiso.Valor);
                }

                using HttpRequestMessage peticion = PeticionDeSondeo.De(accion, TokenDe(cliente));
                using HttpResponseMessage respuesta = await cliente.SendAsync(peticion);

                await mirar(accion, respuesta);
            }
        }
        finally
        {
            foreach (HttpClient cliente in porPermiso.Values)
            {
                cliente.Dispose();
            }
        }
    }

    /// <summary>Si esa respuesta es <b>la puerta</b> cerrada, y no una regla de negocio.</summary>
    /// <remarks>
    /// <para>
    /// Un <c>403</c> significa dos cosas distintas y solo una es la puerta. La de la política de
    /// autorización llega <b>sin cuerpo</b>: la petición no ha entrado en la acción, así que no
    /// hay nada de negocio que contar. La de una regla —«esa empresa no es la tuya»— trae su
    /// <c>problem+json</c> con un <c>type</c> propio, y para llegar a escribirlo la petición
    /// <b>ha tenido que entrar</b>.
    /// </para>
    /// <para>
    /// Distinguirlas no es un detalle: dar por buena la puerta porque «ha salido 403» es
    /// exactamente la manera de que un endpoint sin atributo pase el barrido, escondido detrás de
    /// una regla de negocio que casualmente deniega. Y al revés, en el barrido de «con su permiso
    /// se abre», un 403 de negocio se leería como una puerta que no se abre con su propio permiso.
    /// </para>
    /// </remarks>
    private static async Task<bool> EsPuertaCerradaAsync(HttpResponseMessage respuesta)
    {
        if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
        {
            return true;
        }

        if (respuesta.StatusCode != HttpStatusCode.Forbidden)
        {
            return false;
        }

        string cuerpo = await respuesta.Content.ReadAsStringAsync().ConfigureAwait(false);

        return !cuerpo.Contains("/errors/", StringComparison.Ordinal);
    }

    private static string TokenDe(HttpClient cliente) =>
        cliente.DefaultRequestHeaders.Authorization?.Parameter
        ?? throw new InvalidOperationException("El cliente no lleva token; ¿se autenticó?");

    private IEnumerable<ActionDescriptor> Acciones() =>
        _api.Services.GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>();

    private IEnumerable<ActionDescriptor> Protegidas() => Acciones().Where(accion =>
        !s_anonimas.Contains(PeticionDeSondeo.Nombre(accion)) &&
        !s_sinPermiso.Contains(PeticionDeSondeo.Nombre(accion)));
}
