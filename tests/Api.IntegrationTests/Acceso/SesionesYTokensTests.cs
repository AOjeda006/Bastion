using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.Identidad.Contracts.Sesiones;
using Bastion.Identidad.Contracts.Usuarios;
using Bastion.Identidad.Domain.Usuarios;
using Bastion.Identidad.Endpoints.Comun;
using Bastion.Organizacion.Contracts.Empresas;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Acceso;

/// <summary>
/// Cómo se entra, cómo se sigue dentro y cómo se sale.
/// </summary>
/// <remarks>
/// Todo por HTTP y contra el sistema entero. Los dos sitios donde esto se rompe sin ruido son el
/// token que nadie valida —porque la validación está escrita y no se ejerce— y el refresco que
/// sobrevive a su propio canje.
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class SesionesYTokensTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string RutaDeSesiones = "/api/v1/identidad/sesiones";
    private const string Renovacion = RutaDeSesiones + "/renovacion";
    private const string Actual = RutaDeSesiones + "/actual";
    private const string RutaDeUsuarios = "/api/v1/identidad/usuarios";

    private readonly ApiDeVerdad _api = new(postgres);

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task El_correo_que_no_existe_y_la_contrasena_mala_dan_la_MISMA_respuesta()
    {
        using HttpClient cliente = _api.CrearCliente();

        using HttpResponseMessage inexistente = await Intentar(
            cliente, $"nadie-{Guid.CreateVersion7():N}@bastion.pruebas", "una contraseña larga");
        using HttpResponseMessage equivocada = await Intentar(
            cliente, ApiDeVerdad.CorreoDelAdministrador, "una contraseña larga");

        // Distinguirlas convierte el inicio de sesión en un comprobador de correos: se prueba una
        // lista y se sabe cuáles están dados de alta, que es media suplantación hecha.
        inexistente.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        equivocada.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        JsonElement unProblema = await Problema(inexistente);
        JsonElement otroProblema = await Problema(equivocada);

        unProblema.GetProperty("type").GetString()
            .ShouldBe(otroProblema.GetProperty("type").GetString());
        unProblema.GetProperty("detail").GetString()
            .ShouldBe(otroProblema.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task El_refresco_viaja_en_una_cookie_httpOnly_y_no_en_el_cuerpo()
    {
        using HttpClient cliente = _api.CrearCliente();

        using HttpResponseMessage respuesta = await Intentar(
            cliente, ApiDeVerdad.CorreoDelAdministrador, ApiDeVerdad.ContrasenaDelAdministrador);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK);

        string cookie = respuesta.Headers.GetValues("Set-Cookie")
            .Single(valor => valor.StartsWith(CookieDeRefresco.Nombre + "=", StringComparison.Ordinal));

        // Las cuatro banderas, cada una cierra una cosa distinta. Sin HttpOnly, un solo XSS se
        // lleva sesiones de catorce días; sin Secure viaja en claro; sin SameSite la manda un
        // sitio ajeno; sin el prefijo __Host- la puede plantar un subdominio.
        cookie.ShouldContain("httponly", Case.Insensitive);
        cookie.ShouldContain("secure", Case.Insensitive);
        cookie.ShouldContain("samesite=lax", Case.Insensitive);
        cookie.ShouldContain("path=/", Case.Insensitive);

        // Y el cuerpo no lo lleva. Si lo llevara, el frontal tendría que guardarlo en algún sitio,
        // y ese sitio sería `localStorage`, que es justo lo que la cookie viene a evitar.
        (await respuesta.Content.ReadAsStringAsync()).ShouldNotContain(RefrescoDe(respuesta));
    }

    [Fact]
    public async Task El_token_de_acceso_lleva_dentro_la_empresa_activa_el_usuario_y_los_permisos()
    {
        using HttpClient cliente = _api.CrearCliente();

        using HttpResponseMessage respuesta = await Intentar(
            cliente, ApiDeVerdad.CorreoDelAdministrador, ApiDeVerdad.ContrasenaDelAdministrador);

        SesionDto sesion = (await respuesta.Content.ReadFromJsonAsync<SesionDto>())!;
        JsonElement cuerpo = CuerpoDelToken(sesion.TokenDeAcceso);

        // R8, comprobada donde vive: el identificador de empresa está EN EL TOKEN. Que además no
        // se lea de la petición es lo que prueban los tests de contrato de Organización.
        cuerpo.GetProperty(ClaimsDeBastion.Empresa).GetString()
            .ShouldBe(sesion.EmpresaActivaId.ToString());
        cuerpo.GetProperty(ClaimsDeBastion.Sujeto).GetString().ShouldBe(sesion.UsuarioId.ToString());
        cuerpo.GetProperty(ClaimsDeBastion.Permiso).GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Renovar_devuelve_otro_refresco_y_el_anterior_deja_de_valer()
    {
        using HttpClient cliente = _api.CrearCliente();
        string primero = await AbrirYQuedarseConElRefresco(cliente);

        using HttpResponseMessage renovacion = await cliente.PostAsync(Renovacion, content: null);
        renovacion.StatusCode.ShouldBe(HttpStatusCode.OK);

        RefrescoDe(renovacion).ShouldNotBe(primero);

        // Sin rotación, un refresco robado sirve catorce días y no hay manera de saberlo. Con
        // rotación, sirve hasta que el dueño renueve — y entonces se nota.
        using HttpResponseMessage conElViejo = await RenovarCon(primero);
        conElViejo.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Reutilizar_un_refresco_ya_canjeado_tumba_tambien_al_que_lo_sustituyo()
    {
        using HttpClient cliente = _api.CrearCliente();
        string robado = await AbrirYQuedarseConElRefresco(cliente);

        using HttpResponseMessage legitima = await cliente.PostAsync(Renovacion, content: null);
        legitima.StatusCode.ShouldBe(HttpStatusCode.OK);
        string vigente = RefrescoDe(legitima);

        // El ladrón usa el que copió. Ya está canjeado, así que no le sirve.
        using (HttpResponseMessage reutilizacion = await RenovarCon(robado))
        {
            reutilizacion.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        // Y aquí está lo que de verdad importa: presentar uno ya canjeado solo puede significar
        // que hay una copia por ahí, así que se corta la CADENA ENTERA. Sin esto, el ladrón se
        // queda fuera y el dueño no se entera de nada; con esto, al dueño le toca volver a entrar,
        // que es la única señal que va a recibir de que le han copiado la sesión.
        using HttpResponseMessage conElVigente = await RenovarCon(vigente);
        conElVigente.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Cerrar_sesion_borra_la_cookie_y_deja_el_refresco_inservible()
    {
        using HttpClient cliente = _api.CrearCliente();
        string refresco = await AbrirYQuedarseConElRefresco(cliente);

        using HttpResponseMessage cierre = await cliente.DeleteAsync(Actual);
        cierre.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        cierre.Headers.GetValues("Set-Cookie")
            .ShouldContain(valor => valor.StartsWith(CookieDeRefresco.Nombre + "=;", StringComparison.Ordinal));

        // Borrar la cookie en el navegador no basta: el servidor tiene que dejar de aceptarla, o
        // «cerrar sesión» en un ordenador compartido no cierra nada.
        using HttpResponseMessage despues = await RenovarCon(refresco);
        despues.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("otro emisor")]
    [InlineData("otra audiencia")]
    [InlineData("caducado")]
    [InlineData("otra clave")]
    [InlineData("firma tocada")]
    public async Task Un_token_que_no_pasa_alguna_de_las_comprobaciones_del_borde_no_entra(string defecto)
    {
        using HttpClient cliente = _api.CrearCliente();

        using HttpResponseMessage acceso = await Intentar(
            cliente, ApiDeVerdad.CorreoDelAdministrador, ApiDeVerdad.ContrasenaDelAdministrador);
        SesionDto sesion = (await acceso.Content.ReadFromJsonAsync<SesionDto>())!;

        string token = defecto switch
        {
            "otro emisor" => TokenForjado.Con(
                "https://otro.emisor", ApiDeVerdad.Audiencia, Manana, ApiDeVerdad.ClaveDeFirma),
            "otra audiencia" => TokenForjado.Con(
                ApiDeVerdad.Emisor, "otra-aplicacion", Manana, ApiDeVerdad.ClaveDeFirma),
            "caducado" => TokenForjado.Con(
                ApiDeVerdad.Emisor, ApiDeVerdad.Audiencia, Ayer, ApiDeVerdad.ClaveDeFirma),
            "otra clave" => TokenForjado.Con(
                ApiDeVerdad.Emisor, ApiDeVerdad.Audiencia, Manana, new string('k', 64)),
            _ => TokenForjado.ConLaFirmaTocada(sesion.TokenDeAcceso),
        };

        using HttpRequestMessage peticion = new(HttpMethod.Get, "/api/v1/identidad/roles");
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using HttpResponseMessage respuesta = await cliente.SendAsync(peticion);

        // Las cinco tienen que dar 401. La que más duele si falta es la firma: sin comprobarla,
        // el cuerpo del token es texto que escribe el cliente, permisos incluidos.
        respuesta.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Tras_cinco_intentos_fallidos_la_cuenta_no_admite_ni_la_contrasena_buena()
    {
        // Cuenta propia y recién hecha: bloquear la de la semilla dejaría sin entrar a los demás
        // tests de la colección, que comparten base.
        (string correo, string contrasena) = await UnaCuentaNueva();

        using HttpClient cliente = _api.CrearCliente();

        for (int intento = 0; intento < Usuario.IntentosTolerados; intento++)
        {
            using HttpResponseMessage fallido = await Intentar(cliente, correo, "no es la buena");
            fallido.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using HttpResponseMessage conLaBuena = await Intentar(cliente, correo, contrasena);

        // Sin espera tras los fallos, una contraseña de doce caracteres se prueba entera desde un
        // portátil. Y la respuesta es la misma que la de una contraseña mala: decir «está
        // bloqueada» confirmaría que la cuenta existe.
        conLaBuena.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Problema(conLaBuena)).GetProperty("detail").GetString()
            .ShouldBe("No se ha podido iniciar sesión con esos datos.");
    }

    [Fact]
    public async Task No_se_puede_pasar_a_una_empresa_a_la_que_uno_no_pertenece()
    {
        using HttpClient cliente = await _api.ComoAdministradorAsync();
        EmpresaDto ajena = await Escenario.CrearEmpresaAsync(cliente, "00000014Z");

        using HttpResponseMessage respuesta = await cliente.PutAsJsonAsync(
            $"{Actual}/empresa", new CambiarEmpresaDto { EmpresaId = ajena.Id });

        // La empresa existe y el usuario está autenticado: lo único que lo para es la pertenencia.
        // Si esto pasara, el cambio de empresa sería la puerta trasera de R8.
        respuesta.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Problema(respuesta)).GetProperty("type").GetString()
            .ShouldBe("/errors/empresa-no-pertenece");
    }

    private static DateTime Manana => DateTime.UtcNow.AddHours(1);

    private static DateTime Ayer => DateTime.UtcNow.AddHours(-1);

    private static Task<HttpResponseMessage> Intentar(HttpClient cliente, string correo, string contrasena) =>
        cliente.PostAsJsonAsync(RutaDeSesiones, new IniciarSesionDto { Correo = correo, Contrasena = contrasena });

    private static async Task<JsonElement> Problema(HttpResponseMessage respuesta)
    {
        respuesta.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    // El valor de la cookie se saca de la cabecera de la respuesta y no del contenedor del
    // cliente: el contenedor solo guarda la última, y aquí hace falta la anterior para
    // presentarla otra vez.
    private static string RefrescoDe(HttpResponseMessage respuesta)
    {
        string cookie = respuesta.Headers.GetValues("Set-Cookie")
            .Single(valor => valor.StartsWith(CookieDeRefresco.Nombre + "=", StringComparison.Ordinal));

        return cookie[(CookieDeRefresco.Nombre.Length + 1)..].Split(';')[0];
    }

    private static JsonElement CuerpoDelToken(string token)
    {
        string carga = token.Split('.')[1];
        string relleno = carga.Replace('-', '+').Replace('_', '/')
            .PadRight(carga.Length + ((4 - (carga.Length % 4)) % 4), '=');

        return JsonSerializer.Deserialize<JsonElement>(Convert.FromBase64String(relleno));
    }

    private static async Task<string> AbrirYQuedarseConElRefresco(HttpClient cliente)
    {
        using HttpResponseMessage respuesta = await Intentar(
            cliente, ApiDeVerdad.CorreoDelAdministrador, ApiDeVerdad.ContrasenaDelAdministrador);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK);

        return RefrescoDe(respuesta);
    }

    private async Task<HttpResponseMessage> RenovarCon(string refresco)
    {
        // Cliente limpio y cookie puesta a mano: es la manera de presentar UNA cookie concreta,
        // que es de lo que van estos tests.
        using HttpClient limpio = _api.CrearCliente();
        using HttpRequestMessage peticion = new(HttpMethod.Post, Renovacion);
        peticion.Headers.Add("Cookie", $"{CookieDeRefresco.Nombre}={refresco}");
        peticion.Content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

        return await limpio.SendAsync(peticion);
    }

    private async Task<(string Correo, string Contrasena)> UnaCuentaNueva()
    {
        using HttpClient administrador = await _api.ComoAdministradorAsync();

        string sufijo = Guid.CreateVersion7().ToString("N");
        string correo = $"bloqueable-{sufijo}@bastion.pruebas";
        string contrasena = sufijo[..16] + "aA1!";

        using HttpResponseMessage alta = await administrador.PostAsJsonAsync(
            RutaDeUsuarios,
            new CrearUsuarioDto
            {
                Correo = correo,
                Nombre = "Cuenta bloqueable",
                Contrasena = contrasena,
            });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());

        return (correo, contrasena);
    }
}
